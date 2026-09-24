# gamemode — ваш сервер

Эта папка — ваш код. Платформа (папки `server`, `voice`, `sdk`, `scripts`)
при обновлении заменяется, а `gamemode` не трогается никогда.

Платформа уже даёт: подключение и спавн, администрирование и команды, баны по
Social Club / IP / железу, голосовой чат, чат, права в базе данных.
Здесь пишется игровая логика: работы, дома, фракции, интерфейсы.

## Состав

| Путь | Что это |
|---|---|
| `src/GamemodeResource.cs` | серверная часть (C#), точка входа |
| `client/index.js` | клиентская часть (JavaScript), выполняется у игроков |
| `resource.toml` | описание ресурса для сервера |
| `Gamemode.csproj` | проект сборки |

## Сборка

Нужен **.NET SDK 8** (не только Runtime): https://dotnet.microsoft.com/download/dotnet/8.0

- Windows: `build.cmd`
- Linux: `./build.sh` (поставить SDK автоматически: `sudo ./build.sh --install-sdk`,
  перезапустить сервер после сборки: `sudo ./build.sh --restart`)

Результат — `server/resources/gamemode`. Ресурс сам добавляется в список
`resources` в `server/server.toml`. После сборки перезапустите сервер.

Разработка в Visual Studio / Rider / VS Code: откройте `Gamemode.csproj`.

Нужен второй ресурс (например, отдельный интерфейс или мини-игра)? Положите его
в `server/resources/<имя>` и допишите имя в список `resources` в
`server/server.toml` — сам добавляется только `gamemode`. Обновление платформы
ни ваши ресурсы, ни этот список не трогает.

## Как устроено взаимодействие с платформой

События сервера (C#):

| Событие | Когда |
|---|---|
| `flovmp:player:ready` (`IPlayer`) | игрок загрузил клиент и появился в мире |
| `flovmp:platform:ready` | платформа запущена — зарегистрируйте команды заново |
| `flovmp:command` (`IPlayer`, имя, аргументы) | игрок ввёл вашу команду |
| `flovmp:player:died` (`IPlayer`, убийца, оружие) | игрок погиб |

Регистрация чат-команды (имя, описание для `/help`, минимальный уровень
администратора, 0 — для всех):

```csharp
Alt.Emit("flovmp:commands:register", "job", "устроиться на работу", 0);
```

Настройки платформы из вашего ресурса:

```csharp
// точка появления новых и возрождённых игроков (x, y, z, поворот)
Alt.Emit("flovmp:settings:spawn", -1037.7f, -2737.8f, 20.2f, 330f);
// возрождение после смерти делает ваш ресурс (по событию flovmp:player:died)
Alt.Emit("flovmp:settings:respawn", false);
```

Уровень доступа проверяет платформа. Аргументы присылает игрок — проверяйте
их сами. Имена встроенных команд (`/car`, `/ban` и т.д.) заняты.

Уровень администратора игрока (0 — обычный игрок):

```csharp
player.GetLocalMetaData("adminLevel", out int level);
```

Сообщение в чат: `player.Emit("flovmp:chat:msg", "system", "", "текст {ff3d8a}цвет")`.

Обычные события alt:V тоже работают: `Alt.OnPlayerConnect`, `Alt.OnPlayerDead`,
`Alt.OnClient<...>` для событий из `client/index.js` и т.д.

## База данных

Строка подключения — та же, что у платформы (`config/flovmp.env`):

```csharp
var conn = new FloVMP.Core.Database.DatabaseConfig().BuildConnectionString();
```

Свои таблицы — файлами в `sql/migrations` с номером от `100`
(например `100_jobs.sql`): сервер применит их при старте.

## Мир и интерфейс для игроков GTA Legacy 1.0.3889.0

У игроков на GTA Legacy 1.0.3889.0 (клиент FloV:MP, а не alt:V) нет
`client/index.js`: мир и интерфейс для них задаёт сервер. Эти же события
работают из вашего C#-ресурса.

Карты можно положить файлами в `server/config/maps` (расстановки Menyoo `*.xml`
или `*.json`, пример — в `README.txt` той же папки) и перечитать командой
`reloadmaps` в консоли сервера.

```csharp
// объект: ID, модель, x y z, поворот rx ry rz, измерение, заморожен, коллизия
Alt.Emit("flovmp:world:object", "bench1", "prop_bench_01a", 200f, -930f, 29.7f, 0f, 0f, 90f, 0, true, true);
// метка на карте: ID, x y z, значок, цвет, масштаб, подпись, измерение
Alt.Emit("flovmp:world:blip", "cityhall", 200f, -930f, 30f, 60, 2, 1f, "Мэрия", 0);
// маркер: ID, тип 0..43, x y z, размер, цвет #rrggbbaa, измерение
Alt.Emit("flovmp:world:marker", "job", 1, 200f, -930f, 29f, 1.5f, "#ff3d8ab4", 0);
// 3D-надпись: ID, x y z, текст, дальность, измерение
Alt.Emit("flovmp:world:label", "jobText", 200f, -930f, 31f, "{ff3d8a}Работа{ffffff} — нажмите E", 20f, 0);
// NPC: ID, модель, x y z, поворот, сценарий, измерение
Alt.Emit("flovmp:world:npc", "clerk", "a_m_y_business_01", 201f, -931f, 29.7f, 180f, "WORLD_HUMAN_STAND_MOBILE", 0);
Alt.Emit("flovmp:world:remove", "object", "bench1");
```

Измерение `int.MinValue` — «во всех измерениях».

Если такой игрок всё же попал к вам объектом `IPlayer` (например из
`flovmp:player:ready`), с ним работает не всё — у него нет сущности движка:

| Работает | Не работает |
|---|---|
| `Id`, `Name`, `Position`, `Rotation`, `Dimension`, `Health`, `Armor`, `Model`, `CurrentWeapon` | всё остальное из alt:V API — вызов пропускается с предупреждением в журнале |
| `IsDead`, `IsInVehicle`, `Spawn`, `Kick`, `GiveWeapon`, `RemoveAllWeapons`, `Emit`, `SetLocalMetaData` | |
| `Vehicle`, `Seat` — машина серверного реестра (клиент 1.0.6+; у старых клиентов `null` и 0) | |

`player.Vehicle` у игрока 3889 — машина реестра. У неё работают `Id`,
`Model`, `Position`, `Rotation`, `Velocity`, `Dimension`, `NumberplateText`,
`EngineOn`, `SirenActive` (чтение), `LockState`, `BodyHealth`, `EngineHealth`,
`Driver`, `Repair()`, `Destroy()`. `Seat` — в нумерации alt:V (водитель — 1).
Передавать этот объект в функции движка alt:V нельзя: у него нет сущности движка.

Игрок 3889 для ресурсов — это его ID (у него нет сущности движка alt:V).
События о нём приходят с номером:

| Событие | Когда |
|---|---|
| `flovmp:native:ready` (id, ник) | игрок 3889 появился в мире |
| `flovmp:native:command` (id, ник, команда, аргументы) | ввёл вашу команду |
| `flovmp:native:died` (id, оружие) | погиб |
| `flovmp:native:menuSelect` (id, меню, номер пункта) | выбрал пункт меню |
| `flovmp:native:menuClose` (id, меню) | закрыл меню |
| `flovmp:native:key` (id, клавиша) | нажал клавишу из `flovmp:keys:bind` |

Управление игроком 3889 из вашего ресурса — по его ID:

```csharp
Alt.Emit("flovmp:native:teleport", id, 200f, -930f, 30f);   // переместить
Alt.Emit("flovmp:native:spawn", id, 200f, -930f, 30f, 90f); // возродить и повернуть
Alt.Emit("flovmp:native:health", id, 150);                  // здоровье 0..200
Alt.Emit("flovmp:native:armor", id, 100);                   // броня 0..100
Alt.Emit("flovmp:native:model", id, "a_m_y_business_01");   // модель персонажа
Alt.Emit("flovmp:native:weapon", id, "weapon_pistol", 120); // выдать оружие
Alt.Emit("flovmp:native:disarm", id);                       // забрать всё оружие
Alt.Emit("flovmp:native:dimension", id, 7);                 // измерение
Alt.Emit("flovmp:native:kick", id, "нарушение правил");     // отключить

// Прочитать состояние: запрос и ответ отдельным событием.
Alt.Emit("flovmp:native:query", id);        // один игрок
Alt.Emit("flovmp:native:queryAll");         // все игроки 3889
Alt.OnServer<int, string, float, float, float, float, int, int, string, int, bool, string, int>(
    "flovmp:native:state",
    (id, nick, x, y, z, heading, health, armor, weapon, dimension, inVehicle, vehicleModel, seat) =>
    {
        // позиция, здоровье, броня, оружие, измерение и транспорт игрока
    });

// Игрок вышел: сохранить данные, снять таймеры, закрыть сделки.
Alt.OnServer<int, string, string>("flovmp:native:left", (id, nick, reason) => { });
```

## Транспорт (клиент 1.0.6+)

Машины — сущности сервера: у каждой свой ID, машина остаётся стоять, когда из
неё вышли, здоровье кузова и двигателя считает сервер. Машины, созданные
вашим ресурсом или `/car`, платформа сама не убирает — только `/dv` или вы.
Машины городского трафика, в которые садились игроки, убираются, если стоят
пустыми и рядом никого нет (`vehicles.abandoned_ttl_sec`).

Места: **−1 — водитель**, 0…15 — пассажиры (как в нативах GTA; в RAGE:MP
водитель — 0, в alt:V — 1).

```csharp
// Создать: ключ запроса (любая строка), модель, x y z, курс, измерение,
// номер (пусто — случайный), сохранять ли между перезапусками.
Alt.Emit("flovmp:vehicle:create", "garage-42", "sultan", 200f, -930f, 30f, 90f, 0, "RP 042", true);
Alt.OnServer<string, int, string>("flovmp:vehicle:created", (key, vehicleId, error) =>
{
    // vehicleId == 0 — отказ, причина в error
});

Alt.Emit("flovmp:vehicle:putInto", playerId, vehicleId, -1);  // посадить за руль
Alt.Emit("flovmp:vehicle:removeFrom", playerId);              // высадить
Alt.Emit("flovmp:vehicle:engine", vehicleId, true);
Alt.Emit("flovmp:vehicle:lock", vehicleId, true);
Alt.Emit("flovmp:vehicle:repair", vehicleId);
Alt.Emit("flovmp:vehicle:health", vehicleId, 1000f, 1000f);   // кузов 0..1000, двигатель -4000..1000
Alt.Emit("flovmp:vehicle:plate", vehicleId, "RP 042");        // латиница, цифры, пробел, до 8
Alt.Emit("flovmp:vehicle:persistent", vehicleId, true);
Alt.Emit("flovmp:vehicle:remove", vehicleId);

// Прочитать состояние: запрос и ответ отдельным событием.
Alt.Emit("flovmp:vehicle:query", vehicleId);
Alt.Emit("flovmp:vehicle:queryAll");
Alt.OnServer<int, string, float, float, float, float, int, string, bool, bool, bool, float, float, int, bool, bool>(
    "flovmp:vehicle:state",
    (id, model, x, y, z, heading, dimension, plate, engineOn, locked, sirenOn,
     bodyHealth, engineHealth, driverId, persistent, fromTraffic) => { });

// События
Alt.OnServer<int, int, int>("flovmp:vehicle:enter", (playerId, vehicleId, seat) => { });
Alt.OnServer<int, int, int>("flovmp:vehicle:leave", (playerId, vehicleId, seat) => { }); // пересел = leave + enter
Alt.OnServer<int, float, float, int>("flovmp:vehicle:damage", (vehicleId, bodyLoss, engineLoss, driverId) => { });
Alt.OnServer<int>("flovmp:vehicle:destroyed", vehicleId => { });                        // двигатель дошёл до −4000
Alt.OnServer<int, string>("flovmp:vehicle:removed", (vehicleId, reason) => { });        // api, command, abandoned
```

Свои данные машины (владелец-персонаж, страховка, тюнинг) храните в своих
таблицах по ID машины.

### Сохранение машин

Машина с флагом «сохранять» (`persistent`) переживает перезапуск сервера: платформа
пишет её в таблицу `vehicles` (или в `flovmp-data/vehicles.json`, если базы нет) —
по таймеру, сразу при парковке и перед остановкой. После запуска машины встают
на свои места с теми же ID, заглушёнными, и приходит событие:

```csharp
Alt.OnServer<int>("flovmp:vehicles:loaded", restored => Alt.Emit("flovmp:vehicle:queryAll"));
```

Внешний ключ ваших таблиц — на `vehicles (world, id)` с `ON DELETE CASCADE`:
убранная машина уйдёт из базы вместе с вашими строками. Хотите хранить машины
полностью сами — `vehicles.persistence = off`.

### Настройки из кода

Любую настройку `server/config/client.cfg` ваш ресурс может поменять на лету, с той
же проверкой значения, что и в файле. Значение из кода важнее файла и переживает
`reloadsettings`:

```csharp
Alt.Emit("flovmp:settings:set", "vehicles.register_traffic", "off"); // только свои машины
Alt.Emit("flovmp:settings:set", "vehicles.plate_format", "RP 9999");  // свой формат номеров
```

Что настраивается в транспорте: `vehicles.max_registered`, `vehicles.abandoned_ttl_sec`,
`vehicles.register_traffic`, `vehicles.register_cooldown_sec`, `vehicles.enter_distance`,
`vehicles.plate_format`, `vehicles.persistence`, `vehicles.save_interval_sec`,
`vehicles.restore_damage`, `vehicles.world` — описание каждой в `client.cfg`.
Уровни доступа к встроенным командам (`/car`, `/dv`, `/fix` …) — в
`server/config/admin-commands.cfg`: 0 — доступна всем, 1…8 — с этого уровня
администратора (8 — только владелец).

## Античит: журнал подозрений

Все проверки платформы (движение, урон, машины, оружие, модели, патроны, ход
часов клиента) не наказывают сами — они добавляют вес к счёту игрока, а счёт
со временем тает. Решает порог: по умолчанию администраторам в чат уходит
предупреждение, отключение выключено (`anticheat.kick_score = 0`). Веса,
пороги и списки — раздел «Античит» в `client.cfg` (или `flovmp:settings:set`).
Администраторы: `/ac <ID>` — счёт и последние подозрения, `/acforgive <ID>` — сбросить.

```csharp
// каждое подозрение и пересечение порога ("notify" / "kick")
Alt.OnServer<int, string, float, float, string>("flovmp:anticheat:suspicion", (id, type, weight, score, details) => { });
Alt.OnServer<int, string, float>("flovmp:anticheat:threshold", (id, level, score) =>
{
    // ваше решение: бан, заморозка, запись в свой журнал, сообщение в Discord
});

// своё подозрение (дюп денег, вход в закрытую зону) — со своим весом
Alt.Emit("flovmp:anticheat:report", id, "перевод 1 000 000 за секунду", 40f);
Alt.Emit("flovmp:anticheat:forgive", id);
Alt.Emit("flovmp:anticheat:query", id);   // ответ: flovmp:anticheat:state (id, счёт, JSON истории)
```

Оружие и модели, выданные сервером (`/weapon`, `/skin`, `flovmp:native:weapon`,
`flovmp:native:model`, `player.GiveWeapon`), платформа запоминает: с
`anticheat.issued_weapons_only = on` любое другое оружие в руках — подозрение, а
попаданий больше, чем выдано патронов, быть не может. Выдавайте патроны своим
ресурсом тем же `flovmp:native:weapon` — тогда учёт сходится.

Интерфейс игроку — по его ID:

```csharp
Alt.Emit("flovmp:ui:notify", id, "Вы устроились на работу", 4000);
// меню: ID игрока, ID меню, заголовок, пункты — JSON-массив строк или {"label","desc"}
Alt.Emit("flovmp:ui:menu", id, "shop", "Магазин", "[\"Вода — $5\", {\"label\":\"Хлеб — $3\",\"desc\":\"Утоляет голод\"}]");
Alt.Emit("flovmp:ui:closeMenu", id);
Alt.OnServer<int, string, int>("flovmp:native:menuSelect", (id, menu, index) => { /* выбор пункта */ });

// клавиша, о нажатии которой клиент сообщит серверу (A..Z, 0..9, F1..F12)
Alt.Emit("flovmp:keys:bind", "E");
Alt.OnServer<int, string>("flovmp:native:key", (id, key) => { /* игрок нажал E */ });
```

Сервер принимает выбор только из меню, которое сам открыл этому игроку.

Свои модели (не из GTA) у игроков должны быть установлены как дополнение к
игре — сервер их не передаёт.

## Клиентская часть

`client/index.js` — модуль JavaScript с API `alt-client` и `natives`.
Свои HTML-интерфейсы кладите в `client/html/` и открывайте через
`new alt.WebView('http://resource/client/html/имя/index.html')`.
Клавиши F1, F3, F4, F5, F8, F11, T, L, K, 2 заняты платформой; N — голосовой чат (настраивается в alt:V).
