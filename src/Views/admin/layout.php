<!DOCTYPE html>
<html lang="en" data-theme="dark">

<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title><?= htmlspecialchars($title ?? 'Admin — Shulker Tech') ?></title>
    <link rel="stylesheet" href="/css/fonts.css">
    <link rel="stylesheet" href="/css/main.css">
    <script src="/js/theme.js" defer></script>
</head>

<body>

    <nav class="nav" id="nav">
        <div>
            <a href="<?= HOME_URL ?>" class="nav__logo">
                <span class="nav__logo-shulker">SHULKER</span><span class="nav__logo-tech"> TECH</span>
            </a>
            <span class="nav__badge">Admin</span>
        </div>
        <div class="nav__links" id="navLinks">
            <?php if (\Trevor\ShulkerTech\Auth::check()): ?>
                <a href="/"
                    class="nav__link <?= ($activeSection ?? '') === 'dashboard' ? 'nav__link--active' : '' ?>">Dashboard</a>
                <a href="/servers"
                    class="nav__link <?= ($activeSection ?? '') === 'servers' ? 'nav__link--active' : '' ?>">Servers</a>
                <a href="/users"
                    class="nav__link <?= ($activeSection ?? '') === 'users' ? 'nav__link--active' : '' ?>">Users</a>
                <a href="/roles"
                    class="nav__link <?= ($activeSection ?? '') === 'roles' ? 'nav__link--active' : '' ?>">Roles</a>
                <a href="/settings"
                    class="nav__link <?= ($activeSection ?? '') === 'settings' ? 'nav__link--active' : '' ?>">Settings</a>
            <?php endif; ?>
        </div>
        <div class="nav__actions">
            <?php require __DIR__ . '/../partials/nav-actions.php'; ?>
        </div>
    </nav>

    <div class="admin-wrap">
        <?= $content ?? '' ?>
    </div>

</body>

</html>