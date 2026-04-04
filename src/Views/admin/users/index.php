<?php
use Trevor\ShulkerTech\Csrf;

$activeSection = 'users';
$currentUserId = (int) (\Trevor\ShulkerTech\Auth::user()['id'] ?? 0);
ob_start();
?>
<div class="admin-page">
    <div class="admin-page__header">
        <div>
            <h1 class="admin-page__title">Users</h1>
            <p class="admin-page__subtitle"><?= count($users) ?> user<?= count($users) !== 1 ? 's' : '' ?></p>
        </div>
        <a href="/users/create" class="btn btn--primary">Add User</a>
    </div>

    <?php if (empty($users)): ?>
        <div class="admin-empty">No users found.</div>
    <?php else: ?>
        <div class="admin-table-wrap">
            <table class="admin-table">
                <thead>
                    <tr>
                        <th>Username</th>
                        <th>Email</th>
                        <th>Status</th>
                        <th>Created</th>
                        <th></th>
                    </tr>
                </thead>
                <tbody>
                    <?php foreach ($users as $u): ?>
                        <tr>
                            <td>
                                <strong><?= htmlspecialchars($u['username']) ?></strong>
                                <?php if ((int) $u['id'] === $currentUserId): ?>
                                    <span class="badge badge--info">You</span>
                                <?php endif; ?>
                            </td>
                            <td><?= htmlspecialchars($u['email']) ?></td>
                            <td>
                                <span class="badge <?= $u['is_active'] ? 'badge--success' : 'badge--muted' ?>">
                                    <?= $u['is_active'] ? 'Active' : 'Inactive' ?>
                                </span>
                            </td>
                            <td><?= htmlspecialchars(substr($u['created_at'] ?? '', 0, 10)) ?></td>
                            <td class="admin-table__actions">
                                <a href="/users/<?= (int) $u['id'] ?>/edit" class="btn btn--ghost btn--sm">Edit</a>
                                <?php if ((int) $u['id'] !== $currentUserId): ?>
                                    <form method="POST" action="/users/<?= (int) $u['id'] ?>/delete" class="inline-form"
                                        onsubmit="return confirm('Delete <?= htmlspecialchars(addslashes($u['username'])) ?>?')">
                                        <?= Csrf::tokenField() ?>
                                        <button type="submit" class="btn btn--danger btn--sm">Delete</button>
                                    </form>
                                <?php endif; ?>
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
