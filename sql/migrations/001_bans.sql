-- 001: блокировки, общие для всех инстансов сервера.
--
-- До этой таблицы MultiTierBanService держал баны только в памяти: рестарт
-- снимал все блокировки разом, а при двух инстансах забаненный на одном
-- спокойно заходил на другой.
--
-- Индексы стоят по каждому идентификатору отдельно: проверка входящего
-- подключения ищет совпадение по любому из них, и без индексов это полный
-- скан таблицы банов на КАЖДЫЙ вход игрока.
-- flovmp:ignore-errors 1050,1061

CREATE TABLE IF NOT EXISTS `bans` (
  `id` VARCHAR(32) NOT NULL PRIMARY KEY,
  `account_id` INT UNSIGNED NOT NULL DEFAULT 0,
  `username` VARCHAR(64) NOT NULL,
  `ip` VARCHAR(45) DEFAULT NULL,
  `social_club` VARCHAR(128) DEFAULT NULL,
  `hwid_hash` VARCHAR(128) DEFAULT NULL,
  `mac_address` VARCHAR(64) DEFAULT NULL,
  `flags` INT NOT NULL DEFAULT 0 COMMENT 'BanFlags: Account/Ip/SocialClub/Hwid/Mac/Subnet',
  `admin_username` VARCHAR(64) NOT NULL,
  `reason` VARCHAR(255) NOT NULL,
  `banned_at_utc` DATETIME NOT NULL,
  `expires_at_utc` DATETIME DEFAULT NULL COMMENT 'NULL = навсегда',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `updated_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  INDEX `idx_bans_account` (`account_id`),
  INDEX `idx_bans_username` (`username`),
  INDEX `idx_bans_ip` (`ip`),
  INDEX `idx_bans_social` (`social_club`),
  INDEX `idx_bans_hwid` (`hwid_hash`),
  INDEX `idx_bans_mac` (`mac_address`),
  INDEX `idx_bans_active` (`is_active`),
  -- Дозагрузка чужих банов между инстансами идёт по времени изменения:
  -- инстанс спрашивает «что поменялось после такого-то момента».
  INDEX `idx_bans_updated` (`updated_at_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
