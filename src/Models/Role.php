<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech\Models;

use Trevor\ShulkerTech\Database;

class Role
{
    private static array $fillable = ['name', 'description'];

    public static function all(): array
    {
        return Database::getConnection()
            ->query('SELECT * FROM `roles` ORDER BY `id` ASC')
            ->fetchAll();
    }

    public static function findById(int $id): ?array
    {
        $stmt = Database::getConnection()->prepare('SELECT * FROM `roles` WHERE `id` = ?');
        $stmt->execute([$id]);
        $row = $stmt->fetch();
        return $row ?: null;
    }

    public static function create(array $data): int
    {
        $data = self::filter($data);
        $pdo  = Database::getConnection();

        $cols         = implode(', ', array_map(fn($k) => "`{$k}`", array_keys($data)));
        $placeholders = implode(', ', array_fill(0, count($data), '?'));

        $stmt = $pdo->prepare("INSERT INTO `roles` ({$cols}) VALUES ({$placeholders})");
        $stmt->execute(array_values($data));
        return (int) $pdo->lastInsertId();
    }

    public static function update(int $id, array $data): void
    {
        $data = self::filter($data);
        $pdo  = Database::getConnection();

        $sets = implode(', ', array_map(fn($k) => "`{$k}` = ?", array_keys($data)));
        $stmt = $pdo->prepare("UPDATE `roles` SET {$sets} WHERE `id` = ?");
        $stmt->execute([...array_values($data), $id]);
    }

    public static function delete(int $id): void
    {
        $stmt = Database::getConnection()->prepare('DELETE FROM `roles` WHERE `id` = ?');
        $stmt->execute([$id]);
    }

    public static function allPermissions(): array
    {
        return Database::getConnection()
            ->query('SELECT * FROM `permissions` ORDER BY `name` ASC')
            ->fetchAll();
    }

    /** Returns a flat array of permission name strings for a role. */
    public static function getPermissions(int $roleId): array
    {
        $stmt = Database::getConnection()->prepare(
            'SELECT p.`name`
             FROM `permissions` p
             JOIN `role_permissions` rp ON rp.`permission_id` = p.`id`
             WHERE rp.`role_id` = ?'
        );
        $stmt->execute([$roleId]);
        return $stmt->fetchAll(\PDO::FETCH_COLUMN);
    }

    /** Replace a role's permissions with the given permission IDs. */
    public static function syncPermissions(int $roleId, array $permissionIds): void
    {
        $pdo = Database::getConnection();
        $pdo->beginTransaction();

        $pdo->prepare('DELETE FROM `role_permissions` WHERE `role_id` = ?')->execute([$roleId]);

        if (!empty($permissionIds)) {
            $placeholders = implode(', ', array_fill(0, count($permissionIds), '(?, ?)'));
            $values       = [];
            foreach ($permissionIds as $pid) {
                $values[] = $roleId;
                $values[] = (int) $pid;
            }
            $pdo->prepare("INSERT INTO `role_permissions` (`role_id`, `permission_id`) VALUES {$placeholders}")
                ->execute($values);
        }

        $pdo->commit();
    }

    private static function filter(array $data): array
    {
        return array_intersect_key($data, array_flip(self::$fillable));
    }
}
