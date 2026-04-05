<?php

return [
    'name' => env('APP_NAME', 'Shulker Tech'),
    'env' => env('APP_ENV', 'production'),
    'debug' => (bool) env('APP_DEBUG', false),
    'url' => env('APP_URL', 'https://localhost'),
    'timezone' => 'UTC',
    'locale' => 'en',
    'fallback_locale' => 'en',
    'faker_locale' => 'en_US',
    'cipher' => 'AES-256-CBC',
    'key' => env('APP_KEY') ?: (
        is_readable(storage_path('.key'))
            ? trim(file_get_contents(storage_path('.key')))
            : null
    ),
    'previous_keys' => [
        ...array_filter(
            explode(',', env('APP_PREVIOUS_KEYS', ''))
        ),
    ],
    'maintenance' => [
        'driver' => 'file',
    ],

    'setup_token' => env('ADMIN_SETUP_TOKEN', ''),

    // The base domain used for subdomain routing (e.g. "localhost" or "shulkertech.com")
    'domain' => env('APP_DOMAIN', 'localhost'),

    // Full domain names for each subdomain
    'home_domain'  => env('HOME_DOMAIN',  env('APP_DOMAIN', 'localhost')),
    'wiki_domain'  => env('WIKI_DOMAIN',  'wiki.'.env('APP_DOMAIN', 'localhost')),
    'admin_domain' => env('ADMIN_DOMAIN', 'admin.'.env('APP_DOMAIN', 'localhost')),

    'providers' => Illuminate\Support\ServiceProvider::defaultProviders()->merge([
        App\Providers\AppServiceProvider::class,
    ])->toArray(),
];
