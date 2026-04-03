# Use the official PHP + Apache image
FROM php:8.2-apache

# Enable mod_rewrite for clean URLs
RUN a2enmod rewrite

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

# Copy application source
COPY public/ /var/www/html/
COPY src/ /var/www/src/
COPY migrations/ /var/www/migrations/

# Copy startup runner
COPY startup.php /var/www/startup.php

# Create writable cache directory
RUN mkdir -p /var/www/cache && chown www-data:www-data /var/www/cache

# Set Apache document root to public/
ENV APACHE_DOCUMENT_ROOT=/var/www/html

RUN sed -i 's|/var/www/html|${APACHE_DOCUMENT_ROOT}|g' /etc/apache2/sites-available/000-default.conf \
    && sed -i 's|/var/www/html|${APACHE_DOCUMENT_ROOT}|g' /etc/apache2/apache2.conf

COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80
ENTRYPOINT ["/entrypoint.sh"]
