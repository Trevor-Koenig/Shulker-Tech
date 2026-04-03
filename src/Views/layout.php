<?php
use Trevor\ShulkerTech\Auth;
use Trevor\ShulkerTech\Csrf;

$isLoggedIn = Auth::check();
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
        <a href="<?= HOME_URL ?>" class="nav__logo">
            <span class="nav__logo-shulker">SHULKER</span>TECH
        </a>

        <ul class="nav__links">
            <li><a href="<?= HOME_URL ?>"
                    class="nav__link <?= ($activePage ?? '') === 'home' ? 'nav__link--active' : '' ?>">Home</a></li>
            <li><a href="<?= WIKI_URL ?>"
                    class="nav__link <?= ($activePage ?? '') === 'wiki' ? 'nav__link--active' : '' ?>">Wiki</a></li>
            <?php if ($isLoggedIn && ADMIN_URL !== ''): ?>
                <li><a href="<?= ADMIN_URL ?>" class="nav__link">Admin</a></li>
            <?php endif; ?>
        </ul>

        <div class="nav__actions">
            <?php require __DIR__ . '/partials/nav-actions.php'; ?>
            <button class="nav__hamburger" id="navHamburger" aria-label="Toggle menu" aria-expanded="false">
                <span></span><span></span><span></span>
            </button>
        </div>
    </nav>

    <div class="nav__mobile-menu" id="navMobileMenu">
        <a href="<?= HOME_URL ?>" class="<?= ($activePage ?? '') === 'home' ? 'active' : '' ?>">Home</a>
        <a href="<?= WIKI_URL ?>" class="<?= ($activePage ?? '') === 'wiki' ? 'active' : '' ?>">Wiki</a>
        <?php if ($isLoggedIn && ADMIN_URL !== ''): ?>
            <a href="<?= ADMIN_URL ?>">Admin</a>
        <?php endif; ?>
        <?php if ($isLoggedIn): ?>
            <form method="POST" action="/logout">
                <?= Csrf::tokenField() ?>
                <button type="submit" class="nav__mobile-logout">Logout</button>
            </form>
        <?php else: ?>
            <a href="/login">Login</a>
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