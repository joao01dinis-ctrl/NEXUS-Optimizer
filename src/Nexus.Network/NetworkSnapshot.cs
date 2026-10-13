using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Nexus.Network;

public sealed record NetworkAdapterSnapshot(string Name, string Description, bool Connected,
    long LinkBitsPerSecond, string[] Addresses, string[] Gateways, string[] DnsServers, bool? Dhcp)
{
    public string Display => $"{Name} · {(Connected ? "Ligada" : "Desligada")}\n{Description}\n" +
        $"Ligação anunciada: {(LinkBitsPerSecond > 0 ? $"{LinkBitsPerSecond / 1_000_000d:F0} Mbps" : "indisponível")}\n" +
        $"IP: {List(Addresses)}\nGateway: {List(Gateways)}\nDNS atual: {List(DnsServers)}\nDHCP IPv4: {(Dhcp is true ? "Sim" : Dhcp is false ? "Não" : "indisponível")}";
    private static string List(string[] items) => items.Length == 0 ? "não informado" : string.Join(", ", items);
}

public static class NetworkSnapshot
{
    // Local adapter inspection only. No packets, public-IP lookup or configuration writes.
    public static IReadOnlyList<NetworkAdapterSnapshot> Read()
    {
        var result = new List<NetworkAdapterSnapshot>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            try
            {
                var p = adapter.GetIPProperties(); bool? dhcp = null;
                try { dhcp = p.GetIPv4Properties()?.IsDhcpEnabled; }
                catch (Exception e) when (e is NetworkInformationException or NotSupportedException) { }
                result.Add(new(adapter.Name, adapter.Description, adapter.OperationalStatus == OperationalStatus.Up,
                    adapter.Speed, p.UnicastAddresses.Where(x => x.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6).Select(x => x.Address.ToString()).ToArray(),
                    p.GatewayAddresses.Select(x => x.Address.ToString()).ToArray(), p.DnsAddresses.Select(x => x.ToString()).ToArray(), dhcp));
            }
            catch (Exception e) when (e is NetworkInformationException or NotSupportedException) { }
        }
        return result.OrderByDescending(x => x.Connected).ThenBy(x => x.Name).ToArray();
    }
}
