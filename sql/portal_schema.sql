-- =============================================================================
-- FloV:MP SaaS Portal Database Schema (MariaDB / MySQL)
-- =============================================================================

USE `derzhava_rp`;

CREATE TABLE IF NOT EXISTS `portal_users` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `email` VARCHAR(128) NOT NULL UNIQUE,
  `username` VARCHAR(64) NOT NULL,
  `password_hash` VARCHAR(255) NOT NULL,
  `role` VARCHAR(16) NOT NULL DEFAULT 'client' COMMENT 'client, admin',
  `telegram` VARCHAR(64) DEFAULT NULL,
  `discord` VARCHAR(64) DEFAULT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX `idx_puser_email` (`email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_licenses` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `user_id` INT UNSIGNED NOT NULL,
  `license_key` VARCHAR(64) NOT NULL UNIQUE,
  `server_name` VARCHAR(128) NOT NULL DEFAULT 'My RP Server',
  `bound_ip` VARCHAR(45) NOT NULL DEFAULT '0.0.0.0',
  `plan` VARCHAR(32) NOT NULL DEFAULT 'indie' COMMENT 'indie, business, enterprise',
  `max_players` INT UNSIGNED NOT NULL DEFAULT 128,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `expires_at` DATETIME NOT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `last_verified_at` DATETIME DEFAULT NULL,
  CONSTRAINT `fk_plic_user` FOREIGN KEY (`user_id`) REFERENCES `portal_users` (`id`) ON DELETE CASCADE,
  INDEX `idx_plic_key` (`license_key`),
  INDEX `idx_plic_ip` (`bound_ip`),
  INDEX `idx_plic_active` (`is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_invoices` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `user_id` INT UNSIGNED NOT NULL,
  `license_id` INT UNSIGNED DEFAULT NULL,
  `amount_rub` INT UNSIGNED NOT NULL,
  `plan` VARCHAR(32) NOT NULL,
  `payment_method` VARCHAR(32) NOT NULL DEFAULT 'card',
  `payment_id` VARCHAR(128) DEFAULT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'pending' COMMENT 'pending, paid, cancelled',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `paid_at` DATETIME DEFAULT NULL,
  CONSTRAINT `fk_pinv_user` FOREIGN KEY (`user_id`) REFERENCES `portal_users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_launcher_builds` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `license_id` INT UNSIGNED NOT NULL,
  `project_name` VARCHAR(64) NOT NULL,
  `primary_color` VARCHAR(16) NOT NULL DEFAULT '#ff3d8a',
  `logo_url` VARCHAR(255) DEFAULT NULL,
  `build_status` VARCHAR(16) NOT NULL DEFAULT 'ready',
  `download_url` VARCHAR(255) DEFAULT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT `fk_pbuild_lic` FOREIGN KEY (`license_id`) REFERENCES `portal_licenses` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_telemetry` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `license_key` VARCHAR(64) NOT NULL,
  `players` INT UNSIGNED NOT NULL DEFAULT 0,
  `max_players` INT UNSIGNED NOT NULL DEFAULT 1500,
  `tick_rate` INT UNSIGNED NOT NULL DEFAULT 60,
  `memory_mb` INT UNSIGNED NOT NULL DEFAULT 0,
  `fps` INT UNSIGNED NOT NULL DEFAULT 60,
  `server_ip` VARCHAR(45) NOT NULL DEFAULT '127.0.0.1',
  `recorded_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX `idx_ptele_key` (`license_key`),
  INDEX `idx_ptele_time` (`recorded_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================================================
-- FloV:MP Multi-Project & Multi-Server Hierarchy (v2.0)
-- =============================================================================

CREATE TABLE IF NOT EXISTS `portal_projects` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `user_id` INT UNSIGNED NOT NULL,
  `name` VARCHAR(128) NOT NULL,
  `slug` VARCHAR(64) NOT NULL UNIQUE,
  `license_key` VARCHAR(64) NOT NULL UNIQUE,
  `plan` VARCHAR(32) NOT NULL DEFAULT 'enterprise' COMMENT 'indie, business, enterprise, lifetime',
  `max_players` INT UNSIGNED NOT NULL DEFAULT 1500,
  `api_key` VARCHAR(64) NOT NULL UNIQUE,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `expires_at` DATETIME NOT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT `fk_pproj_user` FOREIGN KEY (`user_id`) REFERENCES `portal_users` (`id`) ON DELETE CASCADE,
  INDEX `idx_pproj_slug` (`slug`),
  INDEX `idx_pproj_key` (`license_key`),
  INDEX `idx_pproj_apikey` (`api_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_servers` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `project_id` INT UNSIGNED NOT NULL,
  `environment` VARCHAR(32) NOT NULL DEFAULT 'production' COMMENT 'production, development, test',
  `name` VARCHAR(128) NOT NULL,
  `ip` VARCHAR(45) NOT NULL DEFAULT '127.0.0.1',
  `port` INT UNSIGNED NOT NULL DEFAULT 7788,
  `agent_token` VARCHAR(64) NOT NULL UNIQUE,
  `status` VARCHAR(16) NOT NULL DEFAULT 'offline' COMMENT 'online, offline, restarting',
  `players_count` INT UNSIGNED NOT NULL DEFAULT 0,
  `max_players` INT UNSIGNED NOT NULL DEFAULT 1500,
  `tick_rate` FLOAT NOT NULL DEFAULT 60.0,
  `memory_mb` INT UNSIGNED NOT NULL DEFAULT 0,
  `cpu_percent` FLOAT NOT NULL DEFAULT 0.0,
  `last_heartbeat` DATETIME DEFAULT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT `fk_psrv_proj` FOREIGN KEY (`project_id`) REFERENCES `portal_projects` (`id`) ON DELETE CASCADE,
  INDEX `idx_psrv_env` (`project_id`, `environment`),
  INDEX `idx_psrv_token` (`agent_token`),
  INDEX `idx_psrv_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_agent_commands` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `server_id` INT UNSIGNED NOT NULL,
  `command` VARCHAR(32) NOT NULL COMMENT 'restart, stop, broadcast, kick_all, execute',
  `payload` TEXT DEFAULT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'pending' COMMENT 'pending, executed, failed',
  `result` TEXT DEFAULT NULL,
  `dispatched_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `executed_at` DATETIME DEFAULT NULL,
  CONSTRAINT `fk_pcmd_srv` FOREIGN KEY (`server_id`) REFERENCES `portal_servers` (`id`) ON DELETE CASCADE,
  INDEX `idx_pcmd_srv_status` (`server_id`, `status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;