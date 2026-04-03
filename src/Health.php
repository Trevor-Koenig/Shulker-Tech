<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech;

class Health
{
    /**
     * Check if a URL's host is reachable by attempting a TCP connection.
     * Uses a short timeout so a down service doesn't slow page loads.
     */
    public static function isReachable(string $url, int $timeoutSeconds = 2): bool
    {
        $parts = parse_url($url);
        if (empty($parts['host'])) {
            return false;
        }

        $host = $parts['host'];
        $port = $parts['port'] ?? ($parts['scheme'] === 'https' ? 443 : 80);

        $socket = @fsockopen($host, $port, $errno, $errstr, $timeoutSeconds);

        if ($socket !== false) {
            fclose($socket);
            return true;
        }

        return false;
    }
}
