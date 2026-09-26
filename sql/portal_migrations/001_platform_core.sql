-- Apply after portal_schema.sql to flovmp_portal (MariaDB 10.6+).
-- Additive first slice of the Account -> Project -> Product/Entitlement model.
-- Keep game database and existing license verification tables unchanged.
USE `flovmp_portal`;

CREATE TABLE IF NOT EXISTS `portal_products` (
  `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `code` VARCHAR(32) NOT NULL UNIQUE,
  `display_name` VARCHAR(128) NOT NULL,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_plans` (
  `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `product_id` BIGINT UNSIGNED NOT NULL,
  `code` VARCHAR(32) NOT NULL,
  `display_name` VARCHAR(128) NOT NULL,
  `default_server_limit` INT UNSIGNED NOT NULL DEFAULT 10,
  `default_player_limit` INT UNSIGNED NOT NULL DEFAULT 128,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  UNIQUE KEY `uk_plan_product_code` (`product_id`, `code`),
  CONSTRAINT `fk_plan_product` FOREIGN KEY (`product_id`) REFERENCES `portal_products` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_project_members` (
  `project_id` INT UNSIGNED NOT NULL,
  `user_id` INT UNSIGNED NOT NULL,
  `role` VARCHAR(24) NOT NULL COMMENT 'owner, admin, developer, viewer',
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`project_id`, `user_id`),
  KEY `idx_member_user` (`user_id`),
  CONSTRAINT `fk_member_project` FOREIGN KEY (`project_id`) REFERENCES `portal_projects` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_member_user` FOREIGN KEY (`user_id`) REFERENCES `portal_users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_entitlements` (
  `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `project_id` INT UNSIGNED NOT NULL,
  `product_id` BIGINT UNSIGNED NOT NULL,
  `plan_id` BIGINT UNSIGNED DEFAULT NULL,
  `state` VARCHAR(16) NOT NULL DEFAULT 'active' COMMENT 'active, suspended, revoked, expired',
  `server_limit` INT UNSIGNED NOT NULL DEFAULT 10,
  `player_limit` INT UNSIGNED NOT NULL DEFAULT 128,
  `valid_from` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `valid_until` DATETIME(6) DEFAULT NULL COMMENT 'NULL for lifetime',
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  KEY `idx_entitlement_project_state` (`project_id`, `state`, `valid_until`),
  KEY `idx_entitlement_product` (`product_id`),
  CONSTRAINT `fk_entitlement_project` FOREIGN KEY (`project_id`) REFERENCES `portal_projects` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_entitlement_product` FOREIGN KEY (`product_id`) REFERENCES `portal_products` (`id`),
  CONSTRAINT `fk_entitlement_plan` FOREIGN KEY (`plan_id`) REFERENCES `portal_plans` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_license_leases` (
  `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `license_id` INT UNSIGNED NOT NULL,
  `server_id` INT UNSIGNED NOT NULL,
  `lease_id` CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE,
  `issued_at` DATETIME(6) NOT NULL,
  `expires_at` DATETIME(6) NOT NULL,
  `revoked_at` DATETIME(6) DEFAULT NULL,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  KEY `idx_lease_server_expiry` (`server_id`, `expires_at`),
  KEY `idx_lease_license_expiry` (`license_id`, `expires_at`),
  CONSTRAINT `fk_lease_license` FOREIGN KEY (`license_id`) REFERENCES `portal_licenses` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_lease_server` FOREIGN KEY (`server_id`) REFERENCES `portal_servers` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_server_heartbeats` (
  `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `server_id` INT UNSIGNED NOT NULL,
  `players_count` INT UNSIGNED NOT NULL,
  `cpu_percent` DECIMAL(5,2) DEFAULT NULL,
  `memory_mb` INT UNSIGNED DEFAULT NULL,
  `server_version` VARCHAR(64) DEFAULT NULL,
  `recorded_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  KEY `idx_heartbeat_server_time` (`server_id`, `recorded_at`),
  CONSTRAINT `fk_heartbeat_server` FOREIGN KEY (`server_id`) REFERENCES `portal_servers` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `portal_audit_logs` (
  `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `actor_user_id` INT UNSIGNED DEFAULT NULL,
  `project_id` INT UNSIGNED DEFAULT NULL,
  `action` VARCHAR(96) NOT NULL,
  `resource_type` VARCHAR(48) NOT NULL,
  `resource_id` VARCHAR(64) NOT NULL,
  `before_json` JSON DEFAULT NULL,
  `after_json` JSON DEFAULT NULL,
  `source` VARCHAR(16) NOT NULL COMMENT 'WEB_ADMIN, TELEGRAM, API, SYSTEM',
  `request_ip` VARCHAR(45) DEFAULT NULL,
  `user_agent` VARCHAR(255) DEFAULT NULL,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  KEY `idx_audit_project_time` (`project_id`, `created_at`),
  KEY `idx_audit_actor_time` (`actor_user_id`, `created_at`),
  CONSTRAINT `fk_audit_actor` FOREIGN KEY (`actor_user_id`) REFERENCES `portal_users` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_audit_project` FOREIGN KEY (`project_id`) REFERENCES `portal_projects` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
