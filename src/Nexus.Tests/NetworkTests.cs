using System.Buffers.Binary;
using Nexus.Network;
using Xunit;

namespace Nexus.Tests;

public sealed class NetworkTests
{
    [Fact]
    public void ValidatesAnswerAndTransaction()
    {
        var packet = Answer(DnsPacket.CreateQuery(123));
        Assert.True(DnsPacket.IsValidAnswer(packet, 123));
        Assert.False(DnsPacket.IsValidAnswer(packet, 124));
    }

    [Theory]
    [InlineData(2, 0x01)] // Query instead of response.
    [InlineData(2, 0x83)] // Truncated UDP answer.
    [InlineData(3, 0x83)] // NXDOMAIN.
    [InlineData(5, 0x02)] // Unexpected question count.
    [InlineData(13, 0x61)] // Different question.
    [InlineData(26, 0x1c)] // AAAA instead of A question.
    [InlineData(28, 0x03)] // Different class.
    public void RejectsWrongResponse(int offset, byte value)
    {
        var packet = Answer(DnsPacket.CreateQuery(1));
        packet[offset] = value;
        Assert.False(DnsPacket.IsValidAnswer(packet, 1));
    }

    [Fact]
    public void RejectsMalformedAndCyclicCompressedNames()
    {
        var packet = Answer(DnsPacket.CreateQuery(1));
        Assert.False(DnsPacket.IsValidAnswer(packet.AsSpan(0, 10), 1));
        Assert.False(DnsPacket.IsValidAnswer(packet.AsSpan(0, packet.Length - 1), 1));
        packet[29] = 0xc0;
        packet[30] = 29; // Name points to itself.
        Assert.False(DnsPacket.IsValidAnswer(packet, 1));
    }

    [Fact]
    public async Task MedianUsesOnlySuccessfulValidatedSamples()
    {
        var transport = new FakeTransport((query, number, _) => number switch
        {
            1 => new(Answer(query), 100),
            2 => new(new byte[4], 1),
            _ => new(Answer(query), 20)
        });
        var results = await new DnsBenchmark(transport, TimeSpan.FromSeconds(1))
            .MeasureAsync([new("Test", "127.0.0.1")]);
        var result = Assert.Single(results);
        Assert.Equal(2, result.SuccessfulSamples);
        Assert.Equal(60d, result.MedianMilliseconds);
        Assert.NotNull(result.Error);
        Assert.Equal(3, transport.Count);
    }

    [Fact]
    public async Task ThreeSuccessfulSamplesUseMiddleValue()
    {
        var transport = new FakeTransport((query, number, _) =>
            new(Answer(query), new double[] { 140, 10, 30 }[number - 1]));
        var result = Assert.Single(await new DnsBenchmark(transport, TimeSpan.FromSeconds(1))
            .MeasureAsync([new("Test", "127.0.0.1")]));
        Assert.Equal(3, result.SuccessfulSamples);
        Assert.Equal(30d, result.MedianMilliseconds);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task TimeoutProducesUnavailableInsteadOfFakeLatency()
    {
        var transport = new FakeTransport((_, _, _) => throw new OperationCanceledException());
        var result = Assert.Single(await new DnsBenchmark(transport, TimeSpan.FromSeconds(1))
            .MeasureAsync([new("Test", "127.0.0.1")]));
        Assert.Equal(0, result.SuccessfulSamples);
        Assert.Null(result.MedianMilliseconds);
        Assert.Equal("tempo esgotado", result.Error);
    }

    [Fact]
    public async Task UserCancellationStopsFurtherQueries()
    {
        using var cancellation = new CancellationTokenSource();
        var transport = new FakeTransport((_, _, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException();
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DnsBenchmark(transport, TimeSpan.FromSeconds(1))
                .MeasureAsync([new("Test", "127.0.0.1")], cancellation.Token));
        Assert.Equal(1, transport.Count);
    }

    private static byte[] Answer(byte[] query)
    {
        var packet = new byte[query.Length + 16];
        query.CopyTo(packet, 0);
        packet[2] = 0x81;
        packet[3] = 0x80;
        packet[7] = 1;
        var offset = query.Length;
        packet[offset] = 0xc0;
        packet[offset + 1] = 12;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(offset + 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(offset + 4), 1);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(offset + 10), 4);
        new byte[] { 93, 184, 216, 34 }.CopyTo(packet, offset + 12);
        return packet;
    }

    private sealed class FakeTransport(Func<byte[], int, CancellationToken, DnsExchange> exchange) : IDnsTransport
    {
        public int Count { get; private set; }
        public Task<DnsExchange> ExchangeAsync(DnsResolver resolver, byte[] query, TimeSpan timeout, CancellationToken token)
            => Task.FromResult(exchange(query, ++Count, token));
    }
}
