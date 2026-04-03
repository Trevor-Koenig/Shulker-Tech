<?php

declare(strict_types=1);

require_once __DIR__ . '/../vendor/autoload.php';

// Determine subdomain — use override for local dev, Host header in production.
$subdomain = $_ENV['SUBDOMAIN_OVERRIDE'] !== ''
    ? $_ENV['SUBDOMAIN_OVERRIDE']
    : explode('.', $_SERVER['HTTP_HOST'] ?? '')[0];

ob_start();

switch ($subdomain) {
    case 'wiki':
        $title = 'Wiki — Shulker Tech';
        $activePage = 'wiki';
        require __DIR__ . '/../src/Views/wiki.php';
        break;

    default:
        $title = 'Shulker Tech';
        $activePage = 'home';
        require __DIR__ . '/../src/Views/home.php';
        break;
}

$content = ob_get_clean();

require __DIR__ . '/../src/Views/layout.php';
