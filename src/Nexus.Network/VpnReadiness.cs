namespace Nexus.Network;

public enum VpnConnectionState { Unavailable, Disconnected, Connecting, Connected, Disconnecting, Error, Unknown }
public sealed record VpnReadiness(bool CanConnect, string Reason)
{
    public static VpnReadiness Evaluate(int provisionedServers, bool trustedComponentInstalled, bool deviceRegistered)
    {
        if (provisionedServers <= 0) return new(false, "Ainda não existe um servidor VPN real configurado.");
        if (!trustedComponentInstalled) return new(false, "O componente WireGuard assinado ainda não foi instalado pelo instalador.");
        if (!deviceRegistered) return new(false, "Este dispositivo ainda não tem uma configuração autenticada.");
        return new(true, "Pré-requisitos presentes. A ligação e a ausência de fugas ainda precisam de ser verificadas.");
    }
}
public sealed record VpnServerHealth(string Id, string Location, bool Available, double? LatencyMs, DateTimeOffset MeasuredAt, double? Load = null);
public static class VpnServerSelection
{
    // Only advertised, available servers with fresh real measurements qualify.
    // This policy does not itself perform a probe, provision a server or connect.
    public static VpnServerHealth? QuickConnect(IEnumerable<VpnServerHealth> servers, DateTimeOffset now) =>
        servers.Where(s => !string.IsNullOrWhiteSpace(s.Id) && s.Available &&
            s.LatencyMs is >= 0 && double.IsFinite(s.LatencyMs.Value) &&
            s.MeasuredAt <= now && now - s.MeasuredAt <= TimeSpan.FromMinutes(1) &&
            (s.Load is null || s.Load is >= 0 and < .95))
        .OrderBy(s => s.LatencyMs).ThenBy(s => s.Load ?? 1).FirstOrDefault();
}
