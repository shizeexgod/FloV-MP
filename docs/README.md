# docs/ — карта документации FloV:MP

Обновлено: 2026-08-29

Разложено по разделам. Актуальный статус проекта и очередь задач — в
[`../agent_state.md`](../agent_state.md). Контекст и правила — в
[`../CLAUDE.md`](../CLAUDE.md).

## engine/ — движок alt:V, серверное ядро

| Файл | О чём |
|---|---|
| [engine/engine-recon.md](engine/engine-recon.md) | что в бэкапе alt:V, сборка рантайма (`assemble-runtime.ps1`), требование .NET 8 |
| [engine/server-config.md](engine/server-config.md) | формат `server.toml` / `resource.toml` (восстановлен) |
| [engine/telemetry-sentry.md](engine/telemetry-sentry.md) | отключение встроенной Sentry-телеметрии сервера |
| [engine/core-hardening.md](engine/core-hardening.md) | устойчивость MP-ядра: Safe-обёртки, автосейв, single-session, карантин сторов, хендшейк client:ready |
| [engine/gtamp-reference.md](engine/gtamp-reference.md) | разбор чужого проекта GTAMP (независимый MP на alt:V) — откуда взят настоящий клиент 16.4.39 |
| [engine/client-direct-connect.md](engine/client-direct-connect.md) | разведка клиента alt:V: флаги `altv.exe`, `altv.toml`, состояние бэкапа |
| [engine/flovmp-connector.md](engine/flovmp-connector.md) | свой коннектор клиента: `LocalCdn` (заглушка мёртвого бэкенда), запуск `altv.exe`, проблема BattlEye |
| [engine/live-test-wall.md](engine/live-test-wall.md) | **живой прогон 2026-08-29**: вся цепочка работает до запуска игры; стена — лаунчер alt:V 16.3.7 не патчит GTA V билд 3889; пути B (Enhanced) / A (16.4.39-лаунчер) |
| [engine/legacy-3889-plan.md](engine/legacy-3889-plan.md) | актуальный план собственного адаптера для Epic Legacy 1.0.3889; старые downgrade-профили не смешиваются с ним |

## gameplay/ — геймплейные системы сервера (C# + NUI)

| Файл | О чём |
|---|---|
| [gameplay/phase1-skeleton.md](gameplay/phase1-skeleton.md) | скелет: C#-геймод (`FloVMP.Gamemode`) + JS-клиент (`flovmp-client`), спавн |
| [gameplay/phase3-auth.md](gameplay/phase3-auth.md) | авторизация: PBKDF2, стор, троттлинг, NUI логина |
| [gameplay/phase3-hud.md](gameplay/phase3-hud.md) | HUD: серверный тик, NUI-оверлей (HP/AR/$/часы/онлайн) |
| [gameplay/phase3-inventory.md](gameplay/phase3-inventory.md) | инвентарь: стекование/вес/persist, NUI-грид с drag&drop |
| [gameplay/phase3-chat.md](gameplay/phase3-chat.md) | чат: санитайзер, rate-limit, команды, NUI |

## launcher/ — лаунчер FloV:MP (WPF)

| Файл | О чём |
|---|---|
| [launcher/launcher.md](launcher/launcher.md) | архитектура WPF-лаунчера: детект GTA V, статус сервера, UI |
| [launcher/launcher-cdn.md](launcher/launcher-cdn.md) | система CDN-манифеста: сверка/докачка ядра клиента |
| [launcher/launcher-compat.md](launcher/launcher-compat.md) | совместимость с патчами GTA V: Вариант 1 (offset-CDN) / Вариант 2 (подмена exe с watchdog) |

## admin/ — админ-панель и логи (проектирование)

| Файл | О чём |
|---|---|
| [admin/admin-and-logging.md](admin/admin-and-logging.md) | **живой дизайн-документ**: веб-система логов + игровая/веб админ-панель с уровнями доступа. Обновляется НА МЕСТЕ |

## instructions/ — пошаговые процедуры

| Файл | О чём |
|---|---|
| [instructions/live-test-guide.md](instructions/live-test-guide.md) | живой тест 2 игроков: сервер → коннектор → полный чек-лист Фазы 3 |
| [instructions/quick-connect-test.md](instructions/quick-connect-test.md) | (устаревающий) прогон клиентом GTAMP — заменён своим коннектором, оставлен как диагностический референс |

## archive/ — устаревшее / историческое

| Файл | Почему здесь |
|---|---|
| [archive/HANDOFF_PROMPT.md](archive/HANDOFF_PROMPT.md) | исходный развёрнутый бриф проекта. Всё перенесено в `CLAUDE.md` + `agent_state.md` |
| [archive/client-recovery-plan.md](archive/client-recovery-plan.md) | реконструкция клиента из CEF+Majestic — не нужна, взят настоящий официальный клиент из GTAMP |
