# Лаунчер FloV:MP — скелет (Фаза 2)

Обновлено: 2026-08-27
Статус: **скелет собирается и запускается. Direct-connect собирается, но не проверен живьём (нужен полный клиент alt:V — см. `docs/client-direct-connect.md`).**

## Стек

C# / WPF / **.NET 8** (`net8.0-windows`). Собирается SDK .NET 10 + Desktop
Runtime 8 (уже стоит). Решение `launcher/FloVMP.Launcher.slnx`.

## Что реализовано

| Модуль | Файл | Делает |
|---|---|---|
| Настройки | `Models/LauncherSettings.cs`, `Services/SettingsStore.cs` | JSON в `%LOCALAPPDATA%\FloVMP\launcher.settings.json` |
| Детект GTA V | `Services/GtaLocator.cs` | реестр: Rockstar/Epic (`InstallFolderEpic`), Rockstar Launcher (`InstallFolder`), Steam (`libraryfolders.vdf` → `common\Grand Theft Auto V`). Валидация по наличию `GTA5.exe` |
| Ядро клиента | `Services/AltvClientCore.cs` | проверка комплектности папки (16 обязательных файлов), сборка `altv://connect/...`-URL и аргументов `-connecturl -noupdate -branch -skipprocesscheck`, запуск `altv.exe`, опц. правка глобального `altv.toml` (`gtapath`/`branch`/`name`, с `.bak`) |
| Статус сервера | `Services/ServerStatus.cs` | HTTP-пинг `:7788` (пробует `/status.json`, потом `/` — любой ответ = «жив») |
| UI | `App.xaml` (тёмная тема + акцент `#FF3D8A`), `MainWindow.xaml`, `ViewModels/MainViewModel.cs` (MVVM-lite: `ObservableObject` + `RelayCommand`) | сервер+статус, никнейм, пути (детект/обзор), опции, лог, кнопка ИГРАТЬ (активна только когда GTA5.exe + ядро + ник валидны) |

## Что НЕ реализовано (следующие заходы)

- **CDN-манифест и обновление ресурсов** (JSON-манифест, MD5/SHA256-сверка,
  многопоточный загрузчик) — Вариант 1 из HANDOFF. Сейчас ядро клиента
  указывается вручную.
- **Приватная папка ядра** `%LOCALAPPDATA%\FloVMP\core\` — сейчас лаунчер
  просто указывает на любую папку с `altv.exe`.
- **Гибридная совместимость с патчами GTA V** (Вариант 1 offset-конфиг с
  CDN; Вариант 2 подмена `GTA5.exe` с atomic-rename / маркером / watchdog).
- **Новости, авторизация, прогресс-бар загрузки.**
- Полноценный дизайн (сейчас — рабочий минимум в палитре Florida V).

## Сборка и запуск

```powershell
dotnet build launcher/FloVMP.Launcher.slnx -c Debug
# или запуск собранного:
launcher/src/FloVMP.Launcher/bin/Debug/net8.0-windows/FloVMP.Launcher.exe
```

## Как «ИГРАТЬ» соберёт запуск

```
altv.exe -connecturl "altv://connect/<host>:<port>?nickname=<ник>" -noupdate -branch release -skipprocesscheck -skipprocessconfirmation
```
рабочая директория = «ЯДРО КЛИЕНТА alt:V». Подробности механизма и флагов —
`docs/client-direct-connect.md`.

## Blockers

- Кнопка ИГРАТЬ не станет активной, пока «ЯДРО КЛИЕНТА» не указывает на
  **полную** папку клиента alt:V (16 обязательных файлов). Бэкап неполный —
  см. `docs/client-direct-connect.md`, `scripts/assemble-client-core.ps1`,
  `runtime/client/MISSING.txt`.
