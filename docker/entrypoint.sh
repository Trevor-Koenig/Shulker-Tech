#!/bin/sh
set -e

cd /var/www/html

# Create .env from example if it doesn't exist yet
if [ ! -f .env ]; then
    cp .env.example .env
    echo "Created .env from .env.example — update it with your values and restart."
fi

# Auto-generate APP_KEY if blank
if ! grep -q '^APP_KEY=.\+' .env 2>/dev/null; then
    echo "Generating APP_KEY..."
    php artisan key:generate --force
fi

echo "Running database migrations..."
php artisan migrate --force

echo "Seeding permissions and roles..."
php artisan db:seed --class=Database\\Seeders\\PermissionSeeder --force

echo "Caching config..."
php artisan config:cache

echo "Starting Apache..."
exec apache2-foreground
