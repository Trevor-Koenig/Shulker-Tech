using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShulkerTech.Core.Services;

public class MinecraftPingService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public virtual async Task<ServerPingResult> PingAsync(string host, int port, CancellationToken ct = default)
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

    internal static ServerPingResult ParseResponse(string json)
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

            // Player sample — best effort; malformed entries are skipped
            var onlinePlayers = new List<OnlinePlayer>();
            try
            {
                if (root.TryGetProperty("players", out var playersEl) &&
                    playersEl.TryGetProperty("sample", out var sample) &&
                    sample.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in sample.EnumerateArray())
                    {
                        try
                        {
                            var name = p.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                                ? nameEl.GetString() : null;
                            var id = p.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                                ? idEl.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(name))
                                onlinePlayers.Add(new OnlinePlayer(name, id));
                        }
                        catch { /* skip malformed player entry */ }
                    }
                }
            }
            catch { /* sample block failed entirely — no player faces */ }

            // MOTD rendering — best effort; failure leaves it null (no MOTD shown)
            string? motdHtml = null;
            try
            {
                if (root.TryGetProperty("description", out var desc))
                    motdHtml = RenderMotdHtml(desc);
            }
            catch { /* MOTD rendering failed — leave null */ }

            string? favicon = null;
            if (root.TryGetProperty("favicon", out var fav) && fav.ValueKind == JsonValueKind.String)
                favicon = fav.GetString();

            return new ServerPingResult(true, playersOnline, playersMax, motdHtml, favicon, onlinePlayers);
        }
        catch
        {
            return ServerPingResult.Offline;
        }
    }

    // ── MOTD rendering ────────────────────────────────────────────────────────

    private static readonly Dictionary<char, string> LegacyColorMap = new()
    {
        ['0'] = "#000000", ['1'] = "#0000AA", ['2'] = "#00AA00", ['3'] = "#00AAAA",
        ['4'] = "#AA0000", ['5'] = "#AA00AA", ['6'] = "#FFAA00", ['7'] = "#AAAAAA",
        ['8'] = "#555555", ['9'] = "#5555FF", ['a'] = "#55FF55", ['b'] = "#55FFFF",
        ['c'] = "#FF5555", ['d'] = "#FF55FF", ['e'] = "#FFFF55", ['f'] = "#FFFFFF",
    };

    private static readonly Dictionary<string, string> NamedColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000",   ["dark_blue"] = "#0000AA",   ["dark_green"] = "#00AA00",
        ["dark_aqua"] = "#00AAAA", ["dark_red"] = "#AA0000", ["dark_purple"] = "#AA00AA",
        ["gold"] = "#FFAA00",    ["gray"] = "#AAAAAA",        ["dark_gray"] = "#555555",
        ["blue"] = "#5555FF",    ["green"] = "#55FF55",        ["aqua"] = "#55FFFF",
        ["red"] = "#FF5555",     ["light_purple"] = "#FF55FF", ["yellow"] = "#FFFF55",
        ["white"] = "#FFFFFF",
    };

    private static string RenderMotdHtml(JsonElement desc)
    {
        if (desc.ValueKind == JsonValueKind.String)
            return FormatLegacyMotd(desc.GetString() ?? "");

        if (desc.ValueKind == JsonValueKind.Object)
            return RenderChatComponent(desc);

        return "";
    }

    private static string RenderChatComponent(JsonElement el)
    {
        var style = new MotdStyle();

        if (el.TryGetProperty("color", out var colorEl) && colorEl.ValueKind == JsonValueKind.String && colorEl.GetString() is { } colorStr)
        {
            // 1.16+ supports "#RRGGBB" directly; older uses named colors
            style = style with { Color = colorStr.StartsWith('#') ? colorStr : NamedColorMap.GetValueOrDefault(colorStr) };
        }
        if (el.TryGetProperty("bold", out var boldEl) && boldEl.ValueKind == JsonValueKind.True)
            style = style with { Bold = true };
        if (el.TryGetProperty("italic", out var italicEl) && italicEl.ValueKind == JsonValueKind.True)
            style = style with { Italic = true };
        if (el.TryGetProperty("underlined", out var ulEl) && ulEl.ValueKind == JsonValueKind.True)
            style = style with { Underline = true };
        if (el.TryGetProperty("strikethrough", out var stEl) && stEl.ValueKind == JsonValueKind.True)
            style = style with { Strikethrough = true };

        var sb = new StringBuilder();
        var styleStr = style.ToCssString();
        if (styleStr.Length > 0)
            sb.Append($"<span style=\"{styleStr}\">");

        if (el.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            sb.Append(FormatLegacyMotd(textEl.GetString() ?? ""));

        if (el.TryGetProperty("extra", out var extraEl) && extraEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in extraEl.EnumerateArray())
            {
                // extra entries can be plain strings as well as component objects
                if (child.ValueKind == JsonValueKind.String)
                    sb.Append(FormatLegacyMotd(child.GetString() ?? ""));
                else if (child.ValueKind == JsonValueKind.Object)
                    sb.Append(RenderChatComponent(child));
            }
        }

        if (styleStr.Length > 0)
            sb.Append("</span>");

        return sb.ToString();
    }

    /// <summary>Converts a string with § formatting codes to HTML spans.</summary>
    private static string FormatLegacyMotd(string text)
    {
        var runs = new List<(MotdStyle style, string text)>();
        var currentStyle = new MotdStyle();
        var currentText = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '§' && i + 1 < text.Length)
            {
                if (currentText.Length > 0)
                {
                    runs.Add((currentStyle, currentText.ToString()));
                    currentText.Clear();
                }

                char code = char.ToLower(text[i + 1]);
                i++; // skip the code character

                currentStyle = code switch
                {
                    // §r resets everything
                    'r' => new MotdStyle(),
                    // Format codes accumulate on top of current color
                    'l' => currentStyle with { Bold = true },
                    'o' => currentStyle with { Italic = true },
                    'n' => currentStyle with { Underline = true },
                    'm' => currentStyle with { Strikethrough = true },
                    // Color codes reset all active formatting (matches Minecraft client behavior)
                    _ when LegacyColorMap.TryGetValue(code, out var hex) => new MotdStyle(Color: hex),
                    _ => currentStyle,
                };
                continue;
            }

            if (text[i] == '\n')
            {
                if (currentText.Length > 0)
                {
                    runs.Add((currentStyle, currentText.ToString()));
                    currentText.Clear();
                }
                runs.Add((new MotdStyle(), "\n"));
                continue;
            }

            // HTML-escape
            currentText.Append(text[i] switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                _ => text[i].ToString(),
            });
        }

        if (currentText.Length > 0)
            runs.Add((currentStyle, currentText.ToString()));

        var sb = new StringBuilder();
        foreach (var (style, runText) in runs)
        {
            if (runText == "\n") { sb.Append("<br>"); continue; }
            var css = style.ToCssString();
            if (css.Length > 0)
                sb.Append($"<span style=\"{css}\">{runText}</span>");
            else
                sb.Append(runText);
        }
        return sb.ToString();
    }

    private record MotdStyle(string? Color = null, bool Bold = false, bool Italic = false,
        bool Underline = false, bool Strikethrough = false)
    {
        public string ToCssString()
        {
            var parts = new List<string>(5);
            if (Color is not null) parts.Add($"color:{Color}");
            if (Bold) parts.Add("font-weight:bold");
            if (Italic) parts.Add("font-style:italic");
            var dec = new List<string>(2);
            if (Underline) dec.Add("underline");
            if (Strikethrough) dec.Add("line-through");
            if (dec.Count > 0) parts.Add($"text-decoration:{string.Join(' ', dec)}");
            return string.Join(';', parts);
        }
    }

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

public record OnlinePlayer(string Name, string? Uuid)
{
    /// <summary>Mineatar face avatar URL (8px * scale=5 → 40px). Falls back to Steve UUID if missing.</summary>
    public string AvatarUrl =>
        $"https://api.mineatar.io/face/{NormalizeUuid(Uuid ?? "00000000-0000-0000-0000-000000000000")}?scale=5";

    private static string NormalizeUuid(string uuid) =>
        uuid.Length == 32
            ? $"{uuid[..8]}-{uuid[8..12]}-{uuid[12..16]}-{uuid[16..20]}-{uuid[20..]}"
            : uuid;
}

public record ServerPingResult(
    bool IsOnline,
    int PlayersOnline,
    int PlayersMax,
    string? MotdHtml,
    string? FaviconDataUrl,
    IReadOnlyList<OnlinePlayer>? OnlinePlayers = null)
{
    public static readonly ServerPingResult Offline = new(false, 0, 0, null, null, null);
}
