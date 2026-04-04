<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech;

class Router
{
    /** @var array<int, array{method: string, path: string, handler: callable, guard: callable|null}> */
    private array $routes = [];

    public function get(string $path, callable $handler, ?callable $guard = null): void
    {
        $this->routes[] = ['method' => 'GET', 'path' => $path, 'handler' => $handler, 'guard' => $guard];
    }

    public function post(string $path, callable $handler, ?callable $guard = null): void
    {
        $this->routes[] = ['method' => 'POST', 'path' => $path, 'handler' => $handler, 'guard' => $guard];
    }

    public function dispatch(string $method, string $uri, ?callable $notFoundGuard = null): void
    {
        $path = parse_url($uri, PHP_URL_PATH) ?? '/';
        $path = '/' . trim($path, '/');
        if ($path === '') {
            $path = '/';
        }

        foreach ($this->routes as $route) {
            if ($route['method'] !== strtoupper($method)) {
                continue;
            }

            $pattern = preg_replace('/\{(\w+)\}/', '(?P<$1>[^/]+)', $route['path']);
            $pattern = '#^' . $pattern . '$#';

            if (preg_match($pattern, $path, $matches) !== 1) {
                continue;
            }

            // Extract only named captures
            $params = array_filter(
                $matches,
                fn($key) => is_string($key),
                ARRAY_FILTER_USE_KEY
            );

            if ($route['guard'] !== null) {
                ($route['guard'])();
            }

            ($route['handler'])($params);
            return;
        }

        if($notFoundGuard !== null) {
            $notFoundGuard();
        }
        http_response_code(404);
        require __DIR__ . '/Views/404.php';
    }
}
