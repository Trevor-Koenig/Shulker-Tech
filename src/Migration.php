<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech;

use PDO;

class Migration
{
    private const MIGRATIONS_DIR = __DIR__ . '/../migrations';

    /**
     * Apply all pending migrations in filename order.
     * Safe to call on every request — already-applied files are skipped instantly.
     */
    public static function run(): void
    {
        $pdo = Database::getConnection();

        self::ensureMigrationsTable($pdo);

        $applied = $pdo
            ->query('SELECT `filename` FROM `migrations` ORDER BY `filename` ASC')
            ->fetchAll(PDO::FETCH_COLUMN);

        $files = self::getMigrationFiles();

        foreach ($files as $file) {
            $filename = basename($file);

            if (in_array($filename, $applied, true)) {
                continue;
            }

            $sql = file_get_contents($file);
            if ($sql === false || trim($sql) === '') {
                continue;
            }

            // Strip line comments before splitting so they don't corrupt statements
            $sql = preg_replace('/^\s*--.*$/m', '', $sql);

            foreach (array_filter(array_map('trim', explode(';', $sql))) as $statement) {
                $pdo->exec($statement);
            }

            $stmt = $pdo->prepare('INSERT INTO `migrations` (`filename`) VALUES (?)');
            $stmt->execute([$filename]);
        }
    }

    private static function ensureMigrationsTable(PDO $pdo): void
    {
        $pdo->exec(
            'CREATE TABLE IF NOT EXISTS `migrations` (
                `id`         INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `filename`   VARCHAR(255) NOT NULL UNIQUE,
                `applied_at` TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
        );
    }

    /** @return string[] Sorted list of absolute paths to .sql files. */
    private static function getMigrationFiles(): array
    {
        $pattern = self::MIGRATIONS_DIR . '/*.sql';
        $files = glob($pattern);

        if ($files === false) {
            return [];
        }

        sort($files);
        return $files;
    }
}
