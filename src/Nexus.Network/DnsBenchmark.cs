using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Nexus.Network;

public sealed record DnsResolver(string Name, string Address);
public sealed record DnsBenchmarkResult(string Name, string Address, int SuccessfulSamples,
    double? MedianMilliseconds, string? Error)
{
    public string Display => MedianMilliseconds is double ms
        ? $"{Name} • {Address} • {ms:F1} ms • {SuccessfulSamples}/3 respostas"
        : $"{Name} • {Address} • indisponível ({Error})";
}

/// <summary>Measures example.com A lookups. This is DNS responsiveness, not game ping or bandwidth.</summary>
public sealed class DnsBenchmark
{
    public static IReadOnlyList<DnsResolver> Resolvers { get; } = Array.AsReadOnly(new[]
    {
        new DnsResolver("Cloudflare", "1.1.1.1"),
        new DnsResolver("Google", "8.8.8.8"),
        new DnsResolver("Quad9", "9.9.9.9"),
        new DnsResolver("AdGuard", "94.140.14.14")
    });

    private readonly IDnsTransport transport;
    private readonly TimeSpan timeout;

    public DnsBenchmark(TimeSpan? timeout = null) : this(new UdpDnsTransport(), timeout ?? TimeSpan.FromMilliseconds(1500)) { }
    internal DnsBenchmark(IDnsTransport transport, TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(10))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        this.transport = transport;
        this.timeout = timeout;
    }

    public Task<IReadOnlyList<DnsBenchmarkResult>> MeasureAsync(CancellationToken cancellationToken = default)
        => MeasureAsync(Resolvers, cancellationToken);

    public async Task<IReadOnlyList<DnsBenchmarkResult>> MeasureAsync(IEnumerable<DnsResolver> resolvers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolvers);
        var selected = resolvers.ToArray();
        if (selected.Length is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(resolvers));
        foreach (var resolver in selected)
            if (string.IsNullOrWhiteSpace(resolver.Name) || !IPAddress.TryParse(resolver.Address, out var ip) ||
                ip.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("O teste exige um servidor DNS IPv4 válido.", nameof(resolvers));
        var results = await Task.WhenAll(selected.Select(x => MeasureResolverAsync(x, cancellationToken)));
        return results.OrderBy(x => x.MedianMilliseconds ?? double.MaxValue).ThenBy(x => x.Name).ToArray();
    }

    private async Task<DnsBenchmarkResult> MeasureResolverAsync(DnsResolver resolver, CancellationToken token)
    {
        var samples = new List<double>(3);
        string? error = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var id = (ushort)RandomNumberGenerator.GetInt32(ushort.MaxValue + 1);
            var query = DnsPacket.CreateQuery(id);
            try
            {
                var exchange = await transport.ExchangeAsync(resolver, query, timeout, token);
                if (!DnsPacket.IsValidAnswer(exchange.Response, id))
                    throw new InvalidDataException("resposta DNS inválida ou sem endereço");
                if (!double.IsFinite(exchange.Milliseconds) || exchange.Milliseconds < 0)
                    throw new InvalidDataException("tempo inválido");
                samples.Add(exchange.Milliseconds);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { error = "tempo esgotado"; }
            catch (Exception e) when (e is SocketException or IOException or InvalidDataException)
            { error = e is SocketException ? "servidor inacessível" : e.Message; }
        }
        samples.Sort();
        double? median = samples.Count switch
        {
            0 => null,
            2 => (samples[0] + samples[1]) / 2,
            _ => samples[samples.Count / 2]
        };
        return new(resolver.Name, resolver.Address, samples.Count, median, error);
    }
}

internal sealed record DnsExchange(byte[] Response, double Milliseconds);
internal interface IDnsTransport
{
    Task<DnsExchange> ExchangeAsync(DnsResolver resolver, byte[] query, TimeSpan timeout, CancellationToken token);
}

internal sealed class UdpDnsTransport : IDnsTransport
{
    public async Task<DnsExchange> ExchangeAsync(DnsResolver resolver, byte[] query, TimeSpan timeout, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.Connect(IPAddress.Parse(resolver.Address), 53);
        var watch = Stopwatch.StartNew();
        await client.SendAsync(query.AsMemory(), deadline.Token);
        var response = await client.ReceiveAsync(deadline.Token);
        watch.Stop();
        return new(response.Buffer, watch.Elapsed.TotalMilliseconds);
    }
}

internal static class DnsPacket
{
    private const string Domain = "example.com";
    public static byte[] CreateQuery(ushort id)
    {
        var packet = new byte[29];
        BinaryPrimitives.WriteUInt16BigEndian(packet, id);
        packet[2] = 1; // Recursion desired; standard query.
        packet[5] = 1;
        packet[12] = 7;
        Encoding.ASCII.GetBytes("example", packet.AsSpan(13));
        packet[20] = 3;
        Encoding.ASCII.GetBytes("com", packet.AsSpan(21));
        packet[26] = 1; // A
        packet[28] = 1; // IN
        return packet;
    }

    public static bool IsValidAnswer(ReadOnlySpan<byte> packet, ushort id)
    {
        if (packet.Length < 12 || U16(packet, 0) != id) return false;
        var flags = U16(packet, 2);
        if ((flags & 0x8000) == 0 || (flags & 0x7800) != 0 || (flags & 0x0200) != 0 || (flags & 0x000f) != 0 ||
            U16(packet, 4) != 1 || U16(packet, 6) == 0) return false;
        var offset = 12;
        if (!TryReadName(packet, ref offset, out var name) || !name.Equals(Domain, StringComparison.OrdinalIgnoreCase) ||
            offset + 4 > packet.Length || U16(packet, offset) != 1 || U16(packet, offset + 2) != 1) return false;
        offset += 4;
        var found = false;
        for (var i = 0; i < U16(packet, 6); i++)
        {
            if (!TryReadName(packet, ref offset, out var owner) || offset + 10 > packet.Length) return false;
            var type = U16(packet, offset);
            var addressClass = U16(packet, offset + 2);
            var size = U16(packet, offset + 8);
            offset += 10;
            if (offset + size > packet.Length) return false;
            if (type == 1 && addressClass == 1 && size == 4 && owner.Equals(Domain, StringComparison.OrdinalIgnoreCase))
                found = true;
            offset += size;
        }
        return found;
    }

    private static ushort U16(ReadOnlySpan<byte> packet, int offset)
        => BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset, 2));

    private static bool TryReadName(ReadOnlySpan<byte> packet, ref int offset, out string name)
    {
        name = "";
        var position = offset;
        var nextOffset = -1;
        var labels = new List<string>();
        var length = 0;
        // Bounded traversal rejects cyclic compression pointers and oversized names.
        for (var step = 0; step < 128; step++)
        {
            if (position >= packet.Length) return false;
            var size = packet[position++];
            if (size == 0)
            {
                offset = nextOffset < 0 ? position : nextOffset;
                name = string.Join('.', labels);
                return true;
            }
            if ((size & 0xc0) == 0xc0)
            {
                if (position >= packet.Length) return false;
                if (nextOffset < 0) nextOffset = position + 1;
                position = ((size & 0x3f) << 8) | packet[position];
                continue;
            }
            if ((size & 0xc0) != 0 || size > 63 || position + size > packet.Length) return false;
            length += size + 1;
            if (length > 254) return false;
            var label = packet.Slice(position, size);
            foreach (var character in label)
                if (character < 33 || character > 126) return false;
            labels.Add(Encoding.ASCII.GetString(label));
            position += size;
        }
        return false;
    }
}
