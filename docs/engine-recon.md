# Разведка движка alt:V и сборка рантайма

Обновлено: 2026-08-27

## Источник

`C:\ViMP backup\backup-altv\` — бэкап **официальных** бинарников alt:V
(свободно распространяемый SDK, не чей-то кастом). Внутри ноль игрового кода,
только платформа. В репозиторий FloV:MP **не входит** — это внешний рантайм.

Ветки: `dev` / `rc` / `release`. Мы стандартизируемся на **`release`**
(версия сервера **16.4.39**), под неё же проверен живой старт.

## Что из бэкапа реально нужно (маппинг)

| Что | Откуда (ветка release, `x64_win32`) | Куда в рантайме |
|---|---|---|
| Сервер | `server/release/x64_win32/altv-server.exe` (+ `altv-crash-handler.exe`, `update.json`) | `runtime/server/` |
| C#-хост | `coreclr-module/release/x64_win32/AltV.Net.Host.dll` (+ `.runtimeconfig.json`) | `runtime/server/` |
| C#-модуль | `coreclr-module/release/x64_win32/modules/csharp-module.dll` | `runtime/server/modules/` |
| JS-модуль | `js-module/release/x64_win32/modules/js-module/{js-module.dll,libnode.dll}` | `runtime/server/modules/js-module/` |
| Данные | `data/release/data/*.bin` (clothes/pedmodels/rpfdata/vehmodels/vehmods/weaponmodels) | `runtime/server/data/` |

`.bin`-файлы движок **не находит сам** по соседнему пути — их обязательно
копировать в `runtime/server/data/`. Без них в лог сыпется
`<name>.bin can't be loaded` (не фатально, но модели будут недоступны).

Всё это делает `scripts/assemble-runtime.ps1` (см. `docs/phase1-skeleton.md`).

## Требование: .NET 8

`AltV.Net.Host.runtimeconfig.json` → `Microsoft.NETCore.App` **8.0.0**.
На машине разработки стоял только SDK .NET 10 без рантайма 8 — хост C# без
него не стартует. Поставлен **.NET 8 Desktop Runtime** (`winget install
Microsoft.DotNet.DesktopRuntime.8`), проверено: `dotnet --list-runtimes`
показывает `Microsoft.NETCore.App 8.0.30`.

Геймод собирается под `net8.0` (см. `server/Directory.Build.props`). Не
поднимать TFM до 9/10 без пересборки хоста alt:V, которого у нас нет.

## Живой старт — подтверждено (2026-08-27)

`altv-server.exe` из собранного рантайма поднимается **полностью автономно**,
без обращения к инфраструктуре alt:V. Лог (`runtime/server/server.log`,
UTF-8) чистый: грузятся оба наших ресурса, поднимаются все сетевые потоки
(EntityStreamer / SyncSend×8 / SyncReceive×4 / NetworkWorker / HTTP :7788),
`Server started in debug mode`.

### Единственная «ошибка» в логе — не наша проблема

```
[Error] HTTP Request failed, curl code: 6
[Error] HTTP Request failed, curl code: 6
```

`curl code 6` = «не резолвится DNS». Движок 16.4.39 сразу после старта делает
1–2 фоновых HTTP-запроса (проверка версии / early-access) даже при
`announce = false` / `useCdn = false`. **Не блокирует** работу — сервер
стартует и работает. Отдельного switch в конфиге под это не нашлось.

### Тайминг

Между `Loading resource flovmp-core` и первым `[C#]`-логом ~6 секунд — это
`csharp-module` прогревает coreclr-хост («Checking dependencies…»). Разово
при старте, дальше не влияет.

## Не проверено

- Подключение **живым клиентом GTA V** — нужен реальный игровой клиент,
  не автоматизируется. Это отдельный пункт очереди.
- ~~Sentry-телеметрия крашей~~ — **разобрано и отключено**, см.
  `docs/telemetry-sentry.md`. `assemble-runtime.ps1` обнуляет DSN в рабочей
  копии `altv-server.exe`; проверено — запросов на `sentry-alt.com` нет.
