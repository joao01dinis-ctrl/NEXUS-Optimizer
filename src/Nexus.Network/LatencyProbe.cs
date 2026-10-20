using System.Net;
using System.Net.NetworkInformation;

namespace Nexus.Network;

public sealed record LatencyResult(string Address, int Sent, long[] Samples)
{
    public double LossPercent => 100d * (Sent - Samples.Length) / Sent;
    public double? Average => Samples.Length == 0 ? null : Samples.Average();
    public double? Jitter => Samples.Length < 2 ? null : Samples.Zip(Samples.Skip(1), (a, b) => Math.Abs(b - a)).Average();
    public string Display => $"{Address} · {Samples.Length}/{Sent} respostas · perda {LossPercent:F0}%\n" +
        (Average is null ? "Sem respostas. ICMP pode estar bloqueado; isto não comprova falha da ligação." : $"Média {Average:F1} ms · mínimo {Samples.Min()} ms · máximo {Samples.Max()} ms\nVariação entre respostas: {(Jitter is null ? "sem amostras suficientes" : $"{Jitter:F1} ms")}");
}
public static class LatencyProbe
{
    public static async Task<LatencyResult> MeasureAsync(string address, CancellationToken token = default)
    {
        if (!IPAddress.TryParse(address, out var ip) || IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.Broadcast))
            throw new ArgumentException("Introduz o IP do gateway ou de um destino que queiras testar.");
        using var ping = new Ping(); var samples = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            token.ThrowIfCancellationRequested();
            var reply = await ping.SendPingAsync(ip, TimeSpan.FromSeconds(1), new byte[32], new PingOptions(), token);
            if (reply.Status == IPStatus.Success) samples.Add(reply.RoundtripTime);
            if (i < 4) await Task.Delay(250, token);
        }
        return new(ip.ToString(), 5, samples.ToArray());
    }
}
