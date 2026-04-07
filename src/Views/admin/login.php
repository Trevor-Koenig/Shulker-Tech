<?php
use Trevor\ShulkerTech\Csrf;

ob_start();
?>
<div class="auth-page">
    <div class="auth-card">
        <div class="auth-card__brand">
            <span class="nav__logo-shulker">SHULKER</span><span class="nav__logo-tech"> TECH</span>
            <span class="auth-card__sub">Admin Panel</span>
        </div>

        <?php if (!empty($error)): ?>
            <div class="alert alert--danger"><?= htmlspecialchars($error) ?></div>
        <?php endif; ?>

        <form method="POST" action="/login" class="form">
            <?= Csrf::tokenField() ?>
            <div class="form__group">
                <label class="form__label" for="email">Email</label>
                <input type="email" id="email" name="email" class="form__input" required autofocus
                    value="<?= htmlspecialchars($_POST['email'] ?? '') ?>">
            </div>
            <div class="form__group">
                <label class="form__label" for="password">Password</label>
                <input type="password" id="password" name="password" class="form__input" required>
            </div>
            <button type="submit" class="btn btn--primary btn--full">Sign In</button>
        </form>
    </div>
</div>
<?php
$content = ob_get_clean();
require __DIR__ . '/../auth-layout.php';
