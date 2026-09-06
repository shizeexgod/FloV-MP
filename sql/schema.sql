-- =============================================================================
-- FloV:MP / Держава Онлайн — База данных (MariaDB / MySQL)
-- =============================================================================
-- Проект: «Держава Онлайн»
-- Движок: FloV:MP (alt:V 16.4.39 runtime)
-- Кодировка: utf8mb4_unicode_ci
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `derzhava_rp`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `derzhava_rp`;

-- -----------------------------------------------------------------------------
-- 1. Таблица аккаунтов (Accounts)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `accounts` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `username` VARCHAR(64) NOT NULL UNIQUE,
  `password_hash` VARCHAR(255) NOT NULL,
  `salt` VARCHAR(255) NOT NULL,
  `email` VARCHAR(128) DEFAULT NULL,
  `hwid` VARCHAR(128) DEFAULT NULL,
  `social_club` VARCHAR(128) DEFAULT NULL,
  `last_ip` VARCHAR(45) DEFAULT NULL,
  `admin_level` TINYINT UNSIGNED NOT NULL DEFAULT 0,
  `is_banned` TINYINT(1) NOT NULL DEFAULT 0,
  `ban_reason` VARCHAR(255) DEFAULT NULL,
  `ban_until_utc` DATETIME DEFAULT NULL,
  `mute_until_utc` DATETIME DEFAULT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `last_login_at` DATETIME DEFAULT NULL,
  INDEX `idx_acc_username` (`username`),
  INDEX `idx_acc_hwid` (`hwid`),
  INDEX `idx_acc_social_club` (`social_club`),
  INDEX `idx_acc_admin` (`admin_level`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- 2. Таблица персонажей (Characters)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `characters` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `account_id` INT UNSIGNED NOT NULL,
  `first_name` VARCHAR(32) NOT NULL,
  `last_name` VARCHAR(32) NOT NULL,
  `gender` TINYINT UNSIGNED NOT NULL DEFAULT 0 COMMENT '0: Male, 1: Female',
  `skin_model` VARCHAR(64) NOT NULL DEFAULT 'mp_m_freemode_01',
  `cash` BIGINT NOT NULL DEFAULT 1500,
  `bank` BIGINT NOT NULL DEFAULT 5000,
  `pos_x` FLOAT NOT NULL DEFAULT 198.7,
  `pos_y` FLOAT NOT NULL DEFAULT -935.4,
  `pos_z` FLOAT NOT NULL DEFAULT 30.7,
  `heading` FLOAT NOT NULL DEFAULT 142.5,
  `dimension` INT NOT NULL DEFAULT 0,
  `health` SMALLINT UNSIGNED NOT NULL DEFAULT 200,
  `armor` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  `jail_time_seconds` INT NOT NULL DEFAULT 0,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT `fk_char_account` FOREIGN KEY (`account_id`) REFERENCES `accounts` (`id`) ON DELETE CASCADE,
  INDEX `idx_char_account` (`account_id`),
  INDEX `idx_char_fullname` (`first_name`, `last_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- 3. Таблица инвентаря (Character Inventories)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `character_inventory` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `character_id` INT UNSIGNED NOT NULL,
  `slot` SMALLINT UNSIGNED NOT NULL,
  `item_id` VARCHAR(64) NOT NULL,
  `count` INT UNSIGNED NOT NULL DEFAULT 1,
  `durability` FLOAT NOT NULL DEFAULT 100.0,
  `metadata_json` JSON DEFAULT NULL,
  CONSTRAINT `fk_inv_character` FOREIGN KEY (`character_id`) REFERENCES `characters` (`id`) ON DELETE CASCADE,
  UNIQUE KEY `uq_char_slot` (`character_id`, `slot`),
  INDEX `idx_inv_char` (`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- 4. Таблица транспорта (Vehicles)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `vehicles` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `owner_character_id` INT UNSIGNED NOT NULL,
  `model` VARCHAR(64) NOT NULL,
  `plate` VARCHAR(16) NOT NULL UNIQUE,
  `color_primary` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  `color_secondary` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  `pos_x` FLOAT NOT NULL DEFAULT 0.0,
  `pos_y` FLOAT NOT NULL DEFAULT 0.0,
  `pos_z` FLOAT NOT NULL DEFAULT 0.0,
  `heading` FLOAT NOT NULL DEFAULT 0.0,
  `dimension` INT NOT NULL DEFAULT 0,
  `fuel` FLOAT NOT NULL DEFAULT 100.0,
  `engine_health` FLOAT NOT NULL DEFAULT 1000.0,
  `body_health` FLOAT NOT NULL DEFAULT 1000.0,
  `is_locked` TINYINT(1) NOT NULL DEFAULT 1,
  `engine_on` TINYINT(1) NOT NULL DEFAULT 0,
  `is_impounded` TINYINT(1) NOT NULL DEFAULT 0,
  `trunk_json` JSON DEFAULT NULL,
  `glovebox_json` JSON DEFAULT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT `fk_veh_owner` FOREIGN KEY (`owner_character_id`) REFERENCES `characters` (`id`) ON DELETE CASCADE,
  INDEX `idx_veh_owner` (`owner_character_id`),
  INDEX `idx_veh_plate` (`plate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- 5. Таблица наказаний (Punishments)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `punishments` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `target_account_id` INT UNSIGNED DEFAULT NULL,
  `target_name` VARCHAR(64) NOT NULL,
  `admin_account_id` INT UNSIGNED DEFAULT NULL,
  `admin_name` VARCHAR(64) NOT NULL,
  `type` VARCHAR(16) NOT NULL COMMENT 'BAN, MUTE, JAIL, WARN, KICK',
  `reason` VARCHAR(255) NOT NULL,
  `duration_seconds` INT NOT NULL DEFAULT 0,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `expires_at` DATETIME DEFAULT NULL,
  INDEX `idx_punish_target` (`target_name`),
  INDEX `idx_punish_admin` (`admin_name`),
  INDEX `idx_punish_active` (`is_active`),
  INDEX `idx_punish_type` (`type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- 6. Аудит-лог действий администрации (Admin Audit Logs)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `admin_audit_logs` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `admin_account_id` INT UNSIGNED DEFAULT NULL,
  `admin_name` VARCHAR(64) NOT NULL,
  `admin_rank` VARCHAR(32) NOT NULL,
  `command` VARCHAR(32) NOT NULL,
  `args` TEXT DEFAULT NULL,
  `target_player` VARCHAR(64) DEFAULT NULL,
  `ip_address` VARCHAR(45) DEFAULT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX `idx_audit_admin` (`admin_name`),
  INDEX `idx_audit_cmd` (`command`),
  INDEX `idx_audit_date` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- 7. История банковских транзакций (Bank Transactions)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `bank_transactions` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `sender_character_id` INT UNSIGNED DEFAULT NULL,
  `receiver_character_id` INT UNSIGNED DEFAULT NULL,
  `amount` BIGINT NOT NULL,
  `type` VARCHAR(24) NOT NULL COMMENT 'DEPOSIT, WITHDRAW, TRANSFER, SALARY, PURCHASE',
  `description` VARCHAR(255) DEFAULT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX `idx_bank_sender` (`sender_character_id`),
  INDEX `idx_bank_receiver` (`receiver_character_id`),
  INDEX `idx_bank_created` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;