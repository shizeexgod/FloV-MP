# Разработка на FloV:MP

## Сборка и тесты

```bash
dotnet build server/FloVMP.slnx -c Release
dotnet test server/FloVMP.slnx
python scripts/pack_server.py --os windows      # или linux / all
python scripts/pack_server.py --skip-build      # только перекладка пакета
```

Проекты используют .NET 8 (`server/Directory.Build.props`): под эту версию
собран хост C# движка, поднимать её нельзя.

## Два способа писать свою логику

1. **Свой ресурс рядом с платформой** — рекомендуемый. В пакете это папка
   `gamemode` (шаблон — `scripts/package-templates/common/sdk/template`).
   Платформа остаётся нетронутой и обновляется отдельно.
2. **Правка самой платформы** — `server/src/FloVMP.Starter`, `server/src/FloVMP.Core`,
   `client/resources/flovmp-client`. После правки — пересборка пакета.

## API платформы для своих ресурсов

Серверные события (C#, `Alt.OnServer` / `Alt.Emit`):

| Событие | Направление | Аргументы |
|---|---|---|
| `flovmp:commands:register` | ресурс → платформа | имя, описание, мин. уровень администратора |
| `flovmp:command` | платформа → ресурс | `IPlayer`, имя команды, строка аргументов |
| `flovmp:player:ready` | платформа → ресурс | `IPlayer` |
| `flovmp:platform:ready` | платформа → ресурсы | — |

Метаданные игрока: `adminLevel` (stream synced) — текущий уровень на дежурстве.

События клиенту (`player.Emit`), которые обрабатывает `flovmp-client`:

| Событие | Аргументы |
|---|---|
| `flovmp:chat:msg` | вид (`system`, `player`, `me`, `do`, `ooc`, `shout`, `whisper`, `admin`), автор, текст с цветами `{RRGGBB}` |
| `flovmp:chat:clear` | — |
| `starter:setWeather` | тип погоды GTA (`CLEAR`, `RAIN`, …) |
| `starter:setTime` | час, минута |
| `starter:setFrozen` | bool |
| `starter:revive` | — |

## Чат-команды в платформе

Команды — `HandleCommand` в `StarterResource.cs`. Шаблон команды:

```csharp
case "mycmd":
    if (!IsAdmin(player, 2)) { SendChatMessage(player, "Нет прав"); return; }
    // аргументы: parts[1..]; проверяйте каждый
    break;
```

`IsAdmin(player, n)` — игрок на дежурстве с уровнем не ниже n.
`CanActOn(admin, target)` — цель ниже по уровню (для силовых команд).
Новое имя команды добавьте в `BuiltinCommands`, чтобы ресурсы не могли его перехватить.

## Своя таблица в базе

Файл `sql/migrations/100_<название>.sql` (в пакете — там же). Сервер применит
его при старте один раз. Уже применённый файл не редактируйте — добавьте новый.

```csharp
var cs = new FloVMP.Core.Database.DatabaseConfig().BuildConnectionString();
await using var conn = new MySqlConnector.MySqlConnection(cs);
await conn.OpenAsync();
```

Запросы к базе — не на главном потоке: `Task.Run`, результат применять к
игрокам в `OnTick` или через событие.

## Клиент

`client/resources/flovmp-client/client/index.js` — модуль с API `alt-client`
и `natives`. Интерфейсы — `client/html/*/index.html` (открываются как
`alt.WebView`). Клавиши задаются в `KEYBINDS` в начале файла.

Правило: клиент ничего не решает сам. Любое действие, которое меняет мир или
права, — событие на сервер (`alt.emitServer`) с проверкой там.

## Лицензия

Файл `license.flv` выдаёт портал. Проверка — `server/src/FloVMP.Core/Licensing/LicenseFile.cs`,
тесты — `server/tests/FloVMP.Core.Tests/LicenseFileTests.cs`.

## Движок

`engine/` — нужная сборщику часть alt:V 16.4.39 (ветка release):
`server`, `coreclr-module`, `js-module`, `data`, `voice-server`. Сборщик
переименовывает бинарники и патчит модуль C# под имя хоста `FloV.Net.Host`.
