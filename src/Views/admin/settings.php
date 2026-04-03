<?php
use Trevor\ShulkerTech\Csrf;

$activeSection = 'settings';
ob_start();
?>
<div class="admin-page">
    <div class="admin-page__header">
        <div>
            <h1 class="admin-page__title">Settings</h1>
            <p class="admin-page__subtitle">Site-wide configuration</p>
        </div>
    </div>

    <?php if (!empty($success)): ?>
        <div class="alert alert--success"><?= htmlspecialchars($success) ?></div>
    <?php endif; ?>

    <div class="admin-form-card">
        <form method="POST" action="/settings" class="form" id="settingsForm">
            <?= Csrf::tokenField() ?>

            <?php foreach ($settings as $setting):
                $type = $setting['type'] ?? 'text';
                $key = htmlspecialchars($setting['key']);
                $value = $setting['value'] ?? '';
                ?>
                <div class="form__group">
                    <label class="form__label" for="<?= $key ?>">
                        <?= htmlspecialchars($setting['label']) ?>
                    </label>
                    <?php if (!empty($setting['description'])): ?>
                        <span class="form__hint"><?= htmlspecialchars($setting['description']) ?></span>
                    <?php endif; ?>
                    <div class="url-list" data-key="<?= $key ?>">
                        <?php foreach (array_filter(array_map('trim', explode("\n", $value))) as $url): ?>
                            <div class="url-list__row">
                                <input type="text" class="form__input url-list__input" value="<?= htmlspecialchars($url) ?>"
                                    placeholder="https://map.example.com">
                                <button type="button" class="btn btn--danger btn--sm url-list__remove"
                                    aria-label="Remove">✕</button>
                            </div>
                        <?php endforeach; ?>
                        <?php if (empty(array_filter(array_map('trim', explode("\n", $value))))): ?>
                            <div class="url-list__row">
                                <input type="text" class="form__input url-list__input" value=""
                                    placeholder="https://map.example.com">
                                <button type="button" class="btn btn--danger btn--sm url-list__remove"
                                    aria-label="Remove">✕</button>
                            </div>
                        <?php endif; ?>
                    </div>
                    <button type="button" class="btn btn--ghost btn--sm url-list__add" data-target="<?= $key ?>">+ Add
                        URL</button>
                    <textarea name="settings[<?= $key ?>]" class="url-list__hidden"
                        aria-hidden="true"><?= htmlspecialchars($value) ?></textarea>
                </div>
            <?php endforeach; ?>

            <div class="form__actions">
                <button type="submit" class="btn btn--primary">Save Settings</button>
            </div>
        </form>
    </div>
</div>

<script>
    document.addEventListener('DOMContentLoaded', function () {
        // Sync url-list inputs into hidden textarea before submit
        document.getElementById('settingsForm').addEventListener('submit', function () {
            document.querySelectorAll('.url-list').forEach(function (list) {
                var key = list.dataset.key;
                var hidden = document.querySelector('textarea[name="settings[' + key + ']"]');
                var urls = Array.from(list.querySelectorAll('.url-list__input'))
                    .map(function (i) { return i.value.trim(); })
                    .filter(Boolean);
                hidden.value = urls.join('\n');
            });
        });

        // Remove row
        document.addEventListener('click', function (e) {
            if (!e.target.classList.contains('url-list__remove')) return;
            var list = e.target.closest('.url-list');
            var rows = list.querySelectorAll('.url-list__row');
            if (rows.length > 1) {
                e.target.closest('.url-list__row').remove();
            } else {
                // Keep one empty row rather than removing the last one
                e.target.closest('.url-list__row').querySelector('.url-list__input').value = '';
            }
        });

        // Add row
        document.querySelectorAll('.url-list__add').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var list = document.querySelector('.url-list[data-key="' + btn.dataset.target + '"]');
                var row = document.createElement('div');
                row.className = 'url-list__row';
                row.innerHTML = '<input type="text" class="form__input url-list__input" placeholder="https://map.example.com">'
                    + '<button type="button" class="btn btn--danger btn--sm url-list__remove" aria-label="Remove">✕</button>';
                list.appendChild(row);
                row.querySelector('input').focus();
            });
        });
    });
</script>
<?php
$content = ob_get_clean();
require __DIR__ . '/layout.php';
