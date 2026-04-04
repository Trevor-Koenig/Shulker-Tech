<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech\Models;

use Trevor\ShulkerTech\Database;

class User
{
    private static array $fillable = ['username', 'email', 'password_hash', 'is_active'];

    public static function all(): array
    {
        return Database::getConnection()
            ->query('SELECT `id`, `username`, `email`, `is_active`, `created_at` FROM `users` ORDER BY `username` ASC')
            ->fetchAll();
    }

    public static function findById(int $id): ?array
    {
        $stmt = Database::getConnection()->prepare(
            'SELECT `id`, `username`, `email`, `password_hash`, `is_active`, `created_at` FROM `users` WHERE `id` = ?'
        );
        $stmt->execute([$id]);
        $row = $stmt->fetch();
        return $row ?: null;
    }

    public static function findByEmail(string $email): ?array
    {
        $stmt = Database::getConnection()->prepare(
            'SELECT * FROM `users` WHERE `email` = ?'
        );
        $stmt->execute([$email]);
        $row = $stmt->fetch();
        return $row ?: null;
    }

    public static function findByUsername(string $username): ?array
    {
        $stmt = Database::getConnection()->prepare(
            'SELECT * FROM `users` WHERE `username` = ?'
        );
        $stmt->execute([$username]);
        $row = $stmt->fetch();
        return $row ?: null;
    }

    public static function create(array $data): int
    {
        $data = self::filter($data);
        $pdo  = Database::getConnection();

        $cols         = implode(', ', array_map(fn($k) => "`{$k}`", array_keys($data)));
        $placeholders = implode(', ', array_fill(0, count($data), '?'));

        $stmt = $pdo->prepare("INSERT INTO `users` ({$cols}) VALUES ({$placeholders})");
        $stmt->execute(array_values($data));
        return (int) $pdo->lastInsertId();
    }

    public static function update(int $id, array $data): void
    {
        $data = self::filter($data);
        if (empty($data)) {
            return;
        }

        $pdo  = Database::getConnection();
        $sets = implode(', ', array_map(fn($k) => "`{$k}` = ?", array_keys($data)));
        $stmt = $pdo->prepare("UPDATE `users` SET {$sets} WHERE `id` = ?");
        $stmt->execute([...array_values($data), $id]);
    }

    public static function delete(int $id): void
    {
        $stmt = Database::getConnection()->prepare('DELETE FROM `users` WHERE `id` = ?');
        $stmt->execute([$id]);
    }

    public static function count(): int
    {
        return (int) Database::getConnection()->query('SELECT COUNT(*) FROM `users`')->fetchColumn();
    }

    public static function hashPassword(string $plain): string
    {
        return password_hash($plain, PASSWORD_BCRYPT);
    }

    public static function verifyPassword(string $plain, string $hash): bool
    {
        return password_verify($plain, $hash);
    }

    /** Returns role rows for a user. */
    public static function getRoles(int $userId): array
    {
        $stmt = Database::getConnection()->prepare(
            'SELECT r.*
             FROM `roles` r
             JOIN `user_roles` ur ON ur.`role_id` = r.`id`
             WHERE ur.`user_id` = ?
             ORDER BY r.`name` ASC'
        );
        $stmt->execute([$userId]);
        return $stmt->fetchAll();
    }

    /** Returns a flat array of permission name strings for a user. */
    public static function getPermissions(int $userId): array
    {
        $stmt = Database::getConnection()->prepare(
            'SELECT DISTINCT p.`name`
             FROM `permissions` p
             JOIN `role_permissions` rp ON rp.`permission_id` = p.`id`
             JOIN `user_roles` ur       ON ur.`role_id`       = rp.`role_id`
             WHERE ur.`user_id` = ?'
        );
        $stmt->execute([$userId]);
        return $stmt->fetchAll(\PDO::FETCH_COLUMN);
    }

    /** Replace a user's roles with the given role IDs. */
    public static function syncRoles(int $userId, array $roleIds): void
    {
        $pdo = Database::getConnection();
        $pdo->beginTransaction();

        $pdo->prepare('DELETE FROM `user_roles` WHERE `user_id` = ?')->execute([$userId]);

        if (!empty($roleIds)) {
            $placeholders = implode(', ', array_fill(0, count($roleIds), '(?, ?)'));
            $values       = [];
            foreach ($roleIds as $rid) {
                $values[] = $userId;
                $values[] = (int) $rid;
            }
            $pdo->prepare("INSERT INTO `user_roles` (`user_id`, `role_id`) VALUES {$placeholders}")
                ->execute($values);
        }

        $pdo->commit();
    }

    private static function filter(array $data): array
    {
        return array_intersect_key($data, array_flip(self::$fillable));
    }
}
