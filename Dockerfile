FROM php:8.2-apache

# Enable mod_rewrite and mod_ssl
RUN a2enmod rewrite ssl headers

# Install system deps and PHP extensions
RUN apt-get update && apt-get install -y libmariadb-dev-compat unzip \
    && rm -rf /var/lib/apt/lists/* \
    && docker-php-ext-install mysqli pdo pdo_mysql \
    && curl -sS https://getcomposer.org/installer | php -- --install-dir=/usr/local/bin --filename=composer

# Set DocumentRoot to Laravel's public directory
ENV APACHE_DOCUMENT_ROOT /var/www/html/public
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

# Install Composer deps first (cached layer)
# Uses composer update on first build (no lock file); subsequent builds use install once lock is committed
COPY composer.json /var/www/html/
RUN cd /var/www/html && composer update --no-dev --optimize-autoloader --no-scripts

# Copy full application
COPY . /var/www/html/

# Run post-install scripts now that the full app is present
RUN cd /var/www/html && composer run-script post-autoload-dump --no-interaction 2>/dev/null || true

# Storage and cache dirs writable by www-data
RUN mkdir -p /var/www/html/storage/{app/public,framework/{cache/data,sessions,views},logs} \
             /var/www/html/bootstrap/cache \
    && chown -R www-data:www-data /var/www/html/storage /var/www/html/bootstrap/cache

COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80 443
ENTRYPOINT ["/entrypoint.sh"]
