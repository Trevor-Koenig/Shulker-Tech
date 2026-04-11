using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShulkerTech.Core.Services;

public class MinecraftPingService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<ServerPingResult> PingAsync(string host, int port, CancellationToken ct = default)
    {
        try
        {
            using var tcp = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(Timeout);

            await tcp.ConnectAsync(host, port, timeoutCts.Token);

            using var stream = tcp.GetStream();

            // ── Handshake packet ──────────────────────────────────────────────
            var handshake = new List<byte>();
            WriteVarInt(handshake, 0x00);          // Packet ID
            WriteVarInt(handshake, 0);             // Protocol version (0 = status)
            WriteString(handshake, host);          // Server address
            handshake.Add((byte)(port >> 8));      // Port high byte
            handshake.Add((byte)(port & 0xFF));    // Port low byte
            WriteVarInt(handshake, 1);             // Next state: status

            await WritePacketAsync(stream, handshake, timeoutCts.Token);

            // ── Status request packet ─────────────────────────────────────────
            var statusReq = new List<byte>();
            WriteVarInt(statusReq, 0x00);
            await WritePacketAsync(stream, statusReq, timeoutCts.Token);

            // ── Status response packet ────────────────────────────────────────
            var packetLength = ReadVarInt(stream);
            var packetId = ReadVarInt(stream);
            if (packetId != 0x00)
                return ServerPingResult.Offline;

            var json = ReadString(stream);
            return ParseResponse(json);
        }
        catch
        {
            return ServerPingResult.Offline;
        }
    }

    private static ServerPingResult ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var playersOnline = 0;
            var playersMax = 0;
            if (root.TryGetProperty("players", out var players))
            {
                players.TryGetProperty("online", out var onlineEl);
                players.TryGetProperty("max", out var maxEl);
                playersOnline = onlineEl.ValueKind == JsonValueKind.Number ? onlineEl.GetInt32() : 0;
                playersMax = maxEl.ValueKind == JsonValueKind.Number ? maxEl.GetInt32() : 0;
            }

            string? motd = null;
            if (root.TryGetProperty("description", out var desc))
            {
                motd = desc.ValueKind == JsonValueKind.String
                    ? desc.GetString()
                    : desc.TryGetProperty("text", out var text) ? text.GetString() : null;

                motd = StripFormattingCodes(motd);
            }

            string? favicon = null;
            if (root.TryGetProperty("favicon", out var fav) && fav.ValueKind == JsonValueKind.String)
                favicon = fav.GetString();

            return new ServerPingResult(true, playersOnline, playersMax, motd, favicon);
        }
        catch
        {
            return ServerPingResult.Offline;
        }
    }

    private static string? StripFormattingCodes(string? s) =>
        s is null ? null : Regex.Replace(s, @"§[0-9a-fk-or]", "", RegexOptions.IgnoreCase);

    // ── Wire protocol helpers ─────────────────────────────────────────────────

    private static async Task WritePacketAsync(NetworkStream stream, List<byte> data, CancellationToken ct)
    {
        var lengthBuf = new List<byte>();
        WriteVarInt(lengthBuf, data.Count);
        await stream.WriteAsync(lengthBuf.ToArray().AsMemory(), ct);
        await stream.WriteAsync(data.ToArray().AsMemory(), ct);
    }

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

    private static void WriteString(List<byte> buf, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(buf, bytes.Length);
        buf.AddRange(bytes);
    }

    private static int ReadVarInt(NetworkStream stream)
    {
        int result = 0, shift = 0;
        while (true)
        {
            int b = stream.ReadByte();
            if (b == -1) throw new EndOfStreamException();
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
            if (shift >= 32) throw new IOException("VarInt too large");
        }
    }

    private static string ReadString(NetworkStream stream)
    {
        int length = ReadVarInt(stream);
        var buf = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = stream.Read(buf, read, length - read);
            if (n == 0) throw new EndOfStreamException();
            read += n;
        }
        return Encoding.UTF8.GetString(buf);
    }
}

public record ServerPingResult(
    bool IsOnline,
    int PlayersOnline,
    int PlayersMax,
    string? Motd,
    string? FaviconDataUrl)
{
    public static readonly ServerPingResult Offline = new(false, 0, 0, null, null);
}
