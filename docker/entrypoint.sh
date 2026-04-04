#!/bin/sh
set -e

cd /var/www/html

echo "Caching Laravel config..."
php artisan config:cache

echo "Running database migrations..."
php artisan migrate --force

echo "Seeding permissions/roles (safe to re-run)..."
php artisan db:seed --class=Database\\Seeders\\PermissionSeeder --force

echo "Starting Apache..."
exec apache2-foreground
