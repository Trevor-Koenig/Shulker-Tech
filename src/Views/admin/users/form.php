<?php
use Trevor\ShulkerTech\Csrf;

$activeSection = 'users';
$isEdit        = $user !== null;
$action        = $isEdit ? '/users/' . (int) $user['id'] . '/edit' : '/users/create';
$userRoles     = $userRoles ?? [];
ob_start();
?>
<div class="admin-page">
    <div class="admin-page__header">
        <div>
            <h1 class="admin-page__title"><?= $isEdit ? 'Edit User' : 'Add User' ?></h1>
        </div>
        <a href="/users" class="btn btn--ghost">← Back</a>
    </div>

    <div class="admin-form-card">
        <form method="POST" action="<?= $action ?>" class="form">
            <?= Csrf::tokenField() ?>

            <div class="form__row">
                <div class="form__group">
                    <label class="form__label" for="username">Username <span class="form__required">*</span></label>
                    <input type="text" id="username" name="username" class="form__input" required
                        value="<?= htmlspecialchars($user['username'] ?? '') ?>">
                </div>
                <div class="form__group">
                    <label class="form__label" for="email">Email <span class="form__required">*</span></label>
                    <input type="email" id="email" name="email" class="form__input" required
                        value="<?= htmlspecialchars($user['email'] ?? '') ?>">
                </div>
            </div>

            <div class="form__group">
                <label class="form__label" for="password">
                    Password <?= $isEdit ? '<span class="form__hint">(leave blank to keep current)</span>' : '<span class="form__required">*</span>' ?>
                </label>
                <input type="password" id="password" name="password" class="form__input"
                    <?= $isEdit ? '' : 'required' ?>>
            </div>

            <div class="form__group form__group--check">
                <label class="form__checkbox">
                    <input type="checkbox" name="is_active" value="1"
                        <?= !$isEdit || !empty($user['is_active']) ? 'checked' : '' ?>>
                    <span>Active</span>
                </label>
            </div>

            <?php if (!empty($roles)): ?>
            <div class="form__group">
                <label class="form__label">Roles</label>
                <div class="form__check-group">
                    <?php foreach ($roles as $role): ?>
                    <label class="form__checkbox">
                        <input type="checkbox" name="roles[]" value="<?= (int) $role['id'] ?>"
                            <?= in_array((int) $role['id'], array_map('intval', $userRoles), true) ? 'checked' : '' ?>>
                        <span><?= htmlspecialchars($role['name']) ?></span>
                    </label>
                    <?php endforeach; ?>
                </div>
            </div>
            <?php endif; ?>

            <div class="form__actions">
                <button type="submit" class="btn btn--primary"><?= $isEdit ? 'Save Changes' : 'Add User' ?></button>
                <a href="/users" class="btn btn--ghost">Cancel</a>
            </div>
        </form>
    </div>
</div>
<?php
$content = ob_get_clean();
require __DIR__ . '/../layout.php';
