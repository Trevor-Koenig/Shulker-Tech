#!/bin/sh
set -e

echo "Running startup tasks..."
php /var/www/startup.php

echo "Starting Apache..."
exec apache2-foreground
