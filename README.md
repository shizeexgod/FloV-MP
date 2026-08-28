# FloV:MP

Собственный мультиплеер-движок и фирменный лаунчер для **Florida V** —
поверх локально работающих (мёртвых как сервис) бинарников **alt:V**,
полностью автономно, без обращения к чужой инфраструктуре.

> RAGE:MP и alt:V закрыты Take-Two в 2026. Заказчик не хочет оставаться
> только на FiveM — нужен независимый мультиплеер под своим брендом.
> Юридический риск владельцем осознан и принят.

Полный контекст, решения и правила — в [`CLAUDE.md`](CLAUDE.md).
Текущий статус и очередь задач — в [`agent_state.md`](agent_state.md).

## Стек

| Слой | Технология |
|---|---|
| Сервер (гейм-логика) | C# / .NET 8, `coreclr-module` alt:V |
| Клиент (игровой UI/HUD) | JS/TS + HTML (NUI), `js-module` alt:V |
| Лаунчер | C# / WPF / .NET 8 |

## Структура

```
server/     — C#-геймод (FloVMP.Gamemode) под alt:V coreclr-module
client/     — клиентский JS-ресурс (flovmp-client)
launcher/   — WPF-лаунчер: Core (логика) + UI + Tests (xUnit)
config/     — шаблон server.toml
scripts/    — сборка рантайма, манифесты, импорт клиента (PowerShell)
docs/       — документация по разделам (engine/ gameplay/ launcher/ admin/ instructions/ archive/), карта — docs/README.md
runtime/    — собранный рантайм (в git НЕ хранится, см. .gitignore)
```

Бинарники движка alt:V (~2 ГБ) — внешняя зависимость (`C:\ViMP backup\
backup-altv`), в репозитории не хранятся.

## Быстрый старт (сервер)

```powershell
powershell -File scripts/assemble-runtime.ps1   # собрать рантайм из бэкапа alt:V
powershell -File scripts/run-server.ps1          # запустить на :7788
```

Ждём `[C#] [FloV:MP] core: server fully started`.

## Быстрый старт (лаунчер)

```powershell
dotnet build launcher/FloVMP.Launcher.slnx -c Debug
dotnet test  launcher/FloVMP.Launcher.slnx -c Debug
launcher/src/FloVMP.Launcher/bin/Debug/net8.0-windows/FloVMP.Launcher.exe
```

## Живой тест (2 игрока)

См. [`docs/instructions/live-test-guide.md`](docs/instructions/live-test-guide.md) — нужен GTA V.

## Документация

Карта всех документов по разделам — **[`docs/README.md`](docs/README.md)**
(`engine/` · `gameplay/` · `launcher/` · `admin/` · `instructions/` · `archive/`).

## Разработка

- Отвечаем и документируем на русском; `.ps1` и `Alt.Log` — ASCII (кодировка
  консоли/парсера).
- `git commit` после каждого раунда правок; `push` — по правилам сессии.
- В репозитории работают только владелец и Claude.
