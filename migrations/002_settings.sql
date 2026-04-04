-- ============================================================
-- Shulker Tech — Settings Table
-- ============================================================

CREATE TABLE IF NOT EXISTS `settings` (
    `key`         VARCHAR(100) NOT NULL,
    `value`       TEXT,
    `label`       VARCHAR(100) NOT NULL,
    `description` VARCHAR(255),
    PRIMARY KEY (`key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO `settings` (`key`, `value`, `label`, `description`) VALUES
    ('bluemap_url', '', 'BlueMap URL', 'URL to your BlueMap server. Used as the live hero background on the home page. Leave blank to disable.');
