<?php
use Trevor\ShulkerTech\Csrf;

$activeSection = 'servers';
$isEdit = $server !== null;
$action = $isEdit ? '/servers/' . (int) $server['id'] . '/edit' : '/servers/create';
ob_start();
?>
<div class="admin-page">
    <div class="admin-page__header">
        <div>
            <h1 class="admin-page__title"><?= $isEdit ? 'Edit Server' : 'Add Server' ?></h1>
        </div>
        <a href="/servers" class="btn btn--ghost">← Back</a>
    </div>

    <div class="admin-form-card">
        <form method="POST" action="<?= $action ?>" class="form">
            <?= Csrf::tokenField() ?>

            <div class="form__row">
                <div class="form__group">
                    <label class="form__label" for="name">Name <span class="form__required">*</span></label>
                    <input type="text" id="name" name="name" class="form__input" required
                        value="<?= htmlspecialchars($server['name'] ?? '') ?>">
                </div>
                <div class="form__group">
                    <label class="form__label" for="slug">Slug <span class="form__required">*</span></label>
                    <input type="text" id="slug" name="slug" class="form__input" required pattern="[a-z0-9\-]+"
                        value="<?= htmlspecialchars($server['slug'] ?? '') ?>">
                </div>
            </div>

            <div class="form__group">
                <label class="form__label" for="description">Description</label>
                <textarea id="description" name="description" class="form__input form__textarea"
                    rows="2"><?= htmlspecialchars($server['description'] ?? '') ?></textarea>
            </div>

            <div class="form__row">
                <div class="form__group">
                    <label class="form__label" for="host">Host <span class="form__required">*</span></label>
                    <input type="text" id="host" name="host" class="form__input" required
                        value="<?= htmlspecialchars($server['host'] ?? '') ?>">
                </div>
                <div class="form__group form__group--sm">
                    <label class="form__label" for="port">Port</label>
                    <input type="number" id="port" name="port" class="form__input" min="1" max="65535"
                        value="<?= (int) ($server['port'] ?? 25565) ?>">
                </div>
            </div>

            <div class="form__row">
                <div class="form__group form__group--sm">
                    <label class="form__label" for="display_order">Display Order</label>
                    <input type="number" id="display_order" name="display_order" class="form__input" min="0"
                        value="<?= (int) ($server['display_order'] ?? 0) ?>">
                </div>
                <div class="form__group form__group--check">
                    <label class="form__checkbox">
                        <input type="checkbox" name="is_active" value="1" <?= !empty($server['is_active']) ? 'checked' : '' ?>>
                        <span>Active</span>
                    </label>
                </div>
            </div>

            <div class="form__actions">
                <button type="submit" class="btn btn--primary"><?= $isEdit ? 'Save Changes' : 'Add Server' ?></button>
                <a href="/servers" class="btn btn--ghost">Cancel</a>
            </div>
        </form>
    </div>
</div>
<?php
$content = ob_get_clean();
require __DIR__ . '/../layout.php';
