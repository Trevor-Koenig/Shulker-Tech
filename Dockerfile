FROM php:8.4-apache

# Enable mod_rewrite and mod_ssl
RUN a2enmod rewrite ssl headers

# Install system deps and all PHP extensions required by Laravel + Filament + openspout
RUN apt-get update && apt-get install -y \
        libmariadb-dev-compat \
        libicu-dev \
        libzip-dev \
        libpng-dev \
        libjpeg-dev \
        libwebp-dev \
        unzip \
    && rm -rf /var/lib/apt/lists/* \
    && docker-php-ext-configure gd --with-jpeg --with-webp \
    && docker-php-ext-install mysqli pdo pdo_mysql intl zip bcmath gd opcache \
    && curl -sS https://getcomposer.org/installer | php -- --install-dir=/usr/local/bin --filename=composer

# Internal Docker defaults — container concerns, not deployment config.
# The mounted .env file overrides anything here except APACHE_DOCUMENT_ROOT.
ENV APACHE_DOCUMENT_ROOT=/var/www/html/public \
    APP_ENV=production \
    APP_DEBUG=false \
    APP_NAME="Shulker Tech" \
    DB_HOST=db \
    DB_PORT=3306 \
    CACHE_STORE=file \
    LOG_CHANNEL=stderr
RUN sed -ri -e 's!/var/www/html!/var/www/html/public!g' \
    /etc/apache2/sites-available/*.conf /etc/apache2/apache2.conf /etc/apache2/conf-available/*.conf

# Copy PHP config and Apache vhost
COPY docker/php.ini  /usr/local/etc/php/conf.d/app.ini
COPY docker/vhost.conf /etc/apache2/sites-available/000-default.conf

# Self-signed fallback cert (overridden by mkcert volume mount in dev)
RUN mkdir -p /etc/apache2/certs && \
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
        -keyout /etc/apache2/certs/key.pem \
        -out    /etc/apache2/certs/cert.pem \
        -subj "/CN=localhost"

# Install Composer deps first (cached layer — rebuilt only when composer.json or composer.lock changes)
COPY composer.json composer.lock /var/www/html/
RUN cd /var/www/html && composer install --no-dev --optimize-autoloader --no-scripts

# Copy full application
COPY . /var/www/html/

# Storage and cache dirs writable by www-data
RUN mkdir -p /var/www/html/storage/{app/public,framework/{cache/data,sessions,views},logs} \
             /var/www/html/bootstrap/cache \
    && chown -R www-data:www-data /var/www/html/storage /var/www/html/bootstrap/cache

COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80 443
ENTRYPOINT ["/entrypoint.sh"]
