#!/bin/sh
set -e

cd /var/www/html

# ── 1. Ensure .env exists ─────────────────────────────────────────────────────
if [ ! -f .env ]; then
    cp .env.example .env
    echo "[startup] Created .env from .env.example — review values and restart."
fi

# ── 2. Ensure runtime directories exist (bind mount may hide image dirs) ──────
mkdir -p storage/framework/cache/data \
         storage/framework/sessions \
         storage/framework/views \
         storage/logs \
         storage/app/public \
         bootstrap/cache

# ── 3. Ensure vendor is present ───────────────────────────────────────────────
if [ ! -d vendor/laravel/framework ]; then
    echo "[startup] Installing Composer dependencies..."
    composer install --no-interaction --optimize-autoloader
fi

# ── 4. Discover packages and publish assets ───────────────────────────────────
echo "[startup] Discovering packages..."
php artisan package:discover --ansi
echo "[startup] Publishing Filament assets..."
php artisan filament:assets

# ── 5. Generate app encryption key if missing ────────────────────────────────
if [ ! -f storage/.key ]; then
    echo "[startup] Generating app key..."
    php -r "echo 'base64:'.base64_encode(random_bytes(32));" > storage/.key
    chmod 640 storage/.key
fi

# ── 6. Run migrations ─────────────────────────────────────────────────────────
echo "[startup] Running migrations..."
php artisan migrate --force

# ── 7. Seed reference data ────────────────────────────────────────────────────
echo "[startup] Seeding permissions..."
php artisan db:seed --class=Database\\Seeders\\PermissionSeeder --force

# ── 8. Cache config in production only (skip in local dev to avoid stale cache)
if [ "${APP_ENV}" != "local" ]; then
    echo "[startup] Caching config..."
    php artisan config:cache
fi

# ── 9. Hand ownership of all runtime files to www-data ───────────────────────
chown -R www-data:www-data storage bootstrap/cache

echo "[startup] Starting Apache..."
exec apache2-foreground
