<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech;

class Startup
{
    public static function run(): void
    {
        self::runMigrations();
    }

    private static function runMigrations(): void
    {
        echo "[startup] Running database migrations...\n";
        Migration::run();
        echo "[startup] Migrations complete.\n";
    }
}
