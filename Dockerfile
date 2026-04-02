# Use a lightweight base
FROM debian:bookworm-slim

# Install packages
RUN apt-get update && apt-get install -y \
        nginx \
        php8.2-fpm \
        php8.2-cli \
        php8.2-mbstring \
        php8.2-xml \
        php8.2-zip \
        unzip \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Copy your source code
COPY src/ /var/www/html

# Copy nginx configuration (adjust upstream to php-fpm socket)
COPY nginx.conf /etc/nginx/nginx.conf

# Ensure the php‑fpm socket is readable by nginx
RUN mkdir -p /var/run/php && \
    chown -R www-data:www-data /var/www/html /var/run/php

# Set up supervisord to run both services
RUN apt-get update && apt-get install -y supervisor && rm -rf /var/lib/apt/lists/*

COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

# Expose web port
EXPOSE 80

# Start supervisord
CMD ["/usr/bin/supervisord", "-n"]