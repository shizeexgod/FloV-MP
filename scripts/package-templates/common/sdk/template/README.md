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
| `Id`, `Name`, `Position`, `Rotation`, `Dimension`, `Health`, `Armor`, `Model`, `CurrentWeapon` | `Vehicle` — всегда `null`, `Seat` — всегда 0 |
| `IsDead`, `IsInVehicle`, `Spawn`, `Kick`, `GiveWeapon`, `RemoveAllWeapons`, `Emit`, `SetLocalMetaData` | всё остальное из alt:V API — вызов пропускается с предупреждением в журнале |

«Игрок в машине?» — `player.IsInVehicle`. Какая именно машина — платформа
пока не сообщает: транспорт 3889 живёт в игре игрока, а не в движке.

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
