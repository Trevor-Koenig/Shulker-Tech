<?php

namespace App\Services;

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
            // Handshake packet (0x00)
            $handshake = self::encodeVarInt(0x00)
                . self::encodeVarInt(767)
                . self::encodeString($host)
                . pack('n', $port)
                . self::encodeVarInt(1);
            self::sendPacket($sock, $handshake);

            // Status request packet (0x00)
            self::sendPacket($sock, self::encodeVarInt(0x00));

            // Read status response
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

            $names = [];
            if (isset($data['players']['sample']) && is_array($data['players']['sample'])) {
                foreach ($data['players']['sample'] as $p) {
                    if (isset($p['name'])) {
                        $names[] = $p['name'];
                    }
                }
            }

            return [
                'online'   => true,
                'version'  => $data['version']['name'] ?? 'Unknown',
                'protocol' => $data['version']['protocol'] ?? 0,
                'motd'     => self::formatMotd($data['description'] ?? ''),
                'players'  => [
                    'online' => (int) ($data['players']['online'] ?? 0),
                    'max'    => (int) ($data['players']['max'] ?? 0),
                    'names'  => $names,
                ],
                'favicon'  => $data['favicon'] ?? null,
                'latency'  => $latency,
            ];
        } catch (\Throwable) {
            return null;
        } finally {
            fclose($sock);
        }
    }

    private static function sendPacket($sock, string $data): void
    {
        fwrite($sock, self::encodeVarInt(strlen($data)) . $data);
    }

    private static function encodeVarInt(int $value): string
    {
        $bytes = '';
        do {
            $byte = $value & 0x7F;
            $value = ($value >> 7) & 0x01FFFFFF;
            if ($value !== 0) {
                $byte |= 0x80;
            }
            $bytes .= chr($byte);
        } while ($value !== 0);
        return $bytes;
    }

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

    private static function encodeString(string $value): string
    {
        return self::encodeVarInt(strlen($value)) . $value;
    }

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

    private const COLOR_MAP = [
        '0' => '#000000', '1' => '#0000AA', '2' => '#00AA00', '3' => '#00AAAA',
        '4' => '#AA0000', '5' => '#AA00AA', '6' => '#FFAA00', '7' => '#AAAAAA',
        '8' => '#555555', '9' => '#5555FF', 'a' => '#55FF55', 'b' => '#55FFFF',
        'c' => '#FF5555', 'd' => '#FF55FF', 'e' => '#FFFF55', 'f' => '#FFFFFF',
    ];

    private const NAMED_COLOR_MAP = [
        'black' => '#000000', 'dark_blue' => '#0000AA', 'dark_green' => '#00AA00',
        'dark_aqua' => '#00AAAA', 'dark_red' => '#AA0000', 'dark_purple' => '#AA00AA',
        'gold' => '#FFAA00', 'gray' => '#AAAAAA', 'dark_gray' => '#555555',
        'blue' => '#5555FF', 'green' => '#55FF55', 'aqua' => '#55FFFF',
        'red' => '#FF5555', 'light_purple' => '#FF55FF', 'yellow' => '#FFFF55',
        'white' => '#FFFFFF',
    ];

    private static function formatMotd(mixed $description): string
    {
        $html = is_array($description)
            ? self::formatChatComponent($description)
            : self::formatLegacyString((string) $description);
        return str_replace(['\\n', "\n"], '<br>', $html);
    }

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
        if (!empty($c['bold']))        $styles[] = 'font-weight:bold';
        if (!empty($c['italic']))      $styles[] = 'font-style:italic';
        if (!empty($c['underlined']))  $styles[] = 'text-decoration:underline';
        if (!empty($c['strikethrough'])) $styles[] = 'text-decoration:line-through';

        $rawText = str_replace(['\\n', "\r\n", "\r"], ["\n", "\n", "\n"], $c['text'] ?? '');
        $inner = implode('<br>', array_map(fn($l) => htmlspecialchars($l, ENT_QUOTES), explode("\n", $rawText)));

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

    private static function formatLegacyString(string $text): string
    {
        $text = str_replace(['\\n', "\r\n", "\r"], ["\n", "\n", "\n"], $text);
        $parts = preg_split('/(§[0-9a-fk-or])/ui', $text, -1, PREG_SPLIT_DELIM_CAPTURE);

        $html = '';
        $color = null;
        $bold = $italic = $underline = $strike = false;

        foreach ($parts as $part) {
            if (preg_match('/^§([0-9a-fk-or])$/ui', $part, $m)) {
                $code = strtolower($m[1]);
                if ($code === 'r') {
                    $color = null;
                    $bold = $italic = $underline = $strike = false;
                } elseif (isset(self::COLOR_MAP[$code])) {
                    $color = self::COLOR_MAP[$code];
                    $bold = $italic = $underline = $strike = false;
                } elseif ($code === 'l') { $bold = true; }
                elseif ($code === 'o') { $italic = true; }
                elseif ($code === 'n') { $underline = true; }
                elseif ($code === 'm') { $strike = true; }
                continue;
            }
            if ($part === '') continue;

            $lines = explode("\n", $part);
            $escaped = implode('<br>', array_map(fn($l) => htmlspecialchars($l, ENT_QUOTES), $lines));
            $styles = [];
            if ($color)    $styles[] = 'color:' . $color;
            if ($bold)     $styles[] = 'font-weight:bold';
            if ($italic)   $styles[] = 'font-style:italic';
            if ($underline) $styles[] = 'text-decoration:underline';
            if ($strike)   $styles[] = 'text-decoration:line-through';

            $html .= $styles
                ? '<span style="' . implode(';', $styles) . '">' . $escaped . '</span>'
                : $escaped;
        }
        return $html;
    }
}
