<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech\Models;

use Trevor\ShulkerTech\Database;

class Server
{
    private static array $fillable = [
        'name', 'slug', 'description', 'host', 'port', 'display_order', 'is_active',
    ];

    public static function all(): array
    {
        $pdo = Database::getConnection();
        return $pdo->query('SELECT * FROM `servers` ORDER BY `display_order` ASC, `name` ASC')
                   ->fetchAll();
    }

    public static function allActive(): array
    {
        $pdo  = Database::getConnection();
        $stmt = $pdo->prepare('SELECT * FROM `servers` WHERE `is_active` = 1 ORDER BY `display_order` ASC, `name` ASC');
        $stmt->execute();
        return $stmt->fetchAll();
    }

    public static function findById(int $id): ?array
    {
        $pdo  = Database::getConnection();
        $stmt = $pdo->prepare('SELECT * FROM `servers` WHERE `id` = ?');
        $stmt->execute([$id]);
        $row = $stmt->fetch();
        return $row ?: null;
    }

    public static function findBySlug(string $slug): ?array
    {
        $pdo  = Database::getConnection();
        $stmt = $pdo->prepare('SELECT * FROM `servers` WHERE `slug` = ?');
        $stmt->execute([$slug]);
        $row = $stmt->fetch();
        return $row ?: null;
    }

    public static function create(array $data): int
    {
        $data = self::filter($data);
        $pdo  = Database::getConnection();

        $cols        = implode(', ', array_map(fn($k) => "`{$k}`", array_keys($data)));
        $placeholders = implode(', ', array_fill(0, count($data), '?'));

        $stmt = $pdo->prepare("INSERT INTO `servers` ({$cols}) VALUES ({$placeholders})");
        $stmt->execute(array_values($data));
        return (int) $pdo->lastInsertId();
    }

    public static function update(int $id, array $data): void
    {
        $data = self::filter($data);
        $pdo  = Database::getConnection();

        $sets = implode(', ', array_map(fn($k) => "`{$k}` = ?", array_keys($data)));
        $stmt = $pdo->prepare("UPDATE `servers` SET {$sets} WHERE `id` = ?");
        $stmt->execute([...array_values($data), $id]);
    }

    public static function delete(int $id): void
    {
        $pdo  = Database::getConnection();
        $stmt = $pdo->prepare('DELETE FROM `servers` WHERE `id` = ?');
        $stmt->execute([$id]);
    }

    public static function count(): int
    {
        return (int) Database::getConnection()->query('SELECT COUNT(*) FROM `servers`')->fetchColumn();
    }

    private static function filter(array $data): array
    {
        return array_intersect_key($data, array_flip(self::$fillable));
    }
}
