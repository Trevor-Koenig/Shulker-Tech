<?php
$title = '404 — Shulker Tech';
$activePage = '';
ob_start();
?>
<div style="text-align:center; padding: 4rem 2rem;">
    <h1>404</h1>
    <p>The page you're looking for doesn't exist.</p>
    <a href="/" class="btn btn--outline">Go Home</a>
</div>
<?php
$content = ob_get_clean();
require __DIR__ . '/layout.php';
