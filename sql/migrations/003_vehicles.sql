-- 003: сохраняемые машины серверного реестра (пункт 8d roadmap).
--
-- Платформа хранит только тонкий слой, общий для любого проекта: где машина
-- стоит, её модель, номер, замок и здоровье. Перезапуск сервера не должен
-- терять машины мира, и каждый геймод не должен писать это заново.
-- Сохраняются только машины, помеченные «сохраняемая» (persistent) — трафик,
-- в который сел игрок, сюда не попадает.
--
-- Свои данные машины (владелец-персонаж, страховка, тюнинг, пробег, топливо)
-- геймод держит в своих таблицах (миграции от 100) по ID машины — это тот
-- же ID, что приходит в событиях flovmp:vehicle:*. Внешний ключ — на пару
-- (`world`, `id`) с ON DELETE CASCADE: машина, убранная командой или
-- геймодом, удаляется отсюда, и ваши строки уйдут вместе с ней.
--
-- `world` — чей это мир: несколько серверов на одной базе (настройка
-- vehicles.world в client.cfg) не видят машин друг друга.
--
-- flovmp:ignore-errors 1050,1061

CREATE TABLE IF NOT EXISTS `vehicles` (
  `world` VARCHAR(32) NOT NULL DEFAULT 'main',
  `id` INT UNSIGNED NOT NULL,
  `model` INT UNSIGNED NOT NULL COMMENT 'хэш модели GTA (joaat)',
  `plate` VARCHAR(8) NOT NULL,
  `pos_x` FLOAT NOT NULL,
  `pos_y` FLOAT NOT NULL,
  `pos_z` FLOAT NOT NULL,
  `rot_x` FLOAT NOT NULL DEFAULT 0,
  `rot_y` FLOAT NOT NULL DEFAULT 0,
  `rot_z` FLOAT NOT NULL DEFAULT 0 COMMENT 'курс, градусы',
  `dimension` INT NOT NULL DEFAULT 0,
  `locked` TINYINT(1) NOT NULL DEFAULT 0,
  `body_health` FLOAT NOT NULL DEFAULT 1000 COMMENT '0..1000',
  `engine_health` FLOAT NOT NULL DEFAULT 1000 COMMENT '-4000..1000',
  `created_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`world`, `id`),
  INDEX `idx_vehicles_updated` (`updated_at_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
