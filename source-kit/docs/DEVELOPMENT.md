# Разработка на FloV:MP

## Сборка и тесты

```bash
dotnet build server/FloVMP.sln -c Release
dotnet test server/FloVMP.sln
python scripts/pack_server.py --os windows      # после добавления разрешённого engine/
python scripts/pack_server.py --skip-build      # только перекладка пакета
node scripts/client-sim/entry_flow.test.mjs     # симуляция клиента: вход, загрузка
node scripts/client-sim/admin_keys.test.mjs     # клавиши администратора и права
node scripts/client-sim/commands_sync.test.mjs  # список команд в чате и консоли
powershell -ExecutionPolicy Bypass -File scripts/smoke_windows.ps1   # живой прогон Windows-пакета
sudo scripts/smoke_linux.sh dist/server/flovmp-server-*-linux.tar.gz  # то же на Linux
```

В обычном source-kit папки `engine/` нет. Для полноценного runtime-пакета её
можно добавить только из разрешённого дистрибутива alt:V; до этого выполняйте
сборку, тесты и симуляции клиента, а упаковку сервера не запускайте.

Симуляция клиента загружает настоящий `client/index.js` с подменёнными
модулями `alt-client` и `natives` — без игры, за секунды.

`smoke_windows.ps1` распаковывает свежий архив в папку с русскими буквами,
запускает сервер на свободных портах, собирает `gamemode` из шаблона,
выполняет команды консоли и проверяет лог — то же, что проходит владелец
сервера после покупки. `smoke_linux.sh` ставит пакет отдельным инстансом
(своя папка, служба и порты, без базы), собирает `gamemode`, обновляет поверх,
проверяет, что код владельца цел, и удаляет — уже работающие серверы не трогает.

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
| `flovmp:player:died` | платформа → ресурс | `IPlayer`, убийца (`IEntity`), хэш оружия |
| `flovmp:settings:spawn` | ресурс → платформа | x, y, z, поворот (`float`) — точка появления |
| `flovmp:settings:respawn` | ресурс → платформа | `bool`: `false` — возрождение делает ресурс |

Метаданные игрока: `adminLevel` (local meta: видят сервер и сам игрок, другие
клиенты — нет) — уровень администратора игрока.

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

Команды — `HandleCommand` в `StarterResource.cs`. Уровень команды **не пишется
в обработчике**: его проверяет один общий вход по реестру
`FloVMP.Core.Admin.AdminCommandRegistry`, а владелец сервера правит уровни в
`server/config/admin-commands.cfg`. Проверка внутри обработчика разошлась бы с
файлом и молча запрещала бы разрешённое.

Новая команда — три шага:

```csharp
// 1) FloVMP.Core/Admin/AdminCommandRegistry.cs — объявить команду и уровень
Register("mycmd", 8, "/mycmd <ID>", "что делает команда");

// 2) StarterResource.cs — добавить имя в BuiltinCommands,
//    чтобы чужой ресурс не перехватил его

// 3) StarterResource.cs — обработчик; права уже проверены
case "mycmd":
    // аргументы: parts[1..]; проверяйте каждый
    break;
```

`IsAdmin(player, n)` — у игрока уровень администратора не ниже n.
`MayUse(player, "имя")` — можно ли игроку эту команду по реестру; так
проверяются события, приходящие от клиента.
`CanActOn(admin, target)` — цель ниже по уровню (для силовых команд).

## Проверка движения и замер тика

`AntiCheatService` (ядро) подключён к платформе: раз в полсекунды сверяет
координаты игроков и замечает телепорт, скорость и полёт. Если ваш код
телепортирует игрока сам, скажите об этом — иначе это выглядит как читерство:

```csharp
// внутри платформы: NotifyTeleport(player);
// из своего ресурса: телепортируйте через события платформы либо
// отключите проверку для своего сценария (FLOVMP_ANTICHEAT=off).
```

`FloVMP.Core.Diagnostics.TickProfiler` меряет время внутри тика без выделения
памяти: `using var _ = TickProfiler.Measure(id);`, где `id` — результат
`TickProfiler.Register("имя")` в статическом поле. Отчёт — команда `perf` в
консоли сервера, раз в минуту он также уходит в лог.

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

`engine/` — локальный сторонний runtime alt:V 16.4.39 (ветка release), который
обычно не входит в source-kit. Если у владельца есть право на его использование
и передачу, сборщик принимает его из `--altv-backup` вместе с
`--include-altv-engine` (или из локальной `engine/`), переименовывает бинарники
и патчит модуль C# под имя хоста `FloV.Net.Host`. Без разрешённого runtime можно
собирать и тестировать собственный код, но нельзя выпустить готовый серверный
архив.
