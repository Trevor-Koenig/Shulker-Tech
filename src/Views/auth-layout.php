<!DOCTYPE html>
<html lang="en" data-theme="dark">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title><?= htmlspecialchars($title ?? 'Shulker Tech') ?></title>
    <link rel="stylesheet" href="/css/fonts.css">
    <link rel="stylesheet" href="/css/main.css">
    <script src="/js/theme.js"></script>
</head>
<body>
    <?= $content ?>
</body>
</html>
