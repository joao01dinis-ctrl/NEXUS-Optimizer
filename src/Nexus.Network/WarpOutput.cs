using System.Text.RegularExpressions;

namespace Nexus.Network;

public static class WarpOutput
{
    private static string[] Lines(string text)
    {
        if (text.Length > 65536 || text.Contains('\0')) throw new InvalidDataException("Resposta WARP inválida ou demasiado grande.");
        return text.Split('\n').Select(x => x.Trim().TrimEnd('\r')).ToArray();
    }
    public static bool HasRequiredCommands(string help)
    {
        var lines = Lines(help);
        return new[] { "status", "settings", "connect", "disconnect" }.All(command =>
            lines.Any(line => Regex.IsMatch(line, "^" + command + @"\s+\S", RegexOptions.CultureInvariant)));
    }
    public static bool NeedsSetup(string text) => Lines(text).Any(line =>
        line.Contains("Accept Terms", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("terms of service", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("registration missing", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Missing Registration", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("not registered", StringComparison.OrdinalIgnoreCase));
    public static bool RequiresInteractiveSetup(string command, string output) => command != "--help" && NeedsSetup(output);
    public static VpnConnectionState State(string text)
    {
        var states = new HashSet<VpnConnectionState>();
        foreach (var line in Lines(text))
        {
            var value = line.StartsWith("Status update:", StringComparison.Ordinal) ? line[14..].Trim() : line;
            var state = value switch
            {
                "Connected" => VpnConnectionState.Connected,
                "Disconnected" => VpnConnectionState.Disconnected,
                "Connecting" => VpnConnectionState.Connecting,
                "Disconnecting" => VpnConnectionState.Disconnecting,
                _ when value.StartsWith("Unable to connect", StringComparison.Ordinal) || value.StartsWith("Error:", StringComparison.Ordinal) => VpnConnectionState.Error,
                _ => VpnConnectionState.Unknown
            };
            if (state != VpnConnectionState.Unknown) states.Add(state);
        }
        return states.Count == 1 ? states.Single() : VpnConnectionState.Unknown;
    }
    public static WarpTrafficMode Mode(string settings)
    {
        var matches = Lines(settings).Select(line => Regex.Match(line, @"^(?:\([^\r\n)]{1,60}\)\s*)?Mode:\s*(\w+)\s*$", RegexOptions.CultureInvariant))
            .Where(x => x.Success).Select(x => x.Groups[1].Value).ToArray();
        if (matches.Length != 1) return WarpTrafficMode.Unknown;
        return matches[0] switch
        {
            "Warp" or "WarpWithDnsOverHttps" or "WarpWithDnsOverTls" => WarpTrafficMode.TrafficAndDns,
            "TunnelOnly" => WarpTrafficMode.TrafficOnly,
            "DnsOverHttps" or "DnsOverTls" or "DnsOnly" => WarpTrafficMode.DnsOnly,
            "WarpProxy" => WarpTrafficMode.LocalProxy,
            _ => WarpTrafficMode.Unknown
        };
    }
    public static bool OrganizationManaged(string settings) => Lines(settings).Any(line =>
        Regex.IsMatch(line, @"^(?:\([^\r\n)]{1,60}\)\s*)?Organization:\s*\S", RegexOptions.CultureInvariant));
}

public enum WarpTraceState { Unknown, Off, On }
public static class WarpTrace
{
    public static WarpTraceState Parse(string text)
    {
        if (text.Length > 16384 || text.Contains('\0')) return WarpTraceState.Unknown;
        var values = text.Split('\n').Select(x => x.TrimEnd('\r')).Where(x => x.StartsWith("warp=", StringComparison.Ordinal)).ToArray();
        return values.Length == 1 ? values[0] switch { "warp=on" or "warp=plus" => WarpTraceState.On, "warp=off" => WarpTraceState.Off, _ => WarpTraceState.Unknown } : WarpTraceState.Unknown;
    }
}
