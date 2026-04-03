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
        <div class="nav__brand">
            <a href="<?= htmlspecialchars($_ENV['HOME_URL'] ?? '/') ?>" class="nav__logo">
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
                <form method="POST" action="/logout" style="display:inline">
                    <?= \Trevor\ShulkerTech\Csrf::tokenField() ?>
                    <button type="submit" class="nav__link btn btn--ghost btn--sm">Logout</button>
                </form>
            <?php endif; ?>
            <button class="nav__theme-toggle" id="themeToggle" aria-label="Toggle theme" title="Toggle theme">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"
                    stroke-linejoin="round">
                    <circle cx="12" cy="12" r="5" />
                    <line x1="12" y1="1" x2="12" y2="3" />
                    <line x1="12" y1="21" x2="12" y2="23" />
                    <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" />
                    <line x1="18.36" y1="18.36" x2="19.78" y2="19.78" />
                    <line x1="1" y1="12" x2="3" y2="12" />
                    <line x1="21" y1="12" x2="23" y2="12" />
                    <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" />
                    <line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
                </svg>
            </button>
        </div>
    </nav>

    <div class="admin-wrap">
        <?= $content ?? '' ?>
    </div>

</body>

</html>