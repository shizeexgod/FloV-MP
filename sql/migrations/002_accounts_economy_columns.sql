-- 002: экономика и наказания на уровне аккаунта.
--
-- MySqlAccountStore читает и пишет accounts.cash / accounts.bank, но в самой
-- ранней схеме этих колонок не было (лежали в characters) — MySQL-путь падал
-- на первом же запросе.
--
-- Директива ниже разрешает раннеру пропускать «уже существует» на уровне
-- отдельного выражения: 1060 = дубль колонки, 1061 = дубль индекса. Это нужно
-- для баз, которые подняты со schema.sql ДО появления миграций: baseline там
-- уже накатан руками, и повторный ALTER обязан быть безвредным.
-- Синтаксис `IF NOT EXISTS` в ALTER/CREATE INDEX намеренно НЕ используется:
-- его понимает MariaDB, но не MySQL 8 — а платформу ставят и туда, и туда.
-- flovmp:ignore-errors 1060,1061

ALTER TABLE `accounts` ADD COLUMN `cash` BIGINT NOT NULL DEFAULT 0;

ALTER TABLE `accounts` ADD COLUMN `bank` BIGINT NOT NULL DEFAULT 0;

ALTER TABLE `accounts` ADD COLUMN `bank_account_number` VARCHAR(32) DEFAULT NULL;

ALTER TABLE `accounts` ADD COLUMN `warns` INT NOT NULL DEFAULT 0;

ALTER TABLE `accounts` ADD COLUMN `jail_until_utc` DATETIME DEFAULT NULL;

-- Поиск по номеру счёта используется командой перевода — без индекса это
-- полный скан таблицы аккаунтов на каждый /transfer.
CREATE INDEX `idx_acc_bank_account` ON `accounts` (`bank_account_number`);
