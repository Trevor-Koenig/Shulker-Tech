<?php
use Trevor\ShulkerTech\Models\User;
use Trevor\ShulkerTech\Models\Server;
use Trevor\ShulkerTech\Models\Role;

$activeSection = 'dashboard';
$userCount = User::count();
$serverCount = Server::count();

ob_start();
?>
<div class="admin-page">
    <div class="admin-page__header">
        <div>
            <h1 class="admin-page__title">Dashboard</h1>
            <p class="admin-page__subtitle">Welcome back,
                <?= htmlspecialchars(\Trevor\ShulkerTech\Auth::user()['username'] ?? '') ?></p>
        </div>
    </div>

    <div class="stat-grid">
        <a href="/servers" class="stat-card">
            <div class="stat-card__label">Servers</div>
            <div class="stat-card__value"><?= $serverCount ?></div>
        </a>
        <a href="/users" class="stat-card">
            <div class="stat-card__label">Users</div>
            <div class="stat-card__value"><?= $userCount ?></div>
        </a>
        <a href="/roles" class="stat-card">
            <div class="stat-card__label">Roles</div>
            <div class="stat-card__value"><?= count(Role::all()) ?></div>
        </a>
    </div>

    <div class="admin-quick-links">
        <a href="/servers/create" class="btn btn--primary">Add Server</a>
        <a href="/users/create" class="btn btn--secondary">Add User</a>
        <a href="/roles/create" class="btn btn--secondary">Add Role</a>
    </div>
</div>
<?php
$content = ob_get_clean();
require __DIR__ . '/layout.php';
