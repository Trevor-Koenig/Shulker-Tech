-- ============================================================
-- Shulker Tech — Initial Schema
-- Run: docker exec -i shulker-tech-db-1 mariadb -u root -p<password> shulkertech < database/001_schema.sql
-- ============================================================

SET FOREIGN_KEY_CHECKS = 0;

-- ------------------------------------------------------------
-- Servers
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `servers` (
    `id`            INT UNSIGNED    NOT NULL AUTO_INCREMENT,
    `name`          VARCHAR(100)    NOT NULL,
    `slug`          VARCHAR(64)     NOT NULL UNIQUE,
    `description`   TEXT,
    `host`          VARCHAR(255)    NOT NULL,
    `port`          SMALLINT UNSIGNED NOT NULL DEFAULT 25565,
    `display_order` TINYINT UNSIGNED NOT NULL DEFAULT 0,
    `is_active`     TINYINT(1)      NOT NULL DEFAULT 1,
    `created_at`    TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at`    TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ------------------------------------------------------------
-- Users
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `users` (
    `id`            INT UNSIGNED    NOT NULL AUTO_INCREMENT,
    `username`      VARCHAR(50)     NOT NULL UNIQUE,
    `email`         VARCHAR(255)    NOT NULL UNIQUE,
    `password_hash` VARCHAR(255)    NOT NULL,
    `is_active`     TINYINT(1)      NOT NULL DEFAULT 1,
    `created_at`    TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at`    TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ------------------------------------------------------------
-- Roles
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `roles` (
    `id`          INT UNSIGNED  NOT NULL AUTO_INCREMENT,
    `name`        VARCHAR(50)   NOT NULL UNIQUE,
    `description` VARCHAR(255),
    `created_at`  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ------------------------------------------------------------
-- Permissions
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `permissions` (
    `id`          INT UNSIGNED  NOT NULL AUTO_INCREMENT,
    `name`        VARCHAR(100)  NOT NULL UNIQUE,
    `description` VARCHAR(255),
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ------------------------------------------------------------
-- Role ↔ Permission pivot
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `role_permissions` (
    `role_id`       INT UNSIGNED NOT NULL,
    `permission_id` INT UNSIGNED NOT NULL,
    PRIMARY KEY (`role_id`, `permission_id`),
    FOREIGN KEY (`role_id`)       REFERENCES `roles`(`id`)       ON DELETE CASCADE,
    FOREIGN KEY (`permission_id`) REFERENCES `permissions`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ------------------------------------------------------------
-- User ↔ Role pivot
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `user_roles` (
    `user_id` INT UNSIGNED NOT NULL,
    `role_id` INT UNSIGNED NOT NULL,
    PRIMARY KEY (`user_id`, `role_id`),
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`role_id`) REFERENCES `roles`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================
-- Seed: Permissions
-- ============================================================
INSERT IGNORE INTO `permissions` (`name`, `description`) VALUES
    ('admin.access',   'Can log in to the admin panel'),
    ('servers.view',   'Can view the server list in admin'),
    ('servers.create', 'Can add new servers'),
    ('servers.edit',   'Can edit existing servers'),
    ('servers.delete', 'Can delete servers'),
    ('users.view',     'Can view the user list'),
    ('users.create',   'Can create new users'),
    ('users.edit',     'Can edit existing users'),
    ('users.delete',   'Can delete users'),
    ('roles.view',     'Can view the role list'),
    ('roles.create',   'Can create new roles'),
    ('roles.edit',     'Can edit existing roles'),
    ('roles.delete',   'Can delete roles');

-- ============================================================
-- Seed: Roles
-- ============================================================
INSERT IGNORE INTO `roles` (`id`, `name`, `description`) VALUES
    (1, 'Super Admin', 'Full access to everything'),
    (2, 'Admin',       'Full access except role management'),
    (3, 'Moderator',   'Read-only access to admin panel');

-- ============================================================
-- Seed: Role → Permission assignments
-- ============================================================

-- Super Admin: all permissions
INSERT IGNORE INTO `role_permissions` (`role_id`, `permission_id`)
    SELECT 1, `id` FROM `permissions`;

-- Admin: everything except roles.create, roles.edit, roles.delete
INSERT IGNORE INTO `role_permissions` (`role_id`, `permission_id`)
    SELECT 2, `id` FROM `permissions`
    WHERE `name` NOT IN ('roles.create', 'roles.edit', 'roles.delete');

-- Moderator: admin.access, servers.view, users.view
INSERT IGNORE INTO `role_permissions` (`role_id`, `permission_id`)
    SELECT 3, `id` FROM `permissions`
    WHERE `name` IN ('admin.access', 'servers.view', 'users.view');
