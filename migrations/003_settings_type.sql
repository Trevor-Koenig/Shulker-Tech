-- ============================================================
-- Shulker Tech — Add type column to settings
-- ============================================================

ALTER TABLE `settings`
    ADD COLUMN `type` VARCHAR(20) NOT NULL DEFAULT 'text' AFTER `value`;

UPDATE `settings`
SET
    `type`        = 'textarea',
    `label`       = 'BlueMap URLs',
    `description` = 'One URL per line. A random one will be shown as the hero background on each page load. Leave blank to disable.'
WHERE `key` = 'bluemap_url';
