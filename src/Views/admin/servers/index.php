<?php
use Trevor\ShulkerTech\Csrf;

$activeSection = 'servers';
ob_start();
?>
<div class="admin-page">
    <div class="admin-page__header">
        <div>
            <h1 class="admin-page__title">Servers</h1>
            <p class="admin-page__subtitle"><?= count($servers) ?> server<?= count($servers) !== 1 ? 's' : '' ?>
                configured</p>
        </div>
        <a href="/servers/create" class="btn btn--primary">Add Server</a>
    </div>

    <?php if (empty($servers)): ?>
        <div class="admin-empty">No servers configured yet.</div>
    <?php else: ?>
        <div class="admin-table-wrap">
            <table class="admin-table">
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Host</th>
                        <th>Port</th>
                        <th>Status</th>
                        <th>Order</th>
                        <th></th>
                    </tr>
                </thead>
                <tbody>
                    <?php foreach ($servers as $srv): ?>
                        <tr>
                            <td>
                                <strong><?= htmlspecialchars($srv['name']) ?></strong>
                                <?php if ($srv['description']): ?>
                                    <div class="admin-table__sub"><?= htmlspecialchars($srv['description']) ?></div>
                                <?php endif; ?>
                            </td>
                            <td><?= htmlspecialchars($srv['host']) ?></td>
                            <td><?= (int) $srv['port'] ?></td>
                            <td>
                                <span class="badge <?= $srv['is_active'] ? 'badge--success' : 'badge--muted' ?>">
                                    <?= $srv['is_active'] ? 'Active' : 'Inactive' ?>
                                </span>
                            </td>
                            <td><?= (int) $srv['display_order'] ?></td>
                            <td class="admin-table__actions">
                                <a href="/servers/<?= (int) $srv['id'] ?>/edit" class="btn btn--ghost btn--sm">Edit</a>
                                <form method="POST" action="/servers/<?= (int) $srv['id'] ?>/delete" class="inline-form"
                                    onsubmit="return confirm('Delete <?= htmlspecialchars(addslashes($srv['name'])) ?>?')">
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
