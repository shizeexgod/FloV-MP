# Agent State — FloV:MP

Updated: 2026-08-27

## Current task
- [~] Фаза 1 — минимальный скелет. Каркас готов, сервер стартует с нашими
  ресурсами. Остаётся тест живым клиентом GTA V (не автоматизируется).

## Queue
| # | Задача | Статус |
|---|------|--------|
| 0 | Проверить, стартует ли `altv-server.exe` автономно без бэкенда alt:V | ✅ done |
| 1 | Найти/восстановить формат `server.toml`/`resource.toml` | ✅ done — восстановлено, рабочие шаблоны + `docs/server-config.md`, проверено живым стартом |
| 2 | Минимальный C#-ресурс (`coreclr-module`) | ✅ done — `flovmp-core` грузится, `OnStart`/`OnServerStarted`/`OnPlayerConnect` работают |
| 3 | Минимальный JS клиентский ресурс (`js-module`) | ✅ done — `flovmp-client` грузится; связь C#→JS через `flovmp:client:welcome` заложена (проверится с живым клиентом) |
| 4 | Реальный тест подключения живым клиентом GTA V | todo — нужен реальный игровой клиент + клиентские бинарники alt:V, не автоматизируется |
| 5 | Лаунчер — скелет WPF-проекта | todo |
| 6 | Отключить Sentry-телеметрию краш-хендлера сервера | todo (низкий приоритет) |

## Done (эта сессия, 2026-08-27)
- **Поставлен .NET 8 Desktop Runtime** (`winget install Microsoft.DotNet.DesktopRuntime.8`).
  Хост C# alt:V требует `Microsoft.NETCore.App 8.0.0`; на машине был только SDK 10.
  Проверено: `Microsoft.NETCore.App 8.0.30` присутствует.
- **C#-геймод** `server/src/FloVMP.Gamemode/` (net8.0, `AltV.Net` 16.4.21):
  `GamemodeResource : Resource` (разводка) + `Systems/PlayerLifecycle.cs`
  (connect → модель + spawn + emit клиенту) + `SpawnPoints.cs` + `BuildInfo.cs`.
  Решение `server/FloVMP.slnx`.
- **JS-клиент** `client/resources/flovmp-client/` — приём `flovmp:client:welcome`,
  логи, нативное уведомление в try/catch.
- **Конфиг** `config/server.toml` — изоляция (`announce/useEarlyAuth/useCdn = false`),
  `debug = true`, `duplicatePlayers = 4` (2 клиента с одной машины для теста).
- **`scripts/assemble-runtime.ps1`** — собирает `runtime/server/` из бэкапа
  alt:V (ветка release 16.4.39) + `dotnet publish` геймода + клиент + конфиг.
  Идемпотентный. **`scripts/run-server.ps1`** — запуск (CWD = папка сервера),
  опция `-TimeoutSeconds` для boot-теста.
- **`.gitignore`** — добавлен `/runtime/` (бинарники alt:V в git не идут).
- **Boot-тест пройден**: сервер поднимается, оба ресурса грузятся,
  `[C#] core: server fully started`, сетевые потоки живы, HTTP :7788.
  Единственные ошибки — `curl code 6` ×2 (фоновый пинг движка, не блокирует).
- Документация: `docs/engine-recon.md`, `docs/server-config.md`,
  `docs/phase1-skeleton.md`.

## Blockers
- **Тест живым клиентом GTA V** (пункт #4) — нельзя автоматизировать, нужен
  реальный игровой клиент. Это же упирается в клиентские бинарники alt:V и
  способ подключить их к `127.0.0.1:7788` — что уже пересекается с Фазой 2
  (лаунчер).

## Notes / принятые решения
- Ветка бинарников alt:V — **release 16.4.39**, стандарт проекта.
- **AltV.Net 16.4.21** (NuGet) — последняя стабильная 16.4.x. Если хост
  ругнётся на несовместимость SDK — поднять до `16.4.28-rc.2`.
- **Скрипты `.ps1` — только ASCII** (PowerShell 5.1 парсит `.ps1` в CP1251,
  кириллица ломает парсер). C#-`Alt.Log` тоже переведён на ASCII (живая
  консоль alt:V коверкает UTF-8; файл `server.log` — корректный UTF-8).
  Русский язык — в `docs/*.md` и комментариях кода.
- Полная история решений/интервью с владельцем — история чата Florida V
  от 2026-08-27 (спросить владельца при нужде в точных деталях, не гадать).
