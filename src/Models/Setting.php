<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech\Models;

use Trevor\ShulkerTech\Database;

class Setting
{
    /** In-memory cache so we only hit the DB once per request. */
    private static ?array $cache = null;

    public static function get(string $key, string $default = ''): string
    {
        self::loadAll();
        return self::$cache[$key]['value'] ?? $default;
    }

    public static function set(string $key, string $value): void
    {
        $pdo  = Database::getConnection();
        $stmt = $pdo->prepare(
            'UPDATE `settings` SET `value` = ? WHERE `key` = ?'
        );
        $stmt->execute([$value, $key]);

        // Bust the in-memory cache
        if (self::$cache !== null && isset(self::$cache[$key])) {
            self::$cache[$key]['value'] = $value;
        }
    }

    public static function all(): array
    {
        self::loadAll();
        return array_values(self::$cache ?? []);
    }

    private static function loadAll(): void
    {
        if (self::$cache !== null) {
            return;
        }

        $rows        = Database::getConnection()
            ->query('SELECT `key`, `value`, `label`, `description` FROM `settings` ORDER BY `label` ASC')
            ->fetchAll();
        self::$cache = [];

        foreach ($rows as $row) {
            self::$cache[$row['key']] = $row;
        }
    }
}
