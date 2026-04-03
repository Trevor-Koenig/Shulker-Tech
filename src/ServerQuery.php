<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech;

class ServerQuery
{
    private const TIMEOUT = 3;

    /**
     * Query a Minecraft server using the 1.7+ Server List Ping protocol.
     *
     * @return array{
     *     online: bool,
     *     version: string,
     *     protocol: int,
     *     motd: string,
     *     players: array{online: int, max: int, names: string[]},
     *     favicon: string|null,
     *     latency: int
     * }|null  null on any connection or parse failure
     */
    public static function query(string $host, int $port = 25565): ?array
    {
        $start = microtime(true);

        $sock = @fsockopen($host, $port, $errno, $errstr, self::TIMEOUT);
        if ($sock === false) {
            return null;
        }

        stream_set_timeout($sock, self::TIMEOUT);

        try {
            // --- Handshake packet (0x00) ---
            $handshake = self::encodeVarInt(0x00)           // packet id
                . self::encodeVarInt(767)                   // protocol version (1.21)
                . self::encodeString($host)                 // server address
                . pack('n', $port)                          // server port (unsigned short BE)
                . self::encodeVarInt(1);                    // next state: status

            self::sendPacket($sock, $handshake);

            // --- Status request packet (0x00) ---
            self::sendPacket($sock, self::encodeVarInt(0x00));

            // --- Read status response ---
            $length = self::readVarInt($sock);
            if ($length <= 0) {
                return null;
            }

            $packetId = self::readVarInt($sock);
            if ($packetId !== 0x00) {
                return null;
            }

            $jsonLength = self::readVarInt($sock);
            $jsonRaw = self::readBytes($sock, $jsonLength);

            $latency = (int) round((microtime(true) - $start) * 1000);

            $data = json_decode($jsonRaw, true);
            if (!is_array($data)) {
                return null;
            }

            // --- Parse players sample ---
            $names = [];
            if (isset($data['players']['sample']) && is_array($data['players']['sample'])) {
                foreach ($data['players']['sample'] as $p) {
                    if (isset($p['name'])) {
                        $names[] = $p['name'];
                    }
                }
            }

            return [
                'online' => true,
                'version' => $data['version']['name'] ?? 'Unknown',
                'protocol' => $data['version']['protocol'] ?? 0,
                'motd' => self::formatMotd($data['description'] ?? ''),
                'players' => [
                    'online' => (int) ($data['players']['online'] ?? 0),
                    'max' => (int) ($data['players']['max'] ?? 0),
                    'names' => $names,
                ],
                'favicon' => $data['favicon'] ?? null,
                'latency' => $latency,
            ];
        } catch (\Throwable) {
            return null;
        } finally {
            fclose($sock);
        }
    }

    // -------------------------------------------------------------------------
    // Packet helpers
    // -------------------------------------------------------------------------

    /** Write a length-prefixed packet to the socket. */
    private static function sendPacket($sock, string $data): void
    {
        $packet = self::encodeVarInt(strlen($data)) . $data;
        fwrite($sock, $packet);
    }

    // -------------------------------------------------------------------------
    // VarInt encoding / decoding
    // -------------------------------------------------------------------------

    private static function encodeVarInt(int $value): string
    {
        $bytes = '';
        do {
            $byte = $value & 0x7F;
            $value = ($value >> 7) & 0x01FFFFFF; // unsigned right shift
            if ($value !== 0) {
                $byte |= 0x80;
            }
            $bytes .= chr($byte);
        } while ($value !== 0);
        return $bytes;
    }

    /** Read a VarInt from the socket (up to 5 bytes). */
    private static function readVarInt($sock): int
    {
        $value = 0;
        $position = 0;

        while (true) {
            $raw = fread($sock, 1);
            if ($raw === false || $raw === '') {
                throw new \RuntimeException('Connection closed while reading VarInt');
            }

            $byte = ord($raw);
            $value |= ($byte & 0x7F) << $position;
            $position += 7;

            if (($byte & 0x80) === 0) {
                break;
            }

            if ($position >= 35) {
                throw new \RuntimeException('VarInt too large');
            }
        }

        return $value;
    }

    // -------------------------------------------------------------------------
    // String encoding (VarInt length prefix + UTF-8 bytes)
    // -------------------------------------------------------------------------

    private static function encodeString(string $value): string
    {
        return self::encodeVarInt(strlen($value)) . $value;
    }

    // -------------------------------------------------------------------------
    // Buffered read
    // -------------------------------------------------------------------------

    private static function readBytes($sock, int $length): string
    {
        $data = '';
        while (strlen($data) < $length) {
            $chunk = fread($sock, $length - strlen($data));
            if ($chunk === false || $chunk === '') {
                throw new \RuntimeException('Connection closed while reading data');
            }
            $data .= $chunk;
        }
        return $data;
    }

    // -------------------------------------------------------------------------
    // MOTD helpers
    // -------------------------------------------------------------------------

    /** Minecraft colour code → CSS hex */
    private const COLOR_MAP = [
        '0' => '#000000',
        '1' => '#0000AA',
        '2' => '#00AA00',
        '3' => '#00AAAA',
        '4' => '#AA0000',
        '5' => '#AA00AA',
        '6' => '#FFAA00',
        '7' => '#AAAAAA',
        '8' => '#555555',
        '9' => '#5555FF',
        'a' => '#55FF55',
        'b' => '#55FFFF',
        'c' => '#FF5555',
        'd' => '#FF55FF',
        'e' => '#FFFF55',
        'f' => '#FFFFFF',
    ];

    /** Minecraft named colour (modern chat component) → CSS hex */
    private const NAMED_COLOR_MAP = [
        'black' => '#000000',
        'dark_blue' => '#0000AA',
        'dark_green' => '#00AA00',
        'dark_aqua' => '#00AAAA',
        'dark_red' => '#AA0000',
        'dark_purple' => '#AA00AA',
        'gold' => '#FFAA00',
        'gray' => '#AAAAAA',
        'dark_gray' => '#555555',
        'blue' => '#5555FF',
        'green' => '#55FF55',
        'aqua' => '#55FFFF',
        'red' => '#FF5555',
        'light_purple' => '#FF55FF',
        'yellow' => '#FFFF55',
        'white' => '#FFFFFF',
    ];

    /**
     * Convert a Minecraft MOTD (legacy string or modern chat component) to safe HTML.
     * Text content is escaped; only <span style="…"> tags are emitted.
     */
    private static function formatMotd(mixed $description): string
    {
        if (is_array($description)) {
            $html = self::formatChatComponent($description);
        } else {
            $html = self::formatLegacyString((string) $description);
        }
        // Final pass: convert any remaining newlines (real or literal \n) to <br>
        return str_replace(['\\n', "\n"], '<br>', $html);
    }

    /** Render a modern JSON chat component recursively. */
    private static function formatChatComponent(array $c): string
    {
        $styles = [];

        $color = $c['color'] ?? null;
        if ($color !== null) {
            $hex = self::NAMED_COLOR_MAP[strtolower($color)]
                ?? (preg_match('/^#[0-9a-f]{6}$/i', $color) ? $color : null);
            if ($hex) {
                $styles[] = 'color:' . $hex;
            }
        }
        if (!empty($c['bold']))
            $styles[] = 'font-weight:bold';
        if (!empty($c['italic']))
            $styles[] = 'font-style:italic';
        if (!empty($c['underlined']))
            $styles[] = 'text-decoration:underline';
        if (!empty($c['strikethrough']))
            $styles[] = 'text-decoration:line-through';

        $rawText = str_replace(['\\n', "\r\n", "\r"], ["\n", "\n", "\n"], $c['text'] ?? '');
        $inner = implode('<br>', array_map(
            fn($l) => htmlspecialchars($l, ENT_QUOTES),
            explode("\n", $rawText)
        ));

        if (isset($c['extra']) && is_array($c['extra'])) {
            foreach ($c['extra'] as $part) {
                $inner .= is_array($part)
                    ? self::formatChatComponent($part)
                    : htmlspecialchars((string) $part, ENT_QUOTES);
            }
        }

        return $styles
            ? '<span style="' . implode(';', $styles) . '">' . $inner . '</span>'
            : $inner;
    }

    /** Convert a legacy § colour-coded string to HTML. */
    private static function formatLegacyString(string $text): string
    {
        // Normalise both real newlines and literal \n sequences
        $text = str_replace(['\\n', "\r\n", "\r"], ["\n", "\n", "\n"], $text);

        // Split on § + one char, keeping the delimiter
        $parts = preg_split('/(§[0-9a-fk-or])/ui', $text, -1, PREG_SPLIT_DELIM_CAPTURE);

        $html = '';
        $color = null;
        $bold = false;
        $italic = false;
        $underline = false;
        $strike = false;

        foreach ($parts as $part) {
            if (preg_match('/^§([0-9a-fk-or])$/ui', $part, $m)) {
                $code = strtolower($m[1]);

                if ($code === 'r') {
                    $color = null;
                    $bold = $italic = $underline = $strike = false;
                } elseif (isset(self::COLOR_MAP[$code])) {
                    $color = self::COLOR_MAP[$code];
                    $bold = $italic = $underline = $strike = false;
                } elseif ($code === 'l') {
                    $bold = true;
                } elseif ($code === 'o') {
                    $italic = true;
                } elseif ($code === 'n') {
                    $underline = true;
                } elseif ($code === 'm') {
                    $strike = true;
                }
                // §k (obfuscated) — skip
                continue;
            }

            if ($part === '')
                continue;

            // Split on literal \n within the segment so line breaks become <br>
            $lines = explode("\n", $part);
            $escaped = implode('<br>', array_map(fn($l) => htmlspecialchars($l, ENT_QUOTES), $lines));
            $styles = [];

            if ($color)
                $styles[] = 'color:' . $color;
            if ($bold)
                $styles[] = 'font-weight:bold';
            if ($italic)
                $styles[] = 'font-style:italic';
            if ($underline)
                $styles[] = 'text-decoration:underline';
            if ($strike)
                $styles[] = 'text-decoration:line-through';

            $html .= $styles
                ? '<span style="' . implode(';', $styles) . '">' . $escaped . '</span>'
                : $escaped;
        }

        return $html;
    }
}
