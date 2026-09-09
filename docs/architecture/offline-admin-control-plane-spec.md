# Спецификация для Claude Code: Серверная веб-админка и оффлайн Control Plane (FloV:MP & Держава Онлайн)

> **Назначение документа:** Полная техническая архитектура, схемы баз данных, REST API контракты и модель гибридной интеграции (Вариант В) для реализации оффлайн-системы полного управления игровым RP-сервером.
> Документ предназначен для передачи в **Claude Code** в качестве строгого технического задания на разработку бэкенда и интерфейса управления.

---

## 1. Архитектурная концепция: Вариант В (Гибридная модель)

В соответствии с выбором владельца реализуется **Вариант В (Гибрид)**:

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                   FloV:MP SaaS Platform & Control Plane                          │
│                                                                                  │
│   ┌───────────────────────────────────┐    ┌─────────────────────────────────┐   │
│   │ 1. Встроенный дашборд FloV:MP    │    │ 2. Автономный White-Label модуль│   │
│   │    (/dashboard/server-admin/...)  │    │    (admin.derzhava-online.ru)   │   │
│   │    Работает «из коробки» для      │    │    Встраивается в сайт проекта  │   │
│   │    любого сервера на платформе    │    │    или работает отдельным app   │   │
│   └─────────────────┬─────────────────┘    └────────────────┬────────────────┘   │
│                     │                                       │                    │
│                     └───────────────────┬───────────────────┘                    │
│                                         ▼                                        │
│                 ┌───────────────────────────────────────────────┐                │
│                 │   Unified Headless Server Admin REST API      │                │
│                 │          (/api/v1/server-admin/*)             │                │
│                 │     Авторизация: Bearer Token + 2FA + RBAC    │                │
│                 └───────────────────────┬───────────────────────┘                │
└─────────────────────────────────────────┼────────────────────────────────────────┘
                                          │
                   ┌──────────────────────┴──────────────────────┐
                   ▼                                             ▼
     [ Онлайн-игрок / Активный сервер ]            [ Оффлайн-игрок / Сервер спит ]
      ┌──────────────────────────────┐              ┌───────────────────────────┐
      │ FloV:MP Live RPC Gateway     │              │ Direct MariaDB Connector  │
      │ (TCP 7799 / RemoteAgent)     │              │ (ACID Transactions, CAS)  │
      │ Мгновенный сокет-кик,        │              │ Прямое изменение записей, │
      │ перемещение в демор/мут      │              │ отложенные флаги входа    │
      └──────────────────────────────┘              └───────────────────────────┘
```

### Принципы гибридной модели:
1. **Единый бэкенд (Headless Server Admin API):** Вся бизнес-логика, валидация прав 8-уровневой админки, транзакции БД и отправка RPC-команд изолированы в API-слое.
2. **Встроенный раздел в SaaS-портале FloV:MP:** В боковом меню личного кабинета (`web/src/app/dashboard`) появляется модуль **«Управление сервером (Admin Plane)»**, позволяющий владельцу управлять проектом прямо из платформы.
3. **Прямое подключение к MariaDB (Вариант 1 — Утверждено):** Админка подключается напрямую к игровой базе MariaDB `derzhava_rp` через защищённый пул соединений (ACID-транзакции, Compare-And-Swap, субмиллисекундный отклик, полная независимость от падений игрового тикрейта).
4. **Портативность (White-Label Export — Утверждено):** Модульные React/Tailwind компоненты (`web/src/components/server-admin/*`) поддерживают автономный режим (`STANDALONE_MODE=true`, `SERVER_DATABASE_URL=...`). Их можно запускать как на основном портале, так и развернуть на поддомене проекта (`admin.derzhava-online.ru`).

---

## 1.1. Интеграция с Discord-ботом: Выдача ЧС и наказаний

В систему оффлайн-администрирования закладывается выделенный шлюз для Discord-бота:
1. **Канал `#ban-logs`:** Мгновенный стрим всех блокировок (Social Club, HWID, Account, Hardban) с rich-embed карточками (кто выдал, кому, срок, причина).
2. **Канал `#reports-feed`:** Трансляция новых репортов и тикетов в реальном времени.
3. **Раздел `/bansc` и выдача ЧС младшей администрацией (Levels 1–2):**
   - Младшие администраторы (Хелперы и Модераторы) функционально в игре не имеют прав выдавать перманентные баны Social Club или заносить в ЧСП.
   - Через Discord-слэш команду `/request_punish` или веб-раздел модератор формирует запрос на блокировку с прикреплением доказательств (видео/скриншоты).
   - Старшая администрация (Level 4+ / ГА / ЗГА) в канале Discord или веб-панели нажимает кнопку `Одобрить` $\rightarrow$ бот автоматически исполняет `/bansc` на сервере и в БД без необходимости захода в игру.

---

## 2. Расширенная схема базы данных (MariaDB `derzhava_rp`)

Существующая база данных дополняется таблицами репортов, продуктивности администрации, семей и очередей оффлайн-действий:

```sql
-- =============================================================================
-- РАСШИРЕНИЕ ДЛЯ ОФФЛАЙН-АДМИНКИ И СИСТЕМЫ РЕПОРТОВ
-- =============================================================================

-- 1. Таблица репортов / тикетов игроков
CREATE TABLE IF NOT EXISTS `admin_reports` (
  `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `sender_character_id` INT UNSIGNED NOT NULL,
  `sender_name` VARCHAR(64) NOT NULL,
  `report_type` ENUM('STANDARD', 'MODERATOR') NOT NULL DEFAULT 'STANDARD' 
      COMMENT 'STANDARD: вопросы/помощь, MODERATOR: жалобы на читы/DM/нарушения',
  `message` TEXT NOT NULL,
  `target_character_id` INT UNSIGNED DEFAULT NULL COMMENT 'ID нарушителя, если репорт-жалоба',
  `target_name` VARCHAR(64) DEFAULT NULL,
  `status` ENUM('PENDING', 'IN_PROGRESS', 'RESOLVED', 'CLOSED', 'AUTO_CLOSED') NOT NULL DEFAULT 'PENDING',
  `responder_account_id` INT UNSIGNED DEFAULT NULL,
  `responder_name` VARCHAR(64) DEFAULT NULL,
  `response_text` TEXT DEFAULT NULL,
  `rating` TINYINT UNSIGNED DEFAULT NULL COMMENT 'Оценка игрока от 1 до 5',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `answered_at` DATETIME DEFAULT NULL,
  `resolved_at` DATETIME DEFAULT NULL,
  `response_time_seconds` INT UNSIGNED DEFAULT NULL COMMENT 'Время от создания до первого ответа',
  `resolution_time_seconds` INT UNSIGNED DEFAULT NULL COMMENT 'Время от создания до закрытия',
  INDEX `idx_rep_status` (`status`),
  INDEX `idx_rep_responder` (`responder_account_id`),
  INDEX `idx_rep_created` (`created_at`),
  INDEX `idx_rep_type` (`report_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. Учёт активности и смен администрации
CREATE TABLE IF NOT EXISTS `admin_activity_shifts` (
  `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `admin_account_id` INT UNSIGNED NOT NULL,
  `admin_name` VARCHAR(64) NOT NULL,
  `admin_level` TINYINT UNSIGNED NOT NULL,
  `shift_start` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `shift_end` DATETIME DEFAULT NULL,
  `reports_resolved` INT UNSIGNED NOT NULL DEFAULT 0,
  `punishments_issued` INT UNSIGNED NOT NULL DEFAULT 0,
  `afk_seconds` INT UNSIGNED NOT NULL DEFAULT 0,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  INDEX `idx_shift_admin` (`admin_account_id`),
  INDEX `idx_shift_start` (`shift_start`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. Штрафные баллы и выговоры администрации
CREATE TABLE IF NOT EXISTS `admin_penalty_points` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `admin_account_id` INT UNSIGNED NOT NULL,
  `issued_by_account_id` INT UNSIGNED NOT NULL,
  `issued_by_name` VARCHAR(64) NOT NULL,
  `points` SMALLINT UNSIGNED NOT NULL DEFAULT 1,
  `reason` VARCHAR(255) NOT NULL,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `removed_at` DATETIME DEFAULT NULL,
  `removal_reason` VARCHAR(255) DEFAULT NULL,
  INDEX `idx_pen_admin` (`admin_account_id`),
  INDEX `idx_pen_active` (`is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. Отпуска администрации
CREATE TABLE IF NOT EXISTS `admin_vacations` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `admin_account_id` INT UNSIGNED NOT NULL,
  `admin_name` VARCHAR(64) NOT NULL,
  `start_date` DATE NOT NULL,
  `end_date` DATE NOT NULL,
  `reason` VARCHAR(255) NOT NULL,
  `approved_by_name` VARCHAR(64) DEFAULT NULL,
  `status` ENUM('PENDING', 'APPROVED', 'REJECTED', 'FINISHED') NOT NULL DEFAULT 'PENDING',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX `idx_vac_admin` (`admin_account_id`),
  INDEX `idx_vac_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. Семьи / Банды (Families)
CREATE TABLE IF NOT EXISTS `families` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `name` VARCHAR(64) NOT NULL UNIQUE,
  `tag` VARCHAR(8) NOT NULL UNIQUE,
  `leader_character_id` INT UNSIGNED NOT NULL,
  `treasury_balance` BIGINT NOT NULL DEFAULT 0,
  `level` TINYINT UNSIGNED NOT NULL DEFAULT 1,
  `mansion_property_id` INT UNSIGNED DEFAULT NULL,
  `armory_json` JSON DEFAULT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX `idx_fam_leader` (`leader_character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 6. Члены семей
CREATE TABLE IF NOT EXISTS `family_members` (
  `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `character_id` INT UNSIGNED NOT NULL,
  `family_id` INT UNSIGNED NOT NULL,
  `rank` TINYINT UNSIGNED NOT NULL DEFAULT 1,
  `joined_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY `uq_fam_char` (`character_id`),
  INDEX `idx_fam_member` (`family_id`),
  CONSTRAINT `fk_fam_member` FOREIGN KEY (`family_id`) REFERENCES `families` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 7. Очередь отложенных оффлайн-действий (Offline Action Queue)
CREATE TABLE IF NOT EXISTS `offline_pending_actions` (
  `id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  `target_account_id` INT UNSIGNED DEFAULT NULL,
  `target_character_id` INT UNSIGNED DEFAULT NULL,
  `action_type` VARCHAR(32) NOT NULL 
      COMMENT 'INVENTORY_CONFISCATE, TELEPORT_SPAWN, FORCE_PASSWORD_RESET, WIPE_PARTIAL',
  `payload_json` JSON NOT NULL,
  `created_by_admin` VARCHAR(64) NOT NULL,
  `is_executed` TINYINT(1) NOT NULL DEFAULT 0,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `executed_at` DATETIME DEFAULT NULL,
  INDEX `idx_off_account` (`target_account_id`),
  INDEX `idx_off_char` (`target_character_id`),
  INDEX `idx_off_status` (`is_executed`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

---

## 3. Dual-Routing Engine: Архитектура онлайн / оффлайн применения

Критический механизм: как панель применяет действия в зависимости от состояния игрока:

```
                   [ Админ нажимает действие в Веб-панели ]
                                     │
                                     ▼
                [ TargetResolver.ResolveTarget(targetId) ]
                                     │
                  ┌──────────────────┴──────────────────┐
                  ▼                                     ▼
            ИГРОК ОНЛАЙН                           ИГРОК ОФФЛАЙН
  ┌─────────────────────────────────┐   ┌─────────────────────────────────┐
  │ 1. Запрос к RPC-шлюзу :7799     │   │ 1. Прямая ACID-транзакция в БД: │
  │    (POST /api/v1/control/apply) │   │    UPDATE characters / accounts │
  │ 2. Исполнение в тике CoreCLR:   │   │    INSERT INTO punishments      │
  │    - Alt.Player.Dimension = ... │   │    INSERT INTO banned_*         │
  │    - player.Emit("showBanModal")│   │ 2. Добавление записи в очередь  │
  │    - player.Kick(...)           │   │    `offline_pending_actions`    │
  │ 3. Сохранение результата в БД   │   │ 3. При следующем логине игрока: │
  │ 4. Мгновенный ответ веб-клиенту │   │    Security Handshake читает БД │
  │                                 │   │    и перехватывает сессию       │
  └─────────────────────────────────┘   └─────────────────────────────────┘
```

---

## 4. Карта разделов Веб-Админки (по референсу ragemp.pro)

Интерфейс разделен на логические блоки с учетом прав доступа:

| Раздел | Элементы меню | Функционал и возможности | Мин. Level |
|---|---|---|:---:|
| **Медиация** | **Статистика / Репорты** | KPI-карточки (Всего, Стандартные, Модераторские, Время ответа, Время решения, Активные админы), графики динамики по дням и часам, тепловая карта нагрузки по дням недели. Ответ на репорты прямо из веба. | Lvl 1+ |
| | **Правила гос. / Общие** | Редактор внутренней базы регламентов и правил сервера для админ-состава. | Lvl 1+ |
| | **Семьи** | Мониторинг составов, складов оружия, общака, войн за территории (каптов). | Lvl 3+ |
| | **Фракции** | Мониторинг гос. фракций (МВД, Правительство, Больницы) и банд: онлайн, бюджеты, ранги, лидеры. | Lvl 3+ |
| **Основное** | **Главная** | Общий обзор онлайна, активных инцидентов античита, последних наказаний, финансовой сводки. | Lvl 1+ |
| | **Команды сервера** | Интерактивный справочник всех серверных команд (`/get...`, `/goto...`, `/set...`) с фильтрацией по уровням. | Lvl 1+ |
| | **Отпуска** | Журнал заявок на отпуск администраторов с кнопками одобрения/отклонения (GA/ZGA). | Lvl 5+ |
| | **Наказания** | Журнал всех выданных наказаний (Demorgan, Jail, Mute, Warn, Bans) с фильтром по админу/игроку и кнопкой аннулирования. | Lvl 2+ |
| **Модерация** | **Штрафные баллы** | Система штрафных баллов и выговоров админ-состава за нарушения регламента. | Lvl 4+ |
| **Экономика** | **Банк и Транзакции** | Мониторинг денежных потоков, поиск крупных переводов (> 500k), RMT-детекция, заморозка счетов. | Lvl 8 |
| | **Транспорт** | Реестр всех ТС сервера: поиск по номерам/владельцу, эвакуация, инспекция багажников, изъятие дюпов. | Lvl 4+ |
| | **Недвижимость** | Дома, квартиры, бизнесы, гаражи: просмотр владельцев, баланса сейфов, взлом/смена замков, конфискация в госфонд. | Lvl 7+ |
| | **Предметы** | Глобальный инспектор предметов на сервере, каталогизация, аудит редких предметов/оружия. | Lvl 7+ |
| **Контент & Игроки**| **Игроки и Персонажи** | 360° досье игрока (инвентарь, авто, дома, документы, история IP/HWID/SocialClub), настройка статов, смена пароля/2FA, **обнуление (Wipe)**. | Lvl 8 (для wipe) |
| | **Античит и Логи** | 13 категорий логов, аномалии телепортации/оружия, live SSE stream, экспорт в JSONL/CSV. | Lvl 3+ |

---

## 5. Спецификация REST API контрактов (`/api/v1/server-admin/`)

Все эндпоинты требуют заголовок авторизации `Authorization: Bearer <ADMIN_SESSION_TOKEN>` и валидируют `admin_level`.

### 5.1. Репорты и продуктивность (`/reports`)
- `GET /api/v1/server-admin/reports/stats?from=...&to=...`
  - **Ответ:**
    ```json
    {
      "totalReports": 1420,
      "standardCount": 1280,
      "moderatorCount": 140,
      "avgResponseTimeSeconds": 42.5,
      "avgResolutionTimeSeconds": 185.0,
      "activeAdminsCount": 18,
      "timeSeries": [
        { "timestamp": "2026-09-09T12:00:00Z", "standard": 45, "moderator": 6, "avgResponse": 38.0 },
        { "timestamp": "2026-09-09T13:00:00Z", "standard": 62, "moderator": 9, "avgResponse": 41.2 }
      ],
      "hourlyDistribution": [
        { "hour": 18, "dayOfWeek": 3, "count": 115 }
      ]
    }
    ```
- `GET /api/v1/server-admin/reports/feed?status=PENDING&page=1&limit=25` — живая лента тикетов.
- `POST /api/v1/server-admin/reports/{id}/reply` — ответ на репорт из веб-интерфейса:
  - Тело: `{"response": "Приветствую! Уже слежу за нарушителем."}`.

### 5.2. Оффлайн-наказания (`/punishments`)
- `POST /api/v1/server-admin/punishments/issue`
  - **Тело запроса:**
    ```json
    {
      "targetType": "STATIC_ID", // STATIC_ID | ACCOUNT_ID | SOCIAL_CLUB | HWID | IP
      "targetValue": "10842",
      "punishmentType": "BANSC", // PRISON | JAIL | MUTE | WARN | BAN | BANSC | BANHWID | HARDBAN
      "durationMinutes": 43200,  // 30 дней (0 = перманент)
      "reason": "Использование запрещенного ПО / читы"
    }
    ```
  - **Логика исполнения:**
    1. Проверка прав администратора (`admin_level >= RequiredLevel`).
    2. Проверка, находится ли нарушитель онлайн в `Players.All`.
    3. Если онлайн: отправка RPC вызова на TCP 7799 -> отображение модалки бана клиенту -> сокет-кик.
    4. Если оффлайн: запись в `banned_social` (или `punishments`), запись в `punishment_history`.
    5. Фиксация в `admin_audit_logs`.

### 5.3. Персонажи: Досье, Редактирование и Обнуление (`/characters`)
- `GET /api/v1/server-admin/characters/search?q=10842` — поиск по нику, статику, логину или номеру паспорта.
- `GET /api/v1/server-admin/characters/{id}/dossier`
  - **Возвращает 360° профиль:** персональные данные, финансы (наличные, банк), инвентарь по слотам с метаданными, список транспорта с координатами, недвижимость, история наказаний, список документов.
- `PATCH /api/v1/server-admin/characters/{id}/adjust`
  - Изменение статов (HP, броня, наличные, банк, дименшен, координаты).
- `POST /api/v1/server-admin/characters/{id}/wipe`
  - **Выборочный или полный сброс:**
    ```json
    {
      "scope": "PARTIAL", // PARTIAL | FULL_CHARACTER | FULL_ACCOUNT
      "resetCash": true,
      "resetBank": true,
      "wipeInventory": true,
      "confiscateVehicles": false,
      "confiscateProperties": false,
      "reason": "Обнуление за покупку виртов (RMT)"
    }
    ```

### 5.4. Экономика и Транзакции (`/economy`)
- `GET /api/v1/server-admin/economy/transactions?minAmount=500000&from=...` — мониторинг крупных денежных переводов.
- `POST /api/v1/server-admin/economy/adjust-treasury` — изменение баланса казны фракции/семьи.

### 5.5. Недвижимость и Транспорт (`/assets`)
- `GET /api/v1/server-admin/assets/vehicles?plate=A777AA77` — инспекция ТС, просмотр багажника.
- `POST /api/v1/server-admin/assets/vehicles/{id}/impound` — отправка на штрафстоянку.
- `POST /api/v1/server-admin/assets/properties/{id}/transfer-owner` — смена владельца недвижимости / передача государству.

---

## 6. Инструкция по сборке компонентов для Claude Code

При реализации в Next.js (`c:\FloV-MP\web\src\app\`):

1. **Размещение в структуре проекта:**
   - Основной рабочий дашборд: `web/src/app/dashboard/server-control/page.tsx`
   - Интерактивный демонстрационный концепт: `web/src/app/concepts/server-admin/page.tsx`
   - Бэкенд API роуты: `web/src/app/api/v1/server-admin/[module]/route.ts`

2. **UI Архитектура компонентов:**
   - Компоненты дашборда репортов (`ReportMetricsCards`, `ReportVolumeChart`, `ReportHeatmapTable`).
   - Компоненты досье персонажа (`PlayerDossierModal`, `InventoryVisualGrid`, `VehicleTrunkInspector`).
   - Модалка выдачи оффлайн-наказания (`OfflinePunishmentModal`).

3. **Стиль и палитра:**
   - Тёмный неоновый стиль (`#ff3d8a` Codex Pink, `#a855f7` Cyber Violet, фон `#08080c`).
   - Четкие бейджи статусов (`Online`, `Offline`, `Banned`, `In Demorgan`).
   - SVG графики без тяжелых сторонних библиотек (Recharts / Chart.js не нужны, отрисовка через легкие SVG Path).
