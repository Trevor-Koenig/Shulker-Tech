using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using ShulkerTech.Core.Services;

namespace ShulkerTech.Tests.Services;

/// <summary>
/// Tests for MinecraftPingService.PingAsync using real TCP listeners.
/// A short timeout (500 ms) is injected so hung-connection tests finish quickly.
/// </summary>
[Trait("Category", "Unit")]
public class MinecraftPingServiceTcpTests
{
    // Short timeout keeps hung-server tests fast; well above realistic CI overhead.
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMilliseconds(500);
    private static MinecraftPingService Sut() => new(TestTimeout);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// Starts a loopback TcpListener on an OS-assigned port and returns (listener, port).
    private static (TcpListener Listener, int Port) StartListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return (listener, port);
    }

    /// Encodes a VarInt into a byte list (mirrors the service implementation).
    private static void WriteVarInt(List<byte> buf, int value)
    {
        uint v = (uint)value;
        while (true)
        {
            if ((v & ~0x7Fu) == 0) { buf.Add((byte)v); return; }
            buf.Add((byte)((v & 0x7F) | 0x80));
            v >>= 7;
        }
    }

    /// Builds a well-formed Minecraft status response packet for the given JSON payload.
    private static byte[] BuildStatusResponse(string json)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        var body = new List<byte>();
        WriteVarInt(body, 0x00);           // packet ID
        WriteVarInt(body, jsonBytes.Length); // string length prefix
        body.AddRange(jsonBytes);

        var packet = new List<byte>();
        WriteVarInt(packet, body.Count);   // packet length prefix
        packet.AddRange(body);

        return [.. packet];
    }

    // ── Connection-level failures ─────────────────────────────────────────────

    [Fact]
    public async Task PingAsync_PortNotListening_ReturnsOffline()
    {
        // Pick a free port then immediately release it so nothing is listening.
        var (listener, port) = StartListener();
        listener.Stop();

        var result = await Sut().PingAsync("127.0.0.1", port);

        result.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task PingAsync_ServerHangsAfterConnect_ReturnsOfflineWithinDeadline()
    {
        var (listener, port) = StartListener();

        // Accept the connection but never send any data.
        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await Task.Delay(TimeSpan.FromSeconds(30)); // outlasts test timeout
        });

        var sw = Stopwatch.StartNew();
        var result = await Sut().PingAsync("127.0.0.1", port);
        sw.Stop();

        listener.Stop();

        result.IsOnline.Should().BeFalse();
        // Should complete shortly after the stream ReadTimeout fires (TestTimeout + buffer)
        sw.Elapsed.Should().BeLessThan(TestTimeout + TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task PingAsync_ServerSendsGarbageBytes_ReturnsOffline()
    {
        var (listener, port) = StartListener();

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var garbage = new byte[] { 0xFF, 0xFE, 0x00, 0x01, 0xAB, 0xCD };
            await stream.WriteAsync(garbage);
        });

        var result = await Sut().PingAsync("127.0.0.1", port);
        listener.Stop();

        result.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task PingAsync_ServerClosesConnectionImmediately_ReturnsOffline()
    {
        var (listener, port) = StartListener();

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            // Close without sending anything — causes EndOfStreamException on read
        });

        var result = await Sut().PingAsync("127.0.0.1", port);
        listener.Stop();

        result.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task PingAsync_ServerSendsTruncatedPacket_ReturnsOffline()
    {
        var (listener, port) = StartListener();

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            // Claim a 1000-byte packet, but only send 5 bytes
            var truncated = new List<byte>();
            WriteVarInt(truncated, 1000);
            truncated.AddRange(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });
            await stream.WriteAsync(truncated.ToArray().AsMemory());
            // Then close — read loop will get 0 bytes → EndOfStreamException
        });

        var result = await Sut().PingAsync("127.0.0.1", port);
        listener.Stop();

        result.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task PingAsync_ServerSendsWrongPacketId_ReturnsOffline()
    {
        var (listener, port) = StartListener();

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();

            // Valid length/encoding but wrong packet ID (0x01 instead of 0x00)
            var body = new List<byte>();
            WriteVarInt(body, 0x01); // wrong packet ID

            var packet = new List<byte>();
            WriteVarInt(packet, body.Count);
            packet.AddRange(body);

            await stream.WriteAsync(packet.ToArray().AsMemory());
        });

        var result = await Sut().PingAsync("127.0.0.1", port);
        listener.Stop();

        result.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task PingAsync_ServerSendsOversizedStringLength_ReturnsOffline()
    {
        var (listener, port) = StartListener();

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();

            // Packet claims a string of 200 KB — above our 64 KB sanity limit.
            var body = new List<byte>();
            WriteVarInt(body, 0x00);       // correct packet ID
            WriteVarInt(body, 200 * 1024); // oversized string length

            var packet = new List<byte>();
            WriteVarInt(packet, body.Count + 1); // fake large packet length
            packet.AddRange(body);

            await stream.WriteAsync(packet.ToArray().AsMemory());
        });

        var result = await Sut().PingAsync("127.0.0.1", port);
        listener.Stop();

        result.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task PingAsync_ValidResponse_ReturnsOnlineWithCorrectData()
    {
        var (listener, port) = StartListener();

        var json = """{"players":{"online":5,"max":20},"description":"Test Server"}""";
        var responseBytes = BuildStatusResponse(json);

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            // Drain whatever the client sends (handshake + status request), then reply
            var drain = new byte[512];
            _ = await stream.ReadAsync(drain.AsMemory());
            await stream.WriteAsync(responseBytes.AsMemory());
        });

        var result = await Sut().PingAsync("127.0.0.1", port);
        listener.Stop();

        result.IsOnline.Should().BeTrue();
        result.PlayersOnline.Should().Be(5);
        result.PlayersMax.Should().Be(20);
    }
}
