<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech;

class Csrf
{
    public static function getToken(): string
    {
        if (empty($_SESSION['csrf_token'])) {
            $_SESSION['csrf_token'] = bin2hex(random_bytes(32));
        }

        return $_SESSION['csrf_token'];
    }

    public static function tokenField(): string
    {
        $token = htmlspecialchars(self::getToken());
        return '<input type="hidden" name="_csrf" value="' . $token . '">';
    }

    public static function verifyRequest(): void
    {
        $token = $_POST['_csrf'] ?? '';

        if (!hash_equals($_SESSION['csrf_token'] ?? '', $token)) {
            http_response_code(419);
            exit('Invalid CSRF token.');
        }
    }
}
