<?php

declare(strict_types=1);

require_once __DIR__ . '/../vendor/autoload.php';

$title = 'Shulker Tech';
$activePage = 'home';

ob_start();
require __DIR__ . '/../src/Views/home.php';
$content = ob_get_clean();

require __DIR__ . '/../src/Views/layout.php';
