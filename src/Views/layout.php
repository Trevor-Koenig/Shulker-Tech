<?php
use Trevor\ShulkerTech\Auth;
use Trevor\ShulkerTech\Csrf;

$homeUrl = htmlspecialchars($_ENV['HOME_URL'] ?? '/');
$wikiUrl = htmlspecialchars($_ENV['WIKI_URL'] ?? '/wiki');
$adminUrl = htmlspecialchars($_ENV['ADMIN_URL'] ?? '');

$isLoggedIn = Auth::check();
$authUser = Auth::user();
?>
<!DOCTYPE html>
<html lang="en" data-theme="dark">

<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title><?= htmlspecialchars($title ?? 'Shulker Tech') ?></title>
    <link rel="stylesheet" href="/css/fonts.css">
    <link rel="stylesheet" href="/css/main.css">
    <script src="/js/theme.js"></script>
    <script src="/js/nav.js" defer></script>
</head>

<body>

    <nav class="nav" id="nav">
        <a href="<?= $homeUrl ?>" class="nav__logo">
            <span class="nav__logo-shulker">SHULKER</span>TECH
        </a>

        <ul class="nav__links">
            <li><a href="<?= $homeUrl ?>"
                    class="nav__link <?= ($activePage ?? '') === 'home' ? 'nav__link--active' : '' ?>">Home</a></li>
            <li><a href="<?= $wikiUrl ?>"
                    class="nav__link <?= ($activePage ?? '') === 'wiki' ? 'nav__link--active' : '' ?>">Wiki</a></li>
            <?php if ($isLoggedIn && $adminUrl !== ''): ?>
                <li><a href="<?= $adminUrl ?>" class="nav__link">Admin</a></li>
            <?php endif; ?>
        </ul>

        <div class="nav__actions">
            <?php if ($isLoggedIn): ?>
                <span class="nav__username"><?= htmlspecialchars($authUser['username'] ?? '') ?></span>
                <form method="POST" action="/logout" class="nav__logout-form">
                    <?= Csrf::tokenField() ?>
                    <button type="submit" class="btn btn--ghost btn--sm">Logout</button>
                </form>
            <?php elseif ($adminUrl !== ''): ?>
                <a href="<?= $adminUrl ?>/login" class="btn btn--ghost btn--sm">Login</a>
            <?php endif; ?>
            <button class="nav__theme-toggle" id="themeToggle" aria-label="Toggle theme">
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
            <button class="nav__hamburger" id="navHamburger" aria-label="Toggle menu" aria-expanded="false">
                <span></span><span></span><span></span>
            </button>
        </div>
    </nav>

    <div class="nav__mobile-menu" id="navMobileMenu">
        <a href="<?= $homeUrl ?>" class="<?= ($activePage ?? '') === 'home' ? 'active' : '' ?>">Home</a>
        <a href="<?= $wikiUrl ?>" class="<?= ($activePage ?? '') === 'wiki' ? 'active' : '' ?>">Wiki</a>
        <?php if ($isLoggedIn && $adminUrl !== ''): ?>
            <a href="<?= $adminUrl ?>">Admin</a>
        <?php endif; ?>
        <?php if ($isLoggedIn): ?>
            <form method="POST" action="/logout">
                <?= Csrf::tokenField() ?>
                <button type="submit" class="nav__mobile-logout">Logout</button>
            </form>
        <?php elseif ($adminUrl !== ''): ?>
            <a href="<?= $adminUrl ?>/login">Login</a>
        <?php endif; ?>
    </div>

    <div class="page">
        <?php if ($sidebar ?? false): ?>
            <aside class="sidebar">
                <?= $sidebar ?>
            </aside>
        <?php endif; ?>

        <main class="main">
            <?= $content ?>
        </main>
    </div>

    <footer class="footer">
        &copy; <?= date('Y') ?> Shulker Tech &mdash; Built with PHP &amp; ❤
    </footer>

</body>

</html>