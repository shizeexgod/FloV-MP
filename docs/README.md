# docs/ — карта документации FloV:MP & Держава Онлайн

Обновлено: 2026-09-09

Разложено по разделам. Актуальный статус проекта и очередь задач — в [`../agent_state.md`](../agent_state.md). Правила и контекст — в [`../AGENTS.md`](../AGENTS.md).

- **FloV:MP** — независимый мультиплеерный движок прямого подключения (сетевой стек на отвязанных бинарниках alt:V v16.4.39 release, протокол, коннектор, SaaS веб-портал).
- **Держава Онлайн** (Держава RP) — игровой RP-проект (карта Москвы RMRP 2025, российская тематика, 8-уровневая админка, фракции, семьи, экономика, оффлайн Control Plane).

---

## architecture/ — системная и продуктовая архитектура

| Файл | О чём |
|---|---|
| [architecture/saas-ecosystem-blueprint.md](architecture/saas-ecosystem-blueprint.md) | **Мастер-спецификация SaaS-экосистемы**: 13 глав (Multiplayer Core, Server Core, SDK, Telemetry 60 Hz, txAdmin Remote Agent, AI Assistant, High-Load 1000+) |
| [architecture/offline-admin-control-plane-spec.md](architecture/offline-admin-control-plane-spec.md) | **Спецификация серверной оффлайн-админки (Вариант В — Гибрид)**: прямое подключение к MariaDB `derzhava_rp`, Dual-Routing Engine (online RPC vs offline DB), репорты, досье, вайпы, Discord-бот `/bansc` |
| [architecture/client-deployment-and-hardware-guide.md](architecture/client-deployment-and-hardware-guide.md) | **Развёртывание на серверах заказчиков**: профиль AMD 8C / 64GB, изоляция KVM/Docker, отказ от Apache в пользу Nginx, защита от DDoS без 3 серверов |
| [architecture/product-boundaries.md](architecture/product-boundaries.md) | Разграничение: движок FloV:MP vs RP-проект «Держава Онлайн» |
| [architecture/platform-boundaries.md](architecture/platform-boundaries.md) | Платформенные границы: что входит в сетевой движок, что является гейммодом |

---

## frontend/ — спецификации веб-интерфейсов

| Файл | О чём |
|---|---|
| [frontend/claude-code-dashboard-spec.md](frontend/claude-code-dashboard-spec.md) | ТЗ для Claude Code: дизайн-система Cyber Dark Glass (`#ff3d8a`), 14 модулей дашборда Next.js 14, Live SSE stream, txAdmin Cloud Terminal |

---

## engine/ — движок alt:V, серверное ядро и безопасность

| Файл | О чём |
|---|---|
| [engine/engine-recon.md](engine/engine-recon.md) | Разведка бинарников alt:V, сборка рантайма, требование .NET 8 |
| [engine/server-config.md](engine/server-config.md) | Формат `server.toml` и `resource.toml`, запуск с `disableDependencyDownload = true` |
| [engine/telemetry-sentry.md](engine/telemetry-sentry.md) | Отключение встроенной Sentry-телеметрии сервера (нулевой оверхед) |
| [engine/core-hardening.md](engine/core-hardening.md) | Харденинг ядра: защита от DoS (64KB лимит), IP rate limiting (30/10s), MaxInFlight (64), устранение утечек памяти в античите |
| [engine/gtamp-reference.md](engine/gtamp-reference.md) | Разбор чужого проекта GTAMP (независимый MP на alt:V) |
| [engine/client-direct-connect.md](engine/client-direct-connect.md) | Разведка клиента alt:V: флаги `altv.exe`, `altv.toml` |
| [engine/flovmp-connector.md](engine/flovmp-connector.md) | Коннектор клиента: `LocalCdn` на порту 9988, обход мертвого бэкенда |
| [engine/live-test-wall.md](engine/live-test-wall.md) | Результаты тестов коннектора и инъекции `altv-client.dll` |
| [engine/legacy-3889-plan.md](engine/legacy-3889-plan.md) | План адаптера для Epic Legacy 1.0.3889 |

---

## gameplay/ — геймплейные системы сервера (C# + NUI)

| Файл | О чём |
|---|---|
| [gameplay/phase1-skeleton.md](gameplay/phase1-skeleton.md) | Скелет: C#-гейммод (`FloVMP.Gamemode`) + JS-клиент (`flovmp-client`), спавн |
| [gameplay/phase3-auth.md](gameplay/phase3-auth.md) | Авторизация: PBKDF2, single-session, rate limiting, NUI логина |
| [gameplay/phase3-hud.md](gameplay/phase3-hud.md) | HUD: серверный тик, NUI-оверлей (HP/AR/$/часы/онлайн) |
| [gameplay/phase3-inventory.md](gameplay/phase3-inventory.md) | Инвентарь: CAS-транзакции на дроп/подбор, каталог предметов (`armour`, `radio`, `lockpick`), cached handshake `flovmp:inv:ready` |
| [gameplay/phase3-chat.md](gameplay/phase3-chat.md) | Чат: санитайзер, rate-limit, команды `/me`, `/do`, `/s`, `/w`, `/clear`, NUI |

---

## launcher/ — лаунчер FloV:MP (Electron + Native Bridge)

| Файл | О чём |
|---|---|
| [launcher/launcher.md](launcher/launcher.md) | Архитектура фирменного лаунчера: Electron UI (Chromium) + нативный C# мост `FloVMP.Connect` |
| [launcher/launcher-cdn.md](launcher/launcher-cdn.md) | Система FastDL & CDN-манифеста: сверка хешей и докачка клиента |
| [launcher/launcher-compat.md](launcher/launcher-compat.md) | Совместимость с патчами GTA V: смещение оффсетов и exe-подмена |

---

## admin/ — админ-панель и логи

| Файл | О чём |
|---|---|
| [admin/admin-and-logging.md](admin/admin-and-logging.md) | 8-уровневая иерархия администрации, 5 уровней банов (включая `/bansc`), резолвер целей `TargetResolver`, аудит-логи |

---

## instructions/ — пошаговые инструкции

| Файл | О чём |
|---|---|
| [instructions/live-test-guide.md](instructions/live-test-guide.md) | Руководство по живому тестированию и валидации сетевого соединения |
| [instructions/quick-connect-test.md](instructions/quick-connect-test.md) | Быстрый прогон прямого подключения |
