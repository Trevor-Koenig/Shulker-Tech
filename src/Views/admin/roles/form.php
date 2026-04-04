<?php
use Trevor\ShulkerTech\Csrf;

$activeSection = 'roles';
$isEdit = $role !== null;
$action = $isEdit ? '/roles/' . (int) $role['id'] . '/edit' : '/roles/create';
ob_start();
?>
<div class="admin-page">
    <div class="admin-page__header">
        <div>
            <h1 class="admin-page__title"><?= $isEdit ? 'Edit Role' : 'Add Role' ?></h1>
        </div>
        <a href="/roles" class="btn btn--ghost">← Back</a>
    </div>

    <div class="admin-form-card">
        <form method="POST" action="<?= $action ?>" class="form">
            <?= Csrf::tokenField() ?>

            <div class="form__group">
                <label class="form__label" for="name">Name <span class="form__required">*</span></label>
                <input type="text" id="name" name="name" class="form__input" required
                    value="<?= htmlspecialchars($role['name'] ?? '') ?>">
            </div>

            <div class="form__group">
                <label class="form__label" for="description">Description</label>
                <input type="text" id="description" name="description" class="form__input"
                    value="<?= htmlspecialchars($role['description'] ?? '') ?>">
            </div>

            <?php if (!empty($permissions)): ?>
                <div class="form__group">
                    <label class="form__label">Permissions</label>
                    <div class="form__check-group form__check-group--cols">
                        <?php foreach ($permissions as $perm): ?>
                            <label class="form__checkbox">
                                <input type="checkbox" name="permissions[]" value="<?= (int) $perm['id'] ?>"
                                    <?= in_array($perm['name'], $rolePerms ?? [], true) ? 'checked' : '' ?>>
                                <span><?= htmlspecialchars($perm['name']) ?></span>
                            </label>
                        <?php endforeach; ?>
                    </div>
                </div>
            <?php endif; ?>

            <div class="form__actions">
                <button type="submit" class="btn btn--primary"><?= $isEdit ? 'Save Changes' : 'Add Role' ?></button>
                <a href="/roles" class="btn btn--ghost">Cancel</a>
            </div>
        </form>
    </div>
</div>
<?php
$content = ob_get_clean();
require __DIR__ . '/../layout.php';
