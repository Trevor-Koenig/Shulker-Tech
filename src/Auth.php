<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech;

use Trevor\ShulkerTech\Models\User;

class Auth
{
    public static function login(array $user): void
    {
        session_regenerate_id(true);
        $_SESSION['auth'] = [
            'id' => $user['id'],
            'username' => $user['username'],
            'email' => $user['email'],
        ];
        self::loadPermissions((int) $user['id']);
    }

    public static function logout(): void
    {
        $_SESSION = [];
        if (ini_get('session.use_cookies')) {
            $params = session_get_cookie_params();
            setcookie(
                session_name(),
                '',
                time() - 42000,
                $params['path'],
                $params['domain'],
                $params['secure'],
                $params['httponly']
            );
        }
        session_destroy();
    }

    public static function check(): bool
    {
        return isset($_SESSION['auth']);
    }

    public static function user(): ?array
    {
        return $_SESSION['auth'] ?? null;
    }

    public static function can(string $permission): bool
    {
        return in_array($permission, $_SESSION['auth_permissions'] ?? [], true);
    }

    /** Guard: redirect to login if not authenticated. */
    public static function requireLogin(): void
    {
        if (!self::check()) {
            header('Location: /login');
            exit;
        }
    }

    /** Guard: 403 if authenticated but lacks permission. */
    public static function requirePermission(string $permission): void
    {
        self::requireLogin();
        if (!self::can($permission)) {
            http_response_code(403);
            exit('Access denied.');
        }
    }

    private static function loadPermissions(int $userId): void
    {
        $_SESSION['auth_permissions'] = User::getPermissions($userId);
    }
}
