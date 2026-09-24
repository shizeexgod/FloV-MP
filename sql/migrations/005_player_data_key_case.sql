-- 005: ключи player_data различают регистр.
--
-- В 004 колонка `key` получила сравнение utf8mb4_unicode_ci — без учёта
-- регистра. Сервер и геймод различают «Money» и «money» (это разные ключи),
-- а база считала их одним: запись одного молча затирала другой. Бинарное
-- сравнение делает ключи в базе такими же, как в памяти сервера.
--
-- flovmp:ignore-errors 1054

ALTER TABLE `player_data`
  MODIFY `key` VARCHAR(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL;
