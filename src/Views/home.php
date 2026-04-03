<?php
use Trevor\ShulkerTech\Health;
use Trevor\ShulkerTech\Cache;
use Trevor\ShulkerTech\ServerQuery;
use Trevor\ShulkerTech\Models\Server;
use Trevor\ShulkerTech\Models\Setting;

$bluemapUrls    = array_values(array_filter(array_map('trim', explode("\n", Setting::get('bluemap_url')))));
$initialIndex   = !empty($bluemapUrls) ? array_rand($bluemapUrls) : 0;
$rawBluemapUrl  = $bluemapUrls[$initialIndex] ?? '';
$bluemapUrl     = htmlspecialchars($rawBluemapUrl);
$bluemapOnline  = $rawBluemapUrl !== '' && Health::isReachable($rawBluemapUrl);
$bluemapUrlsJson = htmlspecialchars(json_encode(array_values($bluemapUrls)), ENT_QUOTES);

$homeUrl = htmlspecialchars($_ENV['HOME_URL'] ?? '/');
$wikiUrl = htmlspecialchars($_ENV['WIKI_URL'] ?? '/wiki');

// Load active servers with cached status
$servers = Server::allActive();
$serverStatuses = [];
foreach ($servers as $srv) {
    $cacheKey = 'server_status_' . $srv['id'];
    $status = Cache::get($cacheKey);
    if ($status === null) {
        $status = ServerQuery::query($srv['host'], (int) ($srv['port'] ?? 25565));
        Cache::set($cacheKey, $status ?? ['online' => false], 30);
    }
    $serverStatuses[$srv['id']] = $status ?? ['online' => false];
}

$discordServerId = htmlspecialchars($_ENV['DISCORD_SERVER_ID'] ?? '');
?>

<section class="hero">
    <?php if ($bluemapOnline): ?>
        <div class="hero__map" id="heroMap" data-maps="<?= $bluemapUrlsJson ?>" data-current="<?= $initialIndex ?>">
            <iframe src="<?= $bluemapUrl ?>" class="hero__map-frame" id="heroMapFrame" title="Live world map"
                sandbox="allow-scripts allow-same-origin" tabindex="-1" aria-hidden="true"></iframe>
            <div class="hero__map-overlay"></div>
            <?php if (count($bluemapUrls) > 1): ?>
                <div class="hero__map-dots" id="heroMapDots">
                    <?php foreach ($bluemapUrls as $i => $url): ?>
                        <button
                            class="hero__map-dot <?= $i === $initialIndex ? 'hero__map-dot--active' : '' ?>"
                            data-index="<?= $i ?>"
                            aria-label="Map <?= $i + 1 ?>"
                        ></button>
                    <?php endforeach; ?>
                </div>
            <?php endif; ?>
        </div>
    <?php endif; ?>

    <div class="hero__content">
        <p class="hero__eyebrow">Welcome to the Server</p>
        <a href="<?= $homeUrl ?>">
            <h1 class="hero__title"><span>SHULKER</span> TECH</h1>
        </a>
        <p class="hero__subtitle">
            One Site to rule them all, One Site to find them, One Site to bring them all, and in the darkness bind them.
        </p>
        <a href="<?= $wikiUrl ?>" <?= ($activePage ?? '') === 'wiki' ? 'class="active"' : '' ?>"
            class="btn btn--primary">Browse the Wiki</a>
    </div>
</section>

<?php if (!empty($servers)): ?>
    <section class="server-status">
        <div class="server-status__header">
            <h2 class="server-status__title">Server Status</h2>
        </div>
        <div class="server-status__grid">
            <?php foreach ($servers as $srv):
                $s = $serverStatuses[$srv['id']];
                $online = !empty($s['online']);
                ?>
                <div class="server-card <?= $online ? 'server-card--online' : 'server-card--offline' ?>"
                    data-server-id="<?= (int) $srv['id'] ?>">
                    <div class="server-card__header">
                        <span class="server-card__indicator"></span>
                        <span class="server-card__name"><?= htmlspecialchars($srv['name']) ?></span>
                        <span class="server-card__status-label"><?= $online ? 'Online' : 'Offline' ?></span>
                    </div>

                    <?php if ($online): ?>
                        <div class="server-card__motd"><?= htmlspecialchars($s['motd'] ?? '') ?></div>

                        <div class="server-card__meta">
                            <span class="server-card__players">
                                <?= (int) ($s['players']['online'] ?? 0) ?>/<?= (int) ($s['players']['max'] ?? 0) ?> players
                            </span>
                            <span class="server-card__version"><?= htmlspecialchars($s['version'] ?? '') ?></span>
                        </div>

                        <?php if (!empty($s['players']['names'])): ?>
                            <div class="server-card__player-list">
                                <?php foreach ($s['players']['names'] as $pname): ?>
                                    <span class="server-card__player">
                                        <img src="https://mc-heads.net/avatar/<?= urlencode($pname) ?>/16" alt="" width="16" height="16"
                                            loading="lazy">
                                        <?= htmlspecialchars($pname) ?>
                                    </span>
                                <?php endforeach; ?>
                            </div>
                        <?php endif; ?>

                        <div class="server-card__address">
                            <?= htmlspecialchars($srv['host']) ?>             <?= ($srv['port'] != 25565) ? ':' . (int) $srv['port'] : '' ?>
                        </div>
                    <?php else: ?>
                        <div class="server-card__offline-msg">Server is currently offline</div>
                    <?php endif; ?>
                </div>
            <?php endforeach; ?>
        </div>
    </section>
<?php endif; ?>

<script src="/js/events.js" defer></script>
<script>
    document.addEventListener('DOMContentLoaded', function () {
        SseClient.on('server-status', function (servers) {
            servers.forEach(function (srv) {
                var card = document.querySelector('[data-server-id="' + srv.id + '"]');
                if (!card) return;

                var s = srv.status;
                var online = !!s.online;
                var players = s.players || {};
                var names = players.names || [];

                card.className = 'server-card ' + (online ? 'server-card--online' : 'server-card--offline');

                var playerListHtml = names.length
                    ? '<div class="server-card__player-list">'
                    + names.map(function (name) {
                        return '<span class="server-card__player">'
                            + '<img src="https://mc-heads.net/avatar/' + encodeURIComponent(name) + '/16"'
                            + ' alt="" width="16" height="16" loading="lazy">'
                            + escHtml(name) + '</span>';
                    }).join('')
                    + '</div>'
                    : '';

                card.innerHTML = online
                    ? '<div class="server-card__header">'
                    + '<span class="server-card__indicator"></span>'
                    + '<span class="server-card__name">' + escHtml(srv.name) + '</span>'
                    + '<span class="server-card__status-label">Online</span>'
                    + '</div>'
                    + '<div class="server-card__motd">' + escHtml(s.motd || '') + '</div>'
                    + '<div class="server-card__meta">'
                    + '<span class="server-card__players">' + (players.online || 0) + '/' + (players.max || 0) + ' players</span>'
                    + '<span class="server-card__version">' + escHtml(s.version || '') + '</span>'
                    + '</div>'
                    + playerListHtml
                    : '<div class="server-card__header">'
                    + '<span class="server-card__indicator"></span>'
                    + '<span class="server-card__name">' + escHtml(srv.name) + '</span>'
                    + '<span class="server-card__status-label">Offline</span>'
                    + '</div>'
                    + '<div class="server-card__offline-msg">Server is currently offline</div>';
            });
        });

        function escHtml(str) {
            return String(str)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;');
        }

        // Map switcher
        var heroMap = document.getElementById('heroMap');
        if (heroMap) {
            var frame   = document.getElementById('heroMapFrame');
            var maps    = JSON.parse(heroMap.dataset.maps || '[]');
            var current = parseInt(heroMap.dataset.current, 10) || 0;
            var dots    = document.querySelectorAll('.hero__map-dot');
            var timer   = null;

            function switchTo(index) {
                current = (index + maps.length) % maps.length;
                frame.src = maps[current];
                dots.forEach(function (d, i) {
                    d.classList.toggle('hero__map-dot--active', i === current);
                });
                resetTimer();
            }

            function resetTimer() {
                clearInterval(timer);
                if (maps.length > 1) {
                    timer = setInterval(function () {
                        switchTo(current + 1);
                    }, 30000);
                }
            }

            dots.forEach(function (dot) {
                dot.addEventListener('click', function () {
                    switchTo(parseInt(dot.dataset.index, 10));
                });
            });

            resetTimer();
        }
    });
</script>

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

<?php if ($discordServerId !== ''): ?>
    <section class="discord-section">
        <div class="discord-section__inner">
            <div class="discord-section__header">
                <h2 class="discord-section__title">Join Our Discord</h2>
                <p class="discord-section__subtitle">
                    Hang out with the community, ask questions, share your builds,
                    and stay up to date with server news and events.
                </p>
                <a href="https://discord.gg/<?= $discordServerId ?>" target="_blank" rel="noopener noreferrer"
                    class="btn btn--primary discord-section__cta">Open in Discord</a>
            </div>
            <div class="discord-widget-wrap">
                <iframe src="https://discord.com/widget?id=<?= $discordServerId ?>&theme=dark" width="350" height="500"
                    allowtransparency="true" frameborder="0"
                    sandbox="allow-popups allow-popups-to-escape-sandbox allow-same-origin allow-scripts"
                    title="Discord server widget" loading="lazy"></iframe>
            </div>
        </div>
    </section>
<?php endif; ?>