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
| 6 | Отключить Sentry-телеметрию краш-хендлера сервера | todo (низкий приоритет) |
| 7 | Найти/дособрать полный клиент alt:V 16.4.39 (release, x64_win32) | **todo — главный блокер, нужен владелец** |
| 8 | Лаунчер: CDN-манифест + сверка хэшей + загрузчик (Вариант 1) | todo |
| 9 | Лаунчер: гибридная совместимость с патчами GTA V (Вар.1 offset-CDN / Вар.2 exe-swap) | todo |

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
