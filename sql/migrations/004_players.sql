-- 004: сохранение игрока (пункт 10 roadmap).
--
-- players — то, что платформа восстанавливает сама при входе: где игрок
-- стоял, здоровье, броня, модель, оружие. Раньше это писал каждый геймод
-- заново. Что именно восстанавливать, решает владелец (players.restore_* в
-- client.cfg): RP-проект с выбором персонажа, например, позицию не берёт.
--
-- player_data — хранилище «ключ → значение» для геймода (инвентарь, деньги,
-- навыки): своё хранение без своего кода работы с базой. Значение — строка,
-- обычно JSON. Свои таблицы геймод по-прежнему может заводить миграциями от 100.
--
-- `identity` — постоянный ID игрока: у клиента 3889 — ID его ключа, у
-- клиента alt:V — SocialClub. Это то же число, что player.SocialClubId.
-- `world` — имя мира (настройка world.name; в комментарии миграции 003 она
-- ещё названа vehicles.world — это одна и та же настройка).
--
-- flovmp:ignore-errors 1050,1061

CREATE TABLE IF NOT EXISTS `players` (
  `world` VARCHAR(32) NOT NULL DEFAULT 'main',
  `identity` VARCHAR(32) NOT NULL,
  `name` VARCHAR(64) NOT NULL,
  `pos_x` FLOAT NOT NULL,
  `pos_y` FLOAT NOT NULL,
  `pos_z` FLOAT NOT NULL,
  `heading` FLOAT NOT NULL DEFAULT 0,
  `dimension` INT NOT NULL DEFAULT 0,
  `health` SMALLINT UNSIGNED NOT NULL DEFAULT 200,
  `armor` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  `model` INT UNSIGNED NOT NULL DEFAULT 0,
  `weapons` TEXT NULL COMMENT 'JSON [{hash, ammo}] — патроны по учёту сервера',
  `updated_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`world`, `identity`),
  INDEX `idx_players_updated` (`updated_at_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `player_data` (
  `world` VARCHAR(32) NOT NULL DEFAULT 'main',
  `identity` VARCHAR(32) NOT NULL,
  `key` VARCHAR(64) NOT NULL,
  `value` MEDIUMTEXT NOT NULL,
  `updated_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`world`, `identity`, `key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
