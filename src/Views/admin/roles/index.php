<?php
use Trevor\ShulkerTech\Csrf;

$activeSection = 'roles';
ob_start();
?>
<div class="admin-page">
    <div class="admin-page__header">
        <div>
            <h1 class="admin-page__title">Roles</h1>
            <p class="admin-page__subtitle"><?= count($roles) ?> role<?= count($roles) !== 1 ? 's' : '' ?></p>
        </div>
        <a href="/roles/create" class="btn btn--primary">Add Role</a>
    </div>

    <?php if (empty($roles)): ?>
        <div class="admin-empty">No roles configured.</div>
    <?php else: ?>
        <div class="admin-table-wrap">
            <table class="admin-table">
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Description</th>
                        <th></th>
                    </tr>
                </thead>
                <tbody>
                    <?php foreach ($roles as $role): ?>
                        <tr>
                            <td><strong><?= htmlspecialchars($role['name']) ?></strong></td>
                            <td><?= htmlspecialchars($role['description'] ?? '') ?></td>
                            <td class="admin-table__actions">
                                <a href="/roles/<?= (int) $role['id'] ?>/edit" class="btn btn--ghost btn--sm">Edit</a>
                                <form method="POST" action="/roles/<?= (int) $role['id'] ?>/delete" class="inline-form"
                                    onsubmit="return confirm('Delete <?= htmlspecialchars(addslashes($role['name'])) ?>?')">
                                    <?= Csrf::tokenField() ?>
                                    <button type="submit" class="btn btn--danger btn--sm">Delete</button>
                                </form>
                            </td>
                        </tr>
                    <?php endforeach; ?>
                </tbody>
            </table>
        </div>
    <?php endif; ?>
</div>
<?php
$content = ob_get_clean();
require __DIR__ . '/../layout.php';
