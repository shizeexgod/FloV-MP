-- 002: права администраторов в базе данных.
--
-- До этой таблицы права жили в config/admins.json у каждого сервера отдельно.
-- Это давало два неудобства: несколько инстансов не видели общих админов, и
-- «выдать себе права через базу» было невозможно — база о них не знала.
-- Теперь база — источник правды, а admins.json остаётся запасным путём, когда
-- база не настроена.
--
-- Ключ — SocialClubId, а не аккаунт и не ник:
--   * ник в alt:V задаёт клиент, его можно подделать;
--   * аккаунтов на сервере нет (входа нет), права привязываются к игроку.
--
-- Как выдать права вручную (сервер подхватит сам в течение 30 секунд, либо
-- сразу — командой reloadadmins в консоли сервера):
--
--   INSERT INTO admins (social_club, level, is_founder, note)
--   VALUES ('123456789', 8, 1, 'владелец')
--   ON DUPLICATE KEY UPDATE level = VALUES(level), is_founder = VALUES(is_founder);
--
-- SocialClubId игрока показывает команда /pos и лог подключения. Уровни:
-- 1..7 — администраторы по возрастанию, 8 — владелец. level = 0 снимает права.
--
-- flovmp:ignore-errors 1050

CREATE TABLE IF NOT EXISTS `admins` (
  `social_club` VARCHAR(32) NOT NULL PRIMARY KEY,
  `level` TINYINT UNSIGNED NOT NULL,
  `is_founder` TINYINT(1) NOT NULL DEFAULT 0,
  `note` VARCHAR(128) DEFAULT NULL,
  `granted_by` VARCHAR(64) DEFAULT NULL,
  `granted_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at_utc` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  -- Уровень вне 0..8 не имеет смысла; опечатка вроде 80 не должна молча
  -- превращаться в какой-то уровень прав.
  CONSTRAINT `chk_admins_level` CHECK (`level` BETWEEN 0 AND 8),
  INDEX `idx_admins_updated` (`updated_at_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
