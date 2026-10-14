using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nexus.Optimization;

public sealed record AiAdvice(string Summary, IReadOnlyList<string> Observations, IReadOnlyList<string> ApprovedKeys);

// Optional advice only. No command/tool execution, process names, file paths,
// network identifiers or hardware identifiers are sent to the API.
public sealed class OpenAiAdvisor(HttpClient client, string apiKey, string model = "gpt-4.1-mini")
{
    public static object SummaryFor(BoostReport report) => new
    {
        goal = report.Input.Goal.ToString(),
        cpuPressure = report.SustainedCpuPressure,
        memoryPressure = report.SustainedMemoryPressure,
        sampleCount = report.Input.Samples.Count,
        cpuPercent = report.Input.Samples.Where(s => s.CpuPercent is not null).Select(s => s.CpuPercent).ToArray(),
        memoryPercent = report.Input.Samples.Select(s => s.MemoryPercent).ToArray(),
        totalMemoryGb = report.Input.Samples.LastOrDefault()?.TotalGb,
        lowSpaceVolumeCount = report.Input.Volumes.Count(v => v.TotalBytes > 0 && v.FreeBytes >= 0 && v.FreeBytes < v.TotalBytes * .15),
        onAcPower = report.Input.OnAcPower,
        batterySaver = report.Input.BatterySaver,
        allowedVisualKeys = AutomaticBoost.Prepare(report, report.At).Changes.Select(c => c.Key).ToArray()
    };

    public async Task<AiAdvice> AnalyzeAsync(BoostReport report, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("A IA OpenAI ainda não tem uma chave configurada.");
        var allowed = AutomaticBoost.Prepare(report, DateTimeOffset.Now).Changes.Select(c => c.Key).ToArray();
        var schema = new
        {
            type = "object", additionalProperties = false, required = new[] { "summary", "observations", "recommendedKeys" },
            properties = new
            {
                summary = new { type = "string" },
                observations = new { type = "array", items = new { type = "string" }, maxItems = 5 },
                recommendedKeys = new { type = "array", items = new { type = "string", @enum = allowed.Length == 0 ? new[] { "no_change" } : allowed }, maxItems = 5 }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model, store = false, max_output_tokens = 900,
            instructions = "És o consultor da NEXUS. Responde em português de Portugal, curto e claro. Os dados são observações limitadas. Explica a pressão de CPU/RAM e as limitações. Só podes recomendar chaves da lista allowedVisualKeys. Se estiver vazia, recommendedKeys deve ser []. Nunca sugiras comandos, registo adicional, desativar segurança, anti-cheat, serviços, atualizações, paginação, fechar processos ou limpar memória/caches. Não alegues melhoria de FPS, mira, ping, temperatura ou input lag. Não inventes medições nem diagnósticos de GPU. A execução é decidida por regras locais, não por ti.",
            input = JsonSerializer.Serialize(SummaryFor(report)),
            text = new { format = new { type = "json_schema", name = "nexus_pc_advice", strict = true, schema } }
        });
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "A OpenAI recusou a chave. A análise local continua disponível.",
                HttpStatusCode.TooManyRequests => "A OpenAI atingiu um limite ou não tem saldo. Não foram feitas novas tentativas automáticas.",
                _ => $"A OpenAI não concluiu a análise (HTTP {(int)response.StatusCode}). A análise local continua disponível."
            });
        if (response.Content.Headers.ContentLength > 100000) throw new InvalidOperationException("A resposta da IA excedeu o limite.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > 100000) throw new InvalidOperationException("A resposta da IA excedeu o limite.");
            buffer.Write(chunk, 0, read);
        }
        using var json = JsonDocument.Parse(buffer.ToArray());
        if (!json.RootElement.TryGetProperty("status", out var status) || status.GetString() != "completed")
            throw new InvalidOperationException("A IA não concluiu uma resposta válida. Nenhuma recomendação foi aplicada.");
        var texts = new List<string>();
        foreach (var item in json.RootElement.GetProperty("output").EnumerateArray())
            if (item.TryGetProperty("content", out var content))
                foreach (var part in content.EnumerateArray())
                    if (part.GetProperty("type").GetString() == "output_text") texts.Add(part.GetProperty("text").GetString() ?? "");
        return Validate(string.Concat(texts), allowed);
    }

    public static AiAdvice Validate(string text, IReadOnlyCollection<string> allowed)
    {
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        var summary = root.GetProperty("summary").GetString() ?? throw new JsonException("Resumo em falta.");
        var observations = root.GetProperty("observations").EnumerateArray().Select(p => p.GetString() ?? "").ToArray();
        var keys = root.GetProperty("recommendedKeys").EnumerateArray().Select(p => p.GetString() ?? "").ToArray();
        if (summary.Length is 0 or > 2000 || observations.Length > 5 || observations.Any(s => s.Length > 1500) ||
            keys.Length > 5 || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length ||
            keys.Any(k => !allowed.Contains(k) || !SmartBoost.VisualKeys.Contains(k)))
            throw new InvalidOperationException("A resposta da IA ultrapassou as permissões da análise local e foi rejeitada.");
        return new(summary, Array.AsReadOnly(observations), Array.AsReadOnly(keys));
    }
}
