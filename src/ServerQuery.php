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
                'motd' => self::stripFormatting($data['description'] ?? ''),
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

    /**
     * Accept either a plain string description or the legacy/modern JSON object
     * {"text":"..."} or {"extra":[...]} form and return a clean plain-text string.
     * Strips § colour/formatting codes.
     */
    private static function stripFormatting(mixed $description): string
    {
        if (is_array($description)) {
            // Modern chat component
            $text = $description['text'] ?? '';
            if (isset($description['extra']) && is_array($description['extra'])) {
                foreach ($description['extra'] as $part) {
                    $text .= is_array($part) ? ($part['text'] ?? '') : (string) $part;
                }
            }
        } else {
            $text = (string) $description;
        }

        // Strip legacy § colour codes (§ followed by any single char)
        return preg_replace('/§[0-9a-fk-or]/i', '', $text) ?? $text;
    }
}
