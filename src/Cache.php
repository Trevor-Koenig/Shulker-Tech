<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech;

class Cache
{
    private static function dir(): string
    {
        return rtrim($_ENV['CACHE_DIR'] ?? '/var/www/cache', '/');
    }

    private static function path(string $key): string
    {
        return self::dir() . '/' . sha1($key) . '.json';
    }

    public static function get(string $key): mixed
    {
        $path = self::path($key);

        if (!file_exists($path)) {
            return null;
        }

        $raw = file_get_contents($path);
        if ($raw === false) {
            return null;
        }

        $entry = json_decode($raw, true);
        if (!is_array($entry) || $entry['expires'] < time()) {
            self::delete($key);
            return null;
        }

        return $entry['data'];
    }

    public static function set(string $key, mixed $value, int $ttl = 60): void
    {
        $dir = self::dir();
        if (!is_dir($dir)) {
            mkdir($dir, 0755, true);
        }

        file_put_contents(
            self::path($key),
            json_encode(['expires' => time() + $ttl, 'data' => $value]),
            LOCK_EX
        );
    }

    public static function delete(string $key): void
    {
        $path = self::path($key);
        if (file_exists($path)) {
            unlink($path);
        }
    }

    public static function flush(): void
    {
        foreach (glob(self::dir() . '/*.json') ?: [] as $file) {
            unlink($file);
        }
    }
}
