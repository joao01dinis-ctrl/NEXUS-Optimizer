using Nexus.Core;
using Nexus.Optimization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Nexus.Tests;

public sealed class IntelligenceTests
{
    private static BoostReport Report(double cpu = 92)
    {
        var at = DateTimeOffset.Now.AddSeconds(-9);
        return SmartBoost.Evaluate(new(BoostGoal.Balanced,
            Enumerable.Range(0, 5).Select(i => new Sample(cpu, 40, 12.8, 32, at.AddSeconds(i * 2))).ToArray(),
            [new("PRIVATE_DISK_NAME", 1000, 100)], [new("PRIVATE_PROCESS", 20)], true, false,
            "PRIVATE_CPU", "PRIVATE_GPU", "PRIVATE_OS", ["PRIVATE_DRIVER"],
            SmartBoost.VisualKeys.Select(k => new BoostSetting(k, k, "0", "1")).ToArray()));
    }
    [Fact] public void AutomaticModeDoesNothingWithoutSustainedPressure()
        => Assert.Empty(AutomaticBoost.Prepare(Report(20), DateTimeOffset.Now).Changes);
    [Fact] public void ForgedAiDecisionsCannotAddPowerOrMouseChanges()
    {
        var report = Report();
        report = report with { Rules = [new("injected", BoostDecision.Apply, "unsafe", "", "", "", "power")] };
        var plan = AutomaticBoost.Prepare(report, DateTimeOffset.Now);
        Assert.Equal(5, plan.Changes.Count);
        Assert.All(plan.Changes, c => { Assert.Contains(c.Key, SmartBoost.VisualKeys); Assert.Equal("0", c.Value); });
    }
    [Fact] public void ExpiredAndFutureReportsCannotProduceAutomaticPlans()
    {
        var report = Report();
        Assert.Throws<InvalidOperationException>(() => AutomaticBoost.Prepare(report, report.At.AddMinutes(6)));
        Assert.Throws<InvalidOperationException>(() => AutomaticBoost.Prepare(report, report.At.AddSeconds(-1)));
    }
    [Fact] public void AutomaticBatchRecordsAndRestoresEveryValue()
    {
        var path = Path.Combine(Path.GetTempPath(), "nexus-auto-" + Guid.NewGuid().ToString("N"), "test.db");
        var settings = SmartBoost.VisualKeys.ToDictionary(k => k, k => new FakeSetting(k));
        var optimizer = new SystemOptimizer(path, k => settings[k]);
        var plan = AutomaticBoost.Prepare(Report(), DateTimeOffset.Now);
        Assert.Equal(5, plan.Apply(optimizer, DateTimeOffset.Now));
        Assert.All(settings.Values, s => Assert.Equal("0", s.Read()));
        Assert.Equal(5, optimizer.History().Count);
        Assert.True(optimizer.RestoreAll().Complete);
        Assert.All(settings.Values, s => Assert.Equal("1", s.Read()));
    }
    [Fact] public void AChangedSettingStopsTheWholeBatchBeforeAnyWrite()
    {
        var path = Path.Combine(Path.GetTempPath(), "nexus-auto-conflict-" + Guid.NewGuid().ToString("N"), "test.db");
        var settings = SmartBoost.VisualKeys.ToDictionary(k => k, k => new FakeSetting(k));
        var optimizer = new SystemOptimizer(path, k => settings[k]);
        var plan = AutomaticBoost.Prepare(Report(), DateTimeOffset.Now);
        settings[SmartBoost.VisualKeys[^1]].Write("0");
        Assert.Throws<InvalidOperationException>(() => plan.Apply(optimizer, DateTimeOffset.Now));
        Assert.Empty(optimizer.History());
        Assert.All(settings.Values.Take(4), s => Assert.Equal("1", s.Read()));
    }
    [Fact] public void AManuallyConstructedUnsafePlanIsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), "nexus-blocked-" + Guid.NewGuid().ToString("N"), "test.db");
        var optimizer = new SystemOptimizer(path, _ => throw new Exception("Resolver must not be called"));
        Assert.Throws<InvalidOperationException>(() => new AutomaticBoostPlan(DateTimeOffset.Now, [new("power", "any", "any")]).Apply(optimizer, DateTimeOffset.Now));
        Assert.Empty(optimizer.History());
    }
    [Fact]
    public void PeriodicModeRespectsUndoAndExternalChanges()
    {
        var plan = AutomaticBoost.Prepare(Report(), DateTimeOffset.Now);
        var undo = new OptimizationRecord(2, "now", "menu-animation", "menu", "1", "0", "reverted");
        var external = new OptimizationRecord(1, "now", "tooltip-animation", "tooltip", "1", "0", "applied");
        var filtered = AutomaticBoost.RespectUserChanges(plan, [undo, external], [new(external, "1", null)]);
        Assert.Equal(3, filtered.Changes.Count);
        Assert.DoesNotContain(filtered.Changes, c => c.Key is "menu-animation" or "tooltip-animation");
    }
    [Theory]
    [InlineData("power")][InlineData("mouse-acceleration")][InlineData("dns:1")][InlineData("DisableDefender")][InlineData("no_change")]
    public void AiCannotSuggestAnyUnapprovedKey(string key)
    {
        var body = JsonSerializer.Serialize(new { summary = "Resumo", observations = Array.Empty<string>(), recommendedKeys = new[] { key } });
        Assert.Throws<InvalidOperationException>(() => OpenAiAdvisor.Validate(body, ["menu-animation"]));
    }
    [Fact] public void AiCannotElevateAnOptionalOrIgnoredVisualPreference()
    {
        var body = "{\"summary\":\"Resumo\",\"observations\":[],\"recommendedKeys\":[\"menu-animation\"]}";
        Assert.Throws<InvalidOperationException>(() => OpenAiAdvisor.Validate(body, []));
    }
    [Fact] public void ApiSummaryDoesNotContainPrivateIdentifiers()
    {
        var body = JsonSerializer.Serialize(OpenAiAdvisor.SummaryFor(Report()));
        Assert.DoesNotContain("PRIVATE_", body);
        Assert.DoesNotContain("Processes", body);
        Assert.DoesNotContain("Drivers", body);
        Assert.Contains("sampleCount", body);
    }
    [Fact] public async Task ResponseApiUsesRestrictedJsonAndDoesNotStoreResponses()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new[] { new { content = new[] { new { type = "output_text", text = "{\"summary\":\"Pressão observada\",\"observations\":[\"FPS não medidos\"],\"recommendedKeys\":[\"menu-animation\"]}" } } } }
        }));
        using var client = new HttpClient(handler);
        var advice = await new OpenAiAdvisor(client, "fake-test-token").AnalyzeAsync(Report());
        Assert.Equal("Pressão observada", advice.Summary);
        Assert.Contains("menu-animation", advice.ApprovedKeys);
        using var sent = JsonDocument.Parse(handler.Body!);
        Assert.False(sent.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal("json_schema", sent.RootElement.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.DoesNotContain("PRIVATE_", handler.Body!);
        Assert.DoesNotContain("fake-test-token", handler.Body!);
        Assert.Equal("https://api.openai.com/v1/responses", handler.Url);
        Assert.Equal(1, handler.Calls);
    }
    [Theory][InlineData(401)][InlineData(429)][InlineData(500)]
    public async Task ApiFailuresAreNotRetriedOrExposed(int status)
    {
        var handler = new FakeHandler((HttpStatusCode)status, "PRIVATE_ERROR_BODY");
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new OpenAiAdvisor(client, "fake").AnalyzeAsync(Report()));
        Assert.DoesNotContain("PRIVATE_ERROR_BODY", error.Message);
        Assert.Equal(1, handler.Calls);
    }
    [Fact] public async Task IncompleteResponsesCannotBecomeAdvice()
    {
        using var client = new HttpClient(new FakeHandler(HttpStatusCode.OK, "{\"status\":\"incomplete\",\"output\":[]}"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new OpenAiAdvisor(client, "fake").AnalyzeAsync(Report()));
    }
    [Theory][InlineData(800, 1, 1600, .5)][InlineData(1600, .2, 800, .4)]
    public void DpiConversionPreservesTheSameLinearSensitivity(double oldDpi, double sensitivity, double newDpi, double expected)
        => Assert.Equal(expected, MousePrecision.EquivalentSensitivity(oldDpi, sensitivity, newDpi), 8);
    [Theory][InlineData(double.NaN)][InlineData(0)][InlineData(double.PositiveInfinity)]
    public void InvalidDpiIsNeverUsed(double dpi)
        => Assert.Throws<ArgumentOutOfRangeException>(() => MousePrecision.EquivalentSensitivity(dpi, 1, 800));
    [Theory][InlineData("6,10,1")][InlineData("0,0,0")][InlineData("30,40,2")]
    public void MouseUndoPreservesEveryCapturedThreshold(string state)
        => Assert.Equal(state, string.Join(",", MousePrecision.ParseState(state)));
    [Theory][InlineData("0,0,3")][InlineData("-1,0,1")][InlineData("0,0")][InlineData("0,0,powershell")]
    public void InvalidMouseStatesAreRejected(string state)
        => Assert.Throws<ArgumentException>(() => MousePrecision.ParseState(state));

    private sealed class FakeSetting(string key) : ISystemSetting
    {
        private string value = "1";
        public string Key => key;
        public string Name => key;
        public string Read() => value;
        public void Write(string next) => value = next;
    }
    private sealed class FakeHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Body, Url;
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Url = request.RequestUri?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
