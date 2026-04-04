# Use the official PHP + Apache image
FROM php:8.2-apache

# Enable mod_rewrite (clean URLs) and mod_ssl (HTTPS)
RUN a2enmod rewrite ssl

# Install MariaDB client library, PHP extensions, and Composer
RUN apt-get update && apt-get install -y libmariadb-dev-compat unzip \
    && rm -rf /var/lib/apt/lists/* \
    && docker-php-ext-install mysqli pdo pdo_mysql \
    && curl -sS https://getcomposer.org/installer | php -- --install-dir=/usr/local/bin --filename=composer

# Install Composer dependencies
COPY composer.json composer.lock /var/www/
RUN cd /var/www && composer install --no-dev --optimize-autoloader

# Copy PHP configuration
COPY docker/php.ini /usr/local/etc/php/conf.d/app.ini

# Copy Apache VirtualHost config (AllowOverride All + SSL)
COPY docker/vhost.conf /etc/apache2/sites-available/000-default.conf

# Generate a self-signed fallback certificate so Apache starts without mounted certs.
# In dev this gets overridden by the mkcert volume mount.
RUN mkdir -p /etc/apache2/certs && \
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
        -keyout /etc/apache2/certs/key.pem \
        -out  /etc/apache2/certs/cert.pem \
        -subj "/CN=localhost"

# Copy application source
COPY public/ /var/www/html/
COPY src/ /var/www/src/
COPY migrations/ /var/www/migrations/

# Copy startup runner
COPY startup.php /var/www/startup.php

# Create writable cache directory
RUN mkdir -p /var/www/cache && chown www-data:www-data /var/www/cache

COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80 443
ENTRYPOINT ["/entrypoint.sh"]
