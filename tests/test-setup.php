<?php

declare(strict_types=1);

require_once __DIR__ . '/../vendor/autoload.php';

// Constants required by the 404 view and layout chain
if (!defined('CURRENT_SUBDOMAIN')) {
    define('CURRENT_SUBDOMAIN', '');
}
if (!defined('HOME_URL')) {
    define('HOME_URL', '/');
}
if (!defined('WIKI_URL')) {
    define('WIKI_URL', '/wiki');
}
if (!defined('ADMIN_URL')) {
    define('ADMIN_URL', '/admin');
}

// Session required by Auth::check() and Csrf::tokenField() in the layout
if (session_status() === PHP_SESSION_NONE) {
    session_start();
}
