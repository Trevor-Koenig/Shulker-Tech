<?php
$homeUrl = htmlspecialchars($_ENV['HOME_URL'] ?? '/');
$wikiUrl = htmlspecialchars($_ENV['WIKI_URL'] ?? '/wiki');
?>
<!DOCTYPE html>
<html lang="en" data-theme="dark">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title><?= htmlspecialchars($title ?? 'Shulker Tech') ?></title>
    <link rel="stylesheet" href="/css/main.css">
    <script src="/js/theme.js"></script>
    <script src="/js/nav.js" defer></script>
</head>
<body>

<nav class="nav">
    <a href="<?= $homeUrl ?>" class="nav__logo"><span>SHULKER</span> TECH</a>
    <ul class="nav__links">
        <li><a href="<?= $homeUrl ?>" <?= ($activePage ?? '') === 'home' ? 'class="active"' : '' ?>>Home</a></li>
        <li><a href="<?= $wikiUrl ?>" <?= ($activePage ?? '') === 'wiki' ? 'class="active"' : '' ?>>Wiki</a></li>
    </ul>
    <div class="nav__actions">
        <button class="theme-toggle" id="theme-toggle" aria-label="Toggle theme">☀</button>
        <button class="nav__hamburger" id="nav-hamburger" aria-label="Toggle menu" aria-expanded="false">
            <span></span>
            <span></span>
            <span></span>
        </button>
    </div>
</nav>
<div class="nav__mobile-menu" id="nav-mobile-menu">
    <a href="<?= $homeUrl ?>" <?= ($activePage ?? '') === 'home' ? 'class="active"' : '' ?>>Home</a>
    <a href="<?= $wikiUrl ?>" <?= ($activePage ?? '') === 'wiki' ? 'class="active"' : '' ?>>Wiki</a>
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
