# FloV:MP & Держава Онлайн — HANDOFF (актуально на 2026-09-09)

Стартовое чтение для нового чата Claude Code / разработчиков. Полный статус и очередь —
`agent_state.md`. Правила и контекст — `AGENTS.md`. Не сокращать.

## Правило №1

При любой неясности — задавать вопрос явным списком уточнений. Ничего не
делать молча/по догадке. Отвечать на русском.

## Ключевое разграничение сущностей

| Сущность | Что это | Ответственность |
|---|---|---|
| **FloV:MP** | Независимый мультиплеерный движок: C++ unhooked runtime alt:V 16.4.39, протокол синхронизации, сетевой коннектор, FastDL CDN и SaaS веб-портал | «проблемы запуска GTA, синхронизации, протокола, телеметрии, сетевого ядра» |
| **Лаунчер «Держава RP / Держава Онлайн»** | Фирменный лаунчер игрока: Electron (Chromium UI) + нативный C# мост `FloVMP.Connect` и реестровый сервис | UI/UX, аппаратное ускорение, безопасный запуск игры |
| **Держава Онлайн** | Игровой RP-проект: карта Москвы RMRP 2025, 8-уровневая админка, фракции, семьи, экономика, инвентарь с CAS-транзакциями, оффлайн Control Plane | «игровой лор, экономика, серверная гейм-логика и оффлайн-управление» |

Границы: `docs/architecture/product-boundaries.md`, `platform-boundaries.md`.

## Как устроен запуск (техпоток)

1. **`FloVMP.Connect`** (CLI, `launcher/src/FloVMP.Connect/`) или Florida V
   Launcher: находит GTA (Epic/Steam/RGL), выбирает профиль, поднимает
   **`LocalCdn` на `127.0.0.1:9988`** — заглушка мёртвого бэкенда alt:V
   (отдаёт манифесты `launcher/client/backup update.json`, файлы клиента с
   диска, фейк `{"access":true}` на авторизацию, `{"release":"16.4.39"}` на
   проверку версии). Порт **9988 жёстко зашит** в alt:V.
2. Пишет `altv.toml` (`gtaPlatform` = `rgl` для Epic-сборки — `egs`/`epic`
   этот клиент отклоняет), опц. патчит `skin.bin` (SkinPatcher → SHA-256 URL
   нашей NUI-оболочки).
3. Запускает `runtime/client/altv.exe` (сток launcher **16.3.7**) с
   `-connecturl altv://connect/<ip:port> -directlaunch -customui
   http://127.0.0.1:9988/ui/index.html` → весь бэкенд-трафик alt:V идёт на
   нашу заглушку.
4. alt:V патчит `GTA5.exe`, инъектит `altv-client.dll` (**16.4.39**),
   коннектится на сервер.
5. **`altv-server.exe`** (Sentry-DSN обнулён) + C#-геймод `FloVMP.Gamemode`
   (грузит `FloVMP.Core`: Auth/Items/Chat/Logging) + JS-ресурс
   `flovmp-client` (NUI: логин/HUD/чат/инвентарь).

### Что РЕАЛИЗОВАНО и ПРОВЕРЕНО

- **Серверный движок на VDS REDL (`188.127.229.224`):**
  - Запущен автономно `./flovmp-server --no-module-path` под systemd (`flovmp.service`).
  - Активны порты: UDP 7788 (сеть), TCP 7788 (FastDL CDN), TCP 7799 (HTTP API).
  - Быстрый старт: 0 секунд (`disableDependencyDownload = true`).
  - База данных MariaDB (`derzhava_rp`) полностью развёрнута на VDS.
- **Тестовое покрытие:**
  - 229 automated tests в `FloVMP.Core.Tests` (100% green).
  - 41 automated test в `FloVMP.Launcher.Tests` (100% green).
  - **Суммарно: 270 автоматических тестов без ошибок.**
- **Харденинг сетевого ядра и безопасности:**
  - Устранена уязвимость перебора паролей (синглтон `AuthService` с IP/Login троттлингом).
  - Лимитер параллельных запросов (`MaxInFlight = 64`), лимит тела запроса 64 KB.
  - Устранена утечка памяти в `AntiCheatSystem` / `CombatValidationService.CleanupPlayer`.
- **Геймплей:**
  - Валидация медикаментов (бинты/аптечки нельзя при 0 HP и 200 HP).
  - Предметы: `armour`, `radio`, `lockpick` с CAS-транзакциями дропа/подбора.
  - Чат-команды `/s`, `/w`, `/clear` с поддержкой клиентского UI.
  - Cached handshake `flovmp:inv:ready` устраняет мигание пустого инвентаря.
- **SaaS Веб-Портал (`c:\FloV-MP\web`):**
  - Next.js 14 App Router, 44 скомпилированных роута.
  - Личный кабинет, биллинг, генератор лаунчеров, Developer API, txAdmin Remote Agent.
- **Оффлайн Control Plane & Веб-Админка (`docs/architecture/offline-admin-control-plane-spec.md`):**
  - Вариант В (Гибрид): встроенный дашборд + экспорт на сайт проекта (`admin.derzhava-online.ru`).
  - Прямое подключение к MariaDB (Вариант 1).
  - Интеграция с Discord-ботом: стрим блокировок в `#ban-logs`, тикетов в `#reports-feed`, выдача `/bansc` и ЧС младшей администрацией через запросы на подтверждение.
  - Мониторинг репортов с KPI времени ответа и часовыми графиками (по референсу ragemp.pro).
  - 360° досье персонажа, управление экономикой, инспекция багажников, выборочный и полный Wipe.

---

## Ключевые файлы

- `agent_state.md` — очередь задач, актуальный статус всех систем.
- `AGENTS.md` — правила репозитория, роли, стек.
- `docs/architecture/offline-admin-control-plane-spec.md` — спецификация серверной оффлайн-админки.
- `docs/frontend/claude-code-dashboard-spec.md` — ТЗ дашборда для Claude Code.
- `docs/architecture/saas-ecosystem-blueprint.md` — мастер-спецификация SaaS экосистемы.
- `docs/admin/admin-and-logging.md` — 8-уровневая админка, 5 уровней банов, система логов.
- `sql/schema.sql` — полная схема базы данных MariaDB.
- `config/server.toml` — боевой конфиг сервера.
- GitHub: `https://github.com/shizeexgod/FloV-MP` (коммиты локально, push строго по разрешению владельца).
