<?php

declare(strict_types=1);

require_once __DIR__ . '/../vendor/autoload.php';

// Share session across all subdomains in production (e.g. SESSION_DOMAIN=.example.com)
$sessionDomain = $_ENV['SESSION_DOMAIN'] ?? '';
if ($sessionDomain !== '') {
    session_set_cookie_params([
        'lifetime' => 0,
        'path'     => '/',
        'domain'   => $sessionDomain,
        'secure'   => true,
        'httponly' => true,
        'samesite' => 'Lax',
    ]);
}

session_start();

use Trevor\ShulkerTech\Auth;
use Trevor\ShulkerTech\Csrf;
use Trevor\ShulkerTech\Router;
use Trevor\ShulkerTech\Models\User;
use Trevor\ShulkerTech\Models\Role;
use Trevor\ShulkerTech\Models\Server;
use Trevor\ShulkerTech\Models\Setting;

// Determine subdomain — use override for local dev, Host header in production.
$subdomain = ($_ENV['SUBDOMAIN_OVERRIDE'] ?? '') !== ''
    ? $_ENV['SUBDOMAIN_OVERRIDE']
    : explode('.', $_SERVER['HTTP_HOST'] ?? '')[0];

ob_start();

// Redirect to setup wizard if no users exist yet
if (User::count() === 0) {
    $currentPath = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
    $onSetup     = $subdomain === 'admin' && $currentPath === '/setup';
    if (!$onSetup) {
        $adminUrl = rtrim($_ENV['ADMIN_URL'] ?? '', '/');
        header("Location: {$adminUrl}/setup");
        exit;
    }
}

switch ($subdomain) {
    case 'admin':
        $router = new Router();

        // Auth routes
        $router->get('/login', function () {
            $title = 'Login — Shulker Tech Admin';
            require __DIR__ . '/../src/Views/admin/login.php';
        });
        $router->post('/login', function () {
            Csrf::verifyRequest();
            $email = trim($_POST['email'] ?? '');
            $password = trim($_POST['password'] ?? '');
            $user = User::findByEmail($email);
            if ($user && User::verifyPassword($password, $user['password_hash'])) {
                Auth::login($user);
                header('Location: /');
                exit;
            }
            $error = 'Invalid email or password.';
            $title = 'Login — Shulker Tech Admin';
            require __DIR__ . '/../src/Views/admin/login.php';
        });
        $router->post('/logout', function () {
            Csrf::verifyRequest();
            Auth::logout();
            header('Location: /login');
            exit;
        });

        // First-run setup (only when users table is empty)
        $router->get('/setup', function () {
            if (User::count() > 0) {
                header('Location: /login');
                exit;
            }
            $title = 'Setup — Shulker Tech Admin';
            require __DIR__ . '/../src/Views/admin/setup.php';
        });
        $router->post('/setup', function () {
            if (User::count() > 0) {
                header('Location: /login');
                exit;
            }
            Csrf::verifyRequest();
            $token = trim($_POST['setup_token'] ?? '');
            if (!hash_equals($_ENV['ADMIN_SETUP_TOKEN'] ?? '', $token)) {
                $error = 'Invalid setup token.';
                $title = 'Setup — Shulker Tech Admin';
                require __DIR__ . '/../src/Views/admin/setup.php';
                return;
            }
            $username = trim($_POST['username'] ?? '');
            $email = trim($_POST['email'] ?? '');
            $password = trim($_POST['password'] ?? '');
            if ($username === '' || $email === '' || $password === '') {
                $error = 'All fields are required.';
                $title = 'Setup — Shulker Tech Admin';
                require __DIR__ . '/../src/Views/admin/setup.php';
                return;
            }
            $userId = User::create([
                'username' => $username,
                'email' => $email,
                'password_hash' => User::hashPassword($password),
                'is_active' => 1,
            ]);
            // Assign Super Admin role (id=1 from schema seed)
            User::syncRoles($userId, [1]);
            $user = User::findById($userId);
            Auth::login($user);
            header('Location: /');
            exit;
        });

        // Dashboard
        $router->get('/', function () {
            Auth::requireLogin();
            $title = 'Dashboard — Shulker Tech Admin';
            require __DIR__ . '/../src/Views/admin/dashboard.php';
        });

        // Servers
        $router->get('/servers', function () {
            Auth::requirePermission('servers.view');
            $title = 'Servers — Shulker Tech Admin';
            $servers = Server::all();
            require __DIR__ . '/../src/Views/admin/servers/index.php';
        });
        $router->get('/servers/create', function () {
            Auth::requirePermission('servers.create');
            $title = 'Add Server — Shulker Tech Admin';
            $server = null;
            require __DIR__ . '/../src/Views/admin/servers/form.php';
        });
        $router->post('/servers/create', function () {
            Auth::requirePermission('servers.create');
            Csrf::verifyRequest();
            Server::create($_POST);
            header('Location: /servers');
            exit;
        });
        $router->get('/servers/{id}/edit', function (array $params) {
            Auth::requirePermission('servers.edit');
            $server = Server::findById((int) $params['id']);
            if (!$server) {
                http_response_code(404);
                exit('Not found.');
            }
            $title = 'Edit Server — Shulker Tech Admin';
            require __DIR__ . '/../src/Views/admin/servers/form.php';
        });
        $router->post('/servers/{id}/edit', function (array $params) {
            Auth::requirePermission('servers.edit');
            Csrf::verifyRequest();
            Server::update((int) $params['id'], $_POST);
            header('Location: /servers');
            exit;
        });
        $router->post('/servers/{id}/delete', function (array $params) {
            Auth::requirePermission('servers.delete');
            Csrf::verifyRequest();
            Server::delete((int) $params['id']);
            header('Location: /servers');
            exit;
        });

        // Users
        $router->get('/users', function () {
            Auth::requirePermission('users.view');
            $title = 'Users — Shulker Tech Admin';
            $users = User::all();
            require __DIR__ . '/../src/Views/admin/users/index.php';
        });
        $router->get('/users/create', function () {
            Auth::requirePermission('users.create');
            $title = 'Add User — Shulker Tech Admin';
            $user = null;
            $roles = Role::all();
            require __DIR__ . '/../src/Views/admin/users/form.php';
        });
        $router->post('/users/create', function () {
            Auth::requirePermission('users.create');
            Csrf::verifyRequest();
            $userId = User::create([
                'username' => $_POST['username'] ?? '',
                'email' => $_POST['email'] ?? '',
                'password_hash' => User::hashPassword($_POST['password'] ?? ''),
                'is_active' => isset($_POST['is_active']) ? 1 : 0,
            ]);
            User::syncRoles($userId, array_map('intval', $_POST['roles'] ?? []));
            header('Location: /users');
            exit;
        });
        $router->get('/users/{id}/edit', function (array $params) {
            Auth::requirePermission('users.edit');
            $user = User::findById((int) $params['id']);
            if (!$user) {
                http_response_code(404);
                exit('Not found.');
            }
            $roles = Role::all();
            $userRoles = array_column(User::getRoles((int) $params['id']), 'id');
            $title = 'Edit User — Shulker Tech Admin';
            require __DIR__ . '/../src/Views/admin/users/form.php';
        });
        $router->post('/users/{id}/edit', function (array $params) {
            Auth::requirePermission('users.edit');
            Csrf::verifyRequest();
            $data = [
                'username' => $_POST['username'] ?? '',
                'email' => $_POST['email'] ?? '',
                'is_active' => isset($_POST['is_active']) ? 1 : 0,
            ];
            if (!empty($_POST['password'])) {
                $data['password_hash'] = User::hashPassword($_POST['password']);
            }
            User::update((int) $params['id'], $data);
            User::syncRoles((int) $params['id'], array_map('intval', $_POST['roles'] ?? []));
            header('Location: /users');
            exit;
        });
        $router->post('/users/{id}/delete', function (array $params) {
            Auth::requirePermission('users.delete');
            Csrf::verifyRequest();
            User::delete((int) $params['id']);
            header('Location: /users');
            exit;
        });

        // Roles
        $router->get('/roles', function () {
            Auth::requirePermission('roles.view');
            $title = 'Roles — Shulker Tech Admin';
            $roles = Role::all();
            require __DIR__ . '/../src/Views/admin/roles/index.php';
        });
        $router->get('/roles/create', function () {
            Auth::requirePermission('roles.create');
            $title = 'Add Role — Shulker Tech Admin';
            $role = null;
            $permissions = Role::allPermissions();
            $rolePerms = [];
            require __DIR__ . '/../src/Views/admin/roles/form.php';
        });
        $router->post('/roles/create', function () {
            Auth::requirePermission('roles.create');
            Csrf::verifyRequest();
            $roleId = Role::create(['name' => $_POST['name'] ?? '', 'description' => $_POST['description'] ?? '']);
            Role::syncPermissions($roleId, array_map('intval', $_POST['permissions'] ?? []));
            header('Location: /roles');
            exit;
        });
        $router->get('/roles/{id}/edit', function (array $params) {
            Auth::requirePermission('roles.edit');
            $role = Role::findById((int) $params['id']);
            if (!$role) {
                http_response_code(404);
                exit('Not found.');
            }
            $permissions = Role::allPermissions();
            $rolePerms = Role::getPermissions((int) $params['id']);
            $title = 'Edit Role — Shulker Tech Admin';
            require __DIR__ . '/../src/Views/admin/roles/form.php';
        });
        $router->post('/roles/{id}/edit', function (array $params) {
            Auth::requirePermission('roles.edit');
            Csrf::verifyRequest();
            Role::update((int) $params['id'], ['name' => $_POST['name'] ?? '', 'description' => $_POST['description'] ?? '']);
            Role::syncPermissions((int) $params['id'], array_map('intval', $_POST['permissions'] ?? []));
            header('Location: /roles');
            exit;
        });
        $router->post('/roles/{id}/delete', function (array $params) {
            Auth::requirePermission('roles.delete');
            Csrf::verifyRequest();
            Role::delete((int) $params['id']);
            header('Location: /roles');
            exit;
        });

        // Settings
        $router->get('/settings', function () {
            Auth::requirePermission('admin.access');
            $title    = 'Settings — Shulker Tech Admin';
            $settings = Setting::all();
            require __DIR__ . '/../src/Views/admin/settings.php';
        });
        $router->post('/settings', function () {
            Auth::requirePermission('admin.access');
            Csrf::verifyRequest();
            foreach ($_POST['settings'] ?? [] as $key => $value) {
                Setting::set((string) $key, (string) $value);
            }
            $title    = 'Settings — Shulker Tech Admin';
            $settings = Setting::all();
            $success  = 'Settings saved.';
            require __DIR__ . '/../src/Views/admin/settings.php';
        });

        ob_end_clean(); // Admin views manage their own output buffering
        $router->dispatch($_SERVER['REQUEST_METHOD'], $_SERVER['REQUEST_URI']);
        exit;

    case 'wiki':
        // Logout from public subdomains
        if ($_SERVER['REQUEST_METHOD'] === 'POST' && parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH) === '/logout') {
            Csrf::verifyRequest();
            Auth::logout();
            header('Location: ' . ($_ENV['HOME_URL'] ?? '/'));
            exit;
        }
        $title = 'Wiki — Shulker Tech';
        $activePage = 'wiki';
        require __DIR__ . '/../src/Views/wiki.php';
        break;

    default:
        // Logout from public subdomains
        if ($_SERVER['REQUEST_METHOD'] === 'POST' && parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH) === '/logout') {
            Csrf::verifyRequest();
            Auth::logout();
            header('Location: ' . ($_ENV['HOME_URL'] ?? '/'));
            exit;
        }
        $title = 'Shulker Tech';
        $activePage = 'home';
        require __DIR__ . '/../src/Views/home.php';
        break;
}

$content = ob_get_clean();

if ($subdomain !== 'admin') {
    require __DIR__ . '/../src/Views/layout.php';
}
