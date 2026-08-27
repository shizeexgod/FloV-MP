# Agent State — FloV:MP

Updated: 2026-08-27

## Current task
- [~] Фаза 1 — скелет сервера готов, стартует с ресурсами.
- [~] Фаза 2 — скелет лаунчера готов, собирается и запускается.
- **Блокер обеих фаз к «играбельно»**: бэкап клиента alt:V неполный
  (17/85 файлов), живой тест невозможен до полного набора клиента.

## Queue
| # | Задача | Статус |
|---|------|--------|
| 0 | `altv-server.exe` стартует автономно | ✅ done |
| 1 | Формат `server.toml` / `resource.toml` | ✅ done — `docs/server-config.md`, проверено стартом |
| 2 | Минимальный C#-ресурс | ✅ done — `flovmp-core` грузится |
| 3 | Минимальный JS клиентский ресурс | ✅ done — `flovmp-client` грузится |
| 4 | Тест живым клиентом GTA V | **blocked** — нужен полный набор файлов клиента alt:V 16.4.39 (бэкап неполный, см. ниже) |
| 5 | Лаунчер — скелет WPF | ✅ done — детект GTA V, статус сервера, сборка direct-connect, UI |
| 6 | Отключить Sentry-телеметрию сервера | ✅ done — DSN обнуляется в рабочей копии `altv-server.exe` при сборке, запросов на `sentry-alt.com` нет (`docs/telemetry-sentry.md`) |
| 7 | Полный клиент alt:V 16.4.x (release, x64_win32) | **BLOCKED, но есть план.** Официальные источники мертвы (CDN DNS нет, `altv.mp` 502, GitHub `altmp` — только вспом. репы). План восстановления: `docs/client-recovery-plan.md` — 66/68 файлов берутся из живых сторонних источников (CEF 131.0.6778.205 подтверждён на cef-builds), реальная стена только `legacy.dll` (обход — Enhanced GTA V). Нужен владелец: community-зеркало ИЛИ решение по Enhanced |
| 8 | Лаунчер: CDN-манифест + сверка хэшей + загрузчик (Вариант 1) | todo |
| 9 | Лаунчер: гибридная совместимость с патчами GTA V (Вар.1 offset-CDN / Вар.2 exe-swap) | todo |

## Done (сессия 2026-08-27, часть 3 — Sentry + сеть)
- **Sentry-телеметрия отключена.** `altv-server.exe` содержит sentry-native
  0.6.5 с хардкод-DSN `...@sentry-alt.com/4`; хост живой (Cloudflare, ingest
  отвечает 400), envelope уходит уже при старте сервера. `SENTRY_DSN` env не
  помогает (alt:V перетирает через `sentry_options_set_dsn`). Решение:
  `assemble-runtime.ps1` обнуляет строку DSN в рабочей копии exe (бэкап цел).
  Проверено `SENTRY_DEBUG=1` — `sentry_init failed`, запросов нет. Опция
  `-KeepSentry` отключает патч. `docs/telemetry-sentry.md`.
- **Сеть проверена**: CDN alt:V (`cdn.alt-mp.com`, `cdn.altv.mp`,
  `cdn.alt-mp.dev`) — DNS мёртв. `altv.mp` — origin 502 (Cloudflare жив).
  `docs.altv.mp` — 200 (только SPA-оболочка, статьи по угадай-URL 404).
  Полного клиента alt:V на дисках C:/D:/E: нет (искал по всем `altv.exe`).

## Done (сессия 2026-08-27, часть 2 — лаунчер + разведка клиента)
- **Лаунчер** `launcher/` (WPF, net8.0-windows, `FloVMP.Launcher.slnx`):
  - `Services/GtaLocator.cs` — детект GTA V по реестру (Rockstar/Epic/Steam);
    на этой машине находит Epic-инсталл по `InstallFolderEpic`.
  - `Services/AltvClientCore.cs` — валидация папки ядра клиента (16
    обязательных файлов), сборка `altv://connect/...` + аргументов
    `-connecturl -noupdate -branch -skipprocesscheck`, запуск `altv.exe`,
    опц. правка глобального `altv.toml` (`.bak`).
  - `Services/ServerStatus.cs` — HTTP-пинг `:7788`.
  - `Services/SettingsStore.cs` + `Models/LauncherSettings.cs` — JSON в
    `%LOCALAPPDATA%\FloVMP\launcher.settings.json`.
  - UI: тёмная тема + акцент `#FF3D8A`, MVVM-lite. Сборка + smoke-run ok.
- **Разведка клиента alt:V** (`docs/client-direct-connect.md`):
  - GTA V на машине = **Epic legacy**, `C:\Program Files\9d2d0eb64d5c44529cece33fe2a46482`.
  - `%LOCALAPPDATA%\altv\altv.toml` уже настроен (branch release, egs, gtapath).
  - `altv.exe` = launcher-UI 14.4; флаги: `-connecturl`, `-noupdate`,
    `-branch`, `-directlaunch`, `-skipprocesscheck`, `-gtaexe`, `-offline`.
  - **Бэкап клиента неполный: 17/85 файлов.** Нет `legacy.dll`,
    `resources.pak`, `icudtl.dat`, V8-снапшотов, crypto-DLL, всех
    `cef/locales/*.pak`. `scripts/assemble-client-core.ps1` собирает что есть
    и пишет `runtime/client/MISSING.txt`.
- Документация: `docs/launcher.md`, `docs/client-direct-connect.md`.

## Blockers
- **Полный клиент alt:V 16.4.39 (release, x64_win32).** Без него `altv.exe`
  не запустит игру → нет живого теста (#4), кнопка ИГРАТЬ в лаунчере
  неактивна. Нужен источник: рабочая установка alt:V на другой машине/диске,
  другой бэкап или зеркало CDN. Затем `assemble-client-core.ps1 -FillFrom <...>`.

## Notes / принятые решения
- Ветка alt:V — **release 16.4.39**.
- `AltV.Net` 16.4.21 (NuGet), при проблемах SDK — `16.4.28-rc.2`.
- Скрипты `.ps1` и `Alt.Log` в C# — **только ASCII** (PS 5.1 CP1251 /
  консоль alt:V). Русский — в `docs/*.md`, комментариях, WPF-UI.
- `runtime/` (сервер и клиент) — в `.gitignore`.
- Решения `.slnx` (не `.sln`) — дефолт `dotnet` 10.
- Правка глобального `altv.toml` — только по явной галке, всегда `.bak`.
