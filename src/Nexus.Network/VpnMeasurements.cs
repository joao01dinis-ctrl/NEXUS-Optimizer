using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;

namespace Nexus.Network;

public sealed record VpnProbeSample(DateTimeOffset At, bool HttpsResponded, WarpTraceState Trace, long? IcmpMilliseconds, string? Error = null);
public interface IVpnProbe { Task<VpnProbeSample> MeasureAsync(CancellationToken cancellation); }

public sealed class VpnMeasurements(IVpnProbe probe)
{
    private readonly Queue<VpnProbeSample> samples = new();
    public IReadOnlyList<VpnProbeSample> Samples => samples.ToArray();
    public async Task<VpnProbeSample> SampleAsync(bool confirmed, CancellationToken cancellation = default)
    {
        if (!confirmed) throw new InvalidOperationException("Autoriza o teste externo antes de medir a ligação.");
        var sample = await probe.MeasureAsync(cancellation);
        if (sample.IcmpMilliseconds is < 0) sample = sample with { IcmpMilliseconds = null };
        samples.Enqueue(sample); while (samples.Count > 12) samples.Dequeue(); return sample;
    }
    public void Clear() => samples.Clear();
    public static bool ConfirmsTestTraffic(VpnStatus? status, VpnProbeSample? sample, DateTimeOffset now) =>
        status is { ReportsTunnel: true } && sample is { HttpsResponded: true, Trace: WarpTraceState.On } &&
        status.CheckedAt <= now && now - status.CheckedAt <= TimeSpan.FromSeconds(20) &&
        sample.At <= now && now - sample.At <= TimeSpan.FromSeconds(30);
    public string Summary
    {
        get
        {
            if (samples.Count == 0) return "Ainda não foram feitas medições. A consulta local do WARP não envia testes externos.";
            var values = samples.ToArray(); var ping = values.Where(x => x.IcmpMilliseconds is not null).Select(x => x.IcmpMilliseconds!.Value).ToArray();
            var intervals = values.Zip(values.Skip(1), (a, b) => a.IcmpMilliseconds is long first && b.IcmpMilliseconds is long second ? (double?)Math.Abs(second - first) : null).Where(x => x is not null).Select(x => x!.Value).ToArray();
            return $"HTTPS Cloudflare: {values.Count(x => x.HttpsResponded)}/{values.Length} respostas válidas · ICMP 1.1.1.1: {ping.Length}/{values.Length} respostas\n" +
                (ping.Length == 0 ? "Sem latência ICMP disponível; pode estar bloqueado sem falha da Internet.\n" : $"Média ICMP: {ping.Average():F1} ms · variação entre respostas consecutivas: {(intervals.Length == 0 ? "sem amostras suficientes" : $"{intervals.Average():F1} ms")}\n") +
                "Amostras desta medição. Não é o ping do jogo, uma previsão de ganhos ou prova de ausência de fugas DNS/IPv6.";
        }
    }
}

public sealed class CloudflareVpnProbe : IVpnProbe, IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
    public async Task<VpnProbeSample> MeasureAsync(CancellationToken cancellation)
    {
        var https = ReadTraceAsync(cancellation); var ping = PingAsync(cancellation);
        await Task.WhenAll(https, ping);
        var result = await https;
        return new(DateTimeOffset.UtcNow, result.Responded, result.Trace, await ping, result.Error);
    }
    private async Task<(bool Responded, WarpTraceState Trace, string? Error)> ReadTraceAsync(CancellationToken cancellation)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.cloudflare.com/cdn-cgi/trace");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (response.StatusCode != HttpStatusCode.OK) return (false, WarpTraceState.Unknown, "O endpoint de teste não devolveu uma resposta válida.");
            if (response.Content.Headers.ContentLength > 16384) return (false, WarpTraceState.Unknown, "Resposta de teste inesperada.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(5));
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8); var buffer = new char[1024]; var text = new StringBuilder();
            while (await reader.ReadAsync(buffer, deadline.Token) is var read && read > 0)
            { if (text.Length + read > 16384) return (false, WarpTraceState.Unknown, "Resposta de teste inesperada."); text.Append(buffer, 0, read); }
            var trace = WarpTrace.Parse(text.ToString());
            // Deliberately discard the remaining trace, including the public IP and request metadata.
            return (trace != WarpTraceState.Unknown, trace, trace == WarpTraceState.Unknown ? "Não foi possível interpretar a verificação de tráfego." : null);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return (false, WarpTraceState.Unknown, "O teste HTTPS excedeu o tempo previsto."); }
        catch (HttpRequestException) { return (false, WarpTraceState.Unknown, "O teste HTTPS falhou. Pode existir uma falha de ligação, portal de acesso ou bloqueio do endpoint."); }
        catch (IOException) { return (false, WarpTraceState.Unknown, "A resposta de teste foi interrompida."); }
    }
    private static async Task<long?> PingAsync(CancellationToken cancellation)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(IPAddress.Parse("1.1.1.1"), TimeSpan.FromSeconds(2), new byte[32], new PingOptions(), cancellation);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (PingException) { return null; }
        catch (System.Net.Sockets.SocketException) { return null; }
    }
    public void Dispose() => client.Dispose();
}
