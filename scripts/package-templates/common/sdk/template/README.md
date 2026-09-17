# gamemode — ваш сервер

Эта папка — ваш код. Платформа (папки `server`, `voice`, `sdk`, `scripts`)
при обновлении заменяется, а `gamemode` не трогается никогда.

Платформа уже даёт: вход на сервер, администрирование и команды, баны по
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
player.GetStreamSyncedMetaData("adminLevel", out int level);
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

## Клиентская часть

`client/index.js` — модуль JavaScript с API `alt-client` и `natives`.
Свои HTML-интерфейсы кладите в `client/html/` и открывайте через
`new alt.WebView('http://resource/client/html/имя/index.html')`.
Клавиши F1, F3, F4, F5, F8, F11, T, L, K, 2 заняты платформой; N — голосовой чат (настраивается в alt:V).
