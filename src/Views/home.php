<?php
use Trevor\ShulkerTech\Health;

$rawBluemapUrl  = $_ENV['BLUEMAP_URL'] ?? 'http://localhost:8100';
$bluemapUrl     = htmlspecialchars($rawBluemapUrl);
$bluemapOnline  = Health::isReachable($rawBluemapUrl);
?>

<section class="hero">
    <?php if ($bluemapOnline): ?>
    <div class="hero__map">
        <iframe
            src="<?= $bluemapUrl ?>"
            class="hero__map-frame"
            title="Live world map"
            loading="lazy"
            sandbox="allow-scripts allow-same-origin"
            tabindex="-1"
            aria-hidden="true"
        ></iframe>
        <div class="hero__map-overlay"></div>
    </div>
    <?php endif; ?>

    <div class="hero__content">
        <p class="hero__eyebrow">Welcome to the Server</p>
        <h1 class="hero__title"><span>SHULKER</span> TECH</h1>
        <p class="hero__subtitle">
            Your central hub for server guides, community resources, and technical documentation.
        </p>
        <a href="/wiki" class="btn btn--primary">Browse the Wiki</a>
    </div>
</section>

<section class="card-section">
    <div class="card-section__header">
        <h2 class="card-section__title">Explore the Wiki</h2>
        <p class="card-section__subtitle">Find what you need quickly</p>
    </div>

    <div class="card-grid">
        <a href="/wiki/getting-started" class="card">
            <div class="card__icon">🚀</div>
            <div class="card__title">Getting Started</div>
            <p class="card__desc">New to the server? Start here for everything you need to know before jumping in.</p>
            <span class="card__cta">Read more →</span>
        </a>
        <a href="/wiki/rules" class="card">
            <div class="card__icon">📜</div>
            <div class="card__title">Rules</div>
            <p class="card__desc">Server rules and community guidelines to keep things fun and fair for everyone.</p>
            <span class="card__cta">Read more →</span>
        </a>
        <a href="/wiki/tech" class="card">
            <div class="card__icon">⚙️</div>
            <div class="card__title">Tech Guides</div>
            <p class="card__desc">Redstone, automation, farms, and technical Minecraft resources.</p>
            <span class="card__cta">Read more →</span>
        </a>
        <a href="/wiki/community" class="card">
            <div class="card__icon">🌐</div>
            <div class="card__title">Community</div>
            <p class="card__desc">Player projects, events, and highlights from around the server.</p>
            <span class="card__cta">Read more →</span>
        </a>
    </div>
</section>
