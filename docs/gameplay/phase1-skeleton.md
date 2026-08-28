# Фаза 1 — минимальный скелет

Обновлено: 2026-08-27
Статус: **каркас готов, сервер стартует с ресурсами. Не хватает теста живым клиентом GTA V.**

## Цель фазы

Два игрока подключаются, видят друг друга и двигаются. Взаимную видимость и
синхронизацию перемещения обеспечивает сетевой движок alt:V сам — от нас
нужно только заспавнить игрока на сервере.

## Что сделано

| Компонент | Путь | Роль |
|---|---|---|
| C#-геймод | `server/src/FloVMP.Gamemode/` | `OnPlayerConnect` → выдать модель `FreemodeMale01`, `player.Spawn(...)` на Legion Square, послать клиенту `flovmp:client:welcome` |
| C#-ресурс | `server/resources/flovmp-core/resource.toml` | `type = "csharp"` |
| JS-клиент | `client/resources/flovmp-client/client/index.js` | ловит `flovmp:client:welcome`, логирует, пробует нативное уведомление |
| JS-ресурс | `client/resources/flovmp-client/resource.toml` | `type = "js"`, только `client-main` |
| Конфиг | `config/server.toml` | изоляция от инфраструктуры alt:V, `debug`, `duplicatePlayers = 4` |
| Сборка | `scripts/assemble-runtime.ps1` | движок из бэкапа + `dotnet publish` геймода + копия клиента + конфиг → `runtime/server/` |
| Запуск | `scripts/run-server.ps1` | запуск из `runtime/server/` (CWD важен), опционально с таймаутом для boot-теста |

Код на C#: точка входа `GamemodeResource : Resource` только разводит системы;
логика подключения — в `Systems/PlayerLifecycle.cs`; точки спавна — в
`SpawnPoints.cs` (разброс по кругу, чтобы игроки не спавнились друг в друге).

## Как собрать и запустить

Предусловия: .NET 8 Runtime (`dotnet --list-runtimes` → `Microsoft.NETCore.App 8.0.x`),
Node в PATH, бэкап alt:V в `C:\ViMP backup\backup-altv`.

```powershell
# 1. собрать рантайм (движок + геймод + клиент + конфиг)
powershell -File scripts/assemble-runtime.ps1

# 2a. обычный запуск (Ctrl+C для остановки)
powershell -File scripts/run-server.ps1

# 2b. или boot-тест без клиента: поднять на 20 с и погасить
powershell -File scripts/run-server.ps1 -TimeoutSeconds 20
```

Лог: `runtime/server/server.log` (**UTF-8** — читать `Get-Content -Encoding UTF8`).

## Что проверено (boot-тест, 2026-08-27)

- `assemble-runtime.ps1` собирает `runtime/server/` целиком, без ручных шагов.
- Сервер поднимается: оба ресурса грузятся, `[C#] core: server fully started`,
  все сетевые потоки живы, HTTP :7788.
- Единственные ошибки в логе — `curl code: 6` ×2 (фоновый пинг движка,
  не блокирует, см. `docs/engine/engine-recon.md`).
- C#-логи переведены на ASCII (живая консоль alt:V на Windows коверкает
  UTF-8; файл лога при этом корректный UTF-8).

## Что НЕ проверено

- **Подключение живым клиентом GTA V.** Нужен реальный игровой клиент +
  клиентские бинарники alt:V (`client/release/x64_win32/`) и способ
  указать им `127.0.0.1:7788`. Не автоматизируется — отдельный пункт очереди.
- Реальная взаимная видимость/движение двух педов (следствие предыдущего).

## Известные решения по ходу

- **Ветка `release` 16.4.39** — стандарт проекта (под неё живой тест).
- **AltV.Net 16.4.21** (NuGet) — последняя стабильная в линейке 16.4.x;
  отдельного пакета под .39 нет, ABI внутри 16.4.x стабилен. Если хост
  ругнётся на SDK — поднять до `16.4.28-rc.2`.
- **Скрипты `.ps1` — только ASCII.** Windows PowerShell 5.1 парсит `.ps1` в
  системной кодировке (CP1251), кириллица ломает парсер. Русский — в `docs/*.md`.
- **Рантайм `runtime/` — в `.gitignore`.** Бинарники alt:V в git не идут
  (CLAUDE.md). В git только исходники: ресурсы, шаблон конфига, скрипты.
- **Решение `.slnx`** (не `.sln`) — `dotnet` 10 создаёт новый формат по
  умолчанию; современные VS/Rider его понимают.

## Дальше

1. Тест живым клиентом GTA V (пункт очереди #4) — нужен реальный клиент.
2. Клиентские бинарники alt:V: как их отдавать/патчить (это уже Фаза 2 —
   лаунчер).
3. Отключить Sentry-телеметрию краш-хендлера сервера.
