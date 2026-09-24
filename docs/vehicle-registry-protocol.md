# Серверный реестр транспорта — протокол FLOV/2

Пункт 8 roadmap, линия **1.0.6-beta**. Поля и имена сообщений утверждены
владельцем 24.09.2026. Этапы: **8a** — серверное ядро + протокол + боты;
**8b** — клиент (`game.cpp`); **8c** — публичный API геймода; **8d** —
сохранение в базу.

## Решения владельца (24.09.2026)

- **Сервер — владелец машины.** `/car` и API создают машину в реестре.
- **Трафик.** Машина, заспавненная игрой клиента, в реестр не попадает.
  Сел игрок за руль такой машины — клиент шлёт `VREQ`, сервер выдаёт ID,
  игрок становится водителем. Ограничения: одна регистрация в 2 с на
  игрока, общий потолок `vehicles.max_registered` (по умолчанию 1000).
- **Здоровье кузова и двигателя — серверные поля.** От водителя
  принимается только вниз, вверх — только командой сервера (ремонт, `VSET`).
  Двери, стёкла, колёса — позже.
- **`STATE` не меняется.** У клиента 1.0.6+ поля `vehModel`, `vehOwner`,
  `seat` сервер игнорирует; место в машине — только из `VENTER` / `VLEAVE`
  / `VOWN`. Флаг «в транспорте» в `flags` остаётся (анимации, проверки
  урона). Причина: одно поле с двумя смыслами по версии клиента — источник
  тихих багов, а `STATE` остаётся сообщением про игрока.
- **Машина без водителя** стоит на последней позиции из `VSYNC`, у всех
  заморожена и поставлена на землю; первый севший за руль становится
  водителем. Передача физики ближайшему игроку — позже.
- **Выход из машины её не удаляет.** Машина остаётся стоять у всех на месте,
  где её бросили. Машины сервера (`/car`, API геймода) платформа не убирает
  никогда — как в alt:V и RAGE:MP: только `/dv` или геймод.
- **Брошенный трафик** (уточнено владельцем 24.09): машина из трафика игры,
  в которую кто-то садился, убирается, только когда она пустая и рядом
  (в радиусе видимости) не было ни одного игрока `vehicles.abandoned_ttl_sec`
  секунд (по умолчанию 300, 0 — никогда). Так делает сама GTA и FiveM.
  Сохраняемые (`persistent`) не трогаются.
- **Версии.** Из `HELLO` сравниваем `major.minor.patch`, суффикс
  (`-beta`) игнорируем, `dev` считается новым клиентом. Ниже 1.0.6 — путь
  через `STATE`, как в 1.0.4/1.0.5; 1.0.6 и выше — реестр. Старый путь
  держим один релиз и убираем после 1.0.7.
- **Смешанный онлайн.** Старый клиент видит машину реестра, только пока в
  ней есть водитель (через его `PSTATE`); пустую не видит. При входе
  старого клиента сервер пишет владельцу предупреждение в журнал.
- **Номер** по умолчанию генерирует сервер (случайный, как в игре), геймод
  может задать свой через API. «Сохраняемая» — только серверное свойство,
  в протокол не попадает.
- **Античит.** `VSYNC` — только от водителя (иначе отброс + журнал), плюс
  `VehiclePhysicsGuardian` на скорость и телепорт; в режиме `log` — только
  журнал.
- **Сохранение (8d)** — платформенная таблица, миграция с номером меньше
  100, только машины с `persistent: true`. Свои поля геймод держит в своих
  таблицах по постоянному ID машины.

## Сообщения

Числа — как везде в FLOV/2: `0.###`, инвариантная культура. Углы — градусы.
Булевы — `0`/`1`. Места: `-1` — водитель, `0…15` — пассажиры (как в
нативах GTA).

### Сервер → клиент

| Сообщение | Поля | Когда |
|---|---|---|
| `VADD` | `id model x y z rx ry rz dimension flags plate` | машина вошла в зону видимости; повторный вход — снова `VADD` |
| `VSTATE` | `id x y z rx ry rz vx vy vz engineOn sirenOn locked bodyHp engHp` | только при изменении, частота по дальности как у `PSTATE` |
| `VDEL` | `id` | машина вышла из зоны видимости или удалена |
| `VOWN` | `id playerId seat` | кто сидит на месте; `seat -1` — водитель; `playerId 0` — место свободно |
| `VREG` | `reqId id` | ответ на `VREQ`: машина зарегистрирована, отправитель — её водитель |
| `VREJ` | `reqId причина` | ответ на `VREQ`: отказ |
| `VSET` | `id engineOn sirenOn locked bodyHp engHp` | команда сервера водителю: применить значения (ремонт, заглушить, закрыть) |

`flags` в `VADD`: 1 — двигатель, 2 — сирена, 4 — закрыта.
`plate` — до 8 символов.

### Клиент → сервер

| Сообщение | Поля | Правило |
|---|---|---|
| `VREQ` | `reqId model` | игрок сел за руль машины трафика; позиция и измерение — из его последнего `STATE` |
| `VSYNC` | `id x y z rx ry rz vx vy vz engineOn sirenOn bodyHp engHp` | только текущий водитель; от не-водителя — отброс + журнал; `bodyHp`/`engHp` — только вниз |
| `VENTER` | `id seat` | игрок сел в машину реестра |
| `VLEAVE` | `id` | игрок вышел |

## Правила обмена (уточнения реализации 8a)

1. **Полный снимок при входе в зону видимости** = `VADD`, сразу за ним
   `VSTATE` (скорость и здоровье в `VADD` не входят) и `VOWN` на каждое
   занятое место. Дальше — только `VSTATE` при изменениях и `VOWN` при
   пересадках. Вышла из зоны — `VDEL`; вернулась — снова полный снимок.
2. **`VOWN` — истина о месте.** Отказ на `VENTER` (закрыта, далеко, место
   занято, другое измерение) приходит как `VOWN id <кто на самом деле
   сидит или 0> seat` только этому игроку: клиент видит, что место не его,
   и высаживает своего персонажа. Отдельного сообщения-отказа нет.
3. **Водитель не получает `VSTATE` своей машины** — иначе сервер дёргал
   бы локальную физику. Всё, что сервер меняет сам (ремонт, двигатель,
   замок), уходит водителю через `VSET`, остальным — обычным `VSTATE`.
4. **`/car` у клиента 1.0.6+** создаёт машину в реестре на месте игрока и
   сажает его за руль: игрок получает `VADD` + `VOWN id себя -1`, клиент
   создаёт машину и сажает в неё персонажа.
5. **`VREQ`, когда игрок уже в машине реестра**, сначала высаживает его из
   неё (как `VLEAVE`), потом регистрирует новую.
6. **Смена измерения или выход игрока** освобождает его место.
   Старый клиент (< 1.0.6), севший пассажиром к водителю 1.0.6+, для
   остальных остаётся пешим; сервер пишет об этом отдельную строку в журнал
   (`[FloV:MP Транспорт] переходный период 1.0.6: …`) — раз на поездку.
   `/dv` снаружи убирает ближайшую свою машину в радиусе 10 м.
7. **Водитель вышел** — машина останавливается: скорость обнуляется,
   остальным уходит `VSTATE` с нулевой скоростью.
8. **Дистанция `VENTER`** — не дальше 10 м от последнего `STATE` игрока.
9. **Здоровье:** кузов 0…1000, двигатель −4000…1000 (как в игре), значения
   от водителя — `min(серверное, присланное)`.
10. **ID машины** — одно пространство `uint` на сервер, не пересекается с
    ID игроков по смыслу (разные сообщения). В 8d сохранённые машины
    возвращаются с тем же ID, а новые получают ID выше максимального
    сохранённого.

## Соответствие имён FloV:MP ↔ RAGE:MP ↔ alt:V

Справочник для слоя совместимости (пункт 17 roadmap) и для имён в
реестре. Источники: публичные типы RAGE:MP (`@ragempcommunity/types-server`
и `types-client` 2.1.9 из npm) и сборка `AltV.Net` 16.4.21, которую мы и
так используем. Взяты только имена и контракты, не код.

### Сущность и создание

| FloV:MP (реестр, 8a/8c) | RAGE:MP (сервер) | alt:V C# |
|---|---|---|
| `VehicleRegistry` | `mp.vehicles` (`VehicleMpPool`) | `Alt.GetAllVehicles()` |
| `Create(model, pos, rot, dimension, options)` | `mp.vehicles.new(model, position, { heading, dimension, engine, locked, numberPlate, color, alpha })` | `Alt.CreateVehicle(model, pos, rot, streamingDistance, isStatic)` |
| `Remove(id)` | `vehicle.destroy()` | `vehicle.Destroy()` |
| `Get(id)` | `mp.vehicles.at(id)` | `Alt.GetVehicleById(id)` |
| `RegisteredVehicle.Id` | `vehicle.id` | `vehicle.Id` |
| `Model` | `vehicle.model` | `vehicle.Model` |
| `Position`, `Rotation` | `vehicle.position`, `vehicle.rotation` (+ `heading`) | `vehicle.Position`, `vehicle.Rotation` |
| `Velocity` | `vehicle.velocity` (только чтение) | `vehicle.Velocity` (только чтение) |
| `Dimension` | `vehicle.dimension` | `vehicle.Dimension` |
| `Plate` | `vehicle.numberPlate` | `vehicle.NumberplateText` |
| `Persistent` | — (в RP-проектах своё поле в БД) | — |

### Состояние

| FloV:MP | RAGE:MP | alt:V C# |
|---|---|---|
| `EngineOn` | `vehicle.engine` | `vehicle.EngineOn` |
| `SirenOn` | `vehicle.siren` (только чтение) | `vehicle.SirenActive` |
| `Locked` | `vehicle.locked` (bool) | `vehicle.LockState` (`VehicleLockState`, перечисление) |
| `BodyHealth` | `vehicle.bodyHealth` | `vehicle.BodyHealth` (`uint`) |
| `EngineHealth` | `vehicle.engineHealth` (только чтение) | `vehicle.EngineHealth` (`int`) |
| `Repair()` | `vehicle.repair()` | `vehicle.Repair()` |
| `Driver` | `vehicle.controller` / `getOccupant(0)` | `vehicle.Driver` |
| `Occupants` | `vehicle.getOccupants()` | `vehicle.Passengers` |
| `GetOccupant(seat)` | `vehicle.getOccupant(seat)` | — |
| `Controller` (кто считает физику = водитель) | `vehicle.controller` | `vehicle.NetworkOwner`, `SetNetworkOwner()` |

### Игрок и места

| FloV:MP | RAGE:MP | alt:V C# |
|---|---|---|
| `IPlayer.Vehicle` (8c, сейчас `null` у 3889) | `player.vehicle` | `player.Vehicle` |
| `IPlayer.Seat` | `player.seat` | `player.Seat` (`byte`) |
| `PutIntoVehicle(player, vehicle, seat)` (8c) | `player.putIntoVehicle(vehicle, seat)` | `player.SetIntoVehicle(vehicle, seat)` |
| `RemoveFromVehicle(player)` (8c) | `player.removeFromVehicle()` | — (задача клиента) |

**Нумерация мест у всех разная — главный источник ошибок при переносе:**

| Место | FloV:MP / нативы GTA | RAGE:MP (`RageEnums.VehicleSeat`) | alt:V (`player.Seat`) |
|---|---|---|---|
| водитель | `-1` | `0` (`DRIVER`) | `1` |
| 1-й пассажир | `0` | `1` (`PASSENGER_1`) | `2` |

Слой совместимости переводит: `rage = ours + 1`, `altv = ours + 2`.

### События

| FloV:MP (8c, события ресурса) | RAGE:MP сервер | alt:V C# |
|---|---|---|
| `flovmp:vehicle:enter (player, vehicle, seat)` | `playerEnterVehicle(player, vehicle, seat)` | `OnPlayerEnterVehicle(vehicle, player, seat)` |
| `flovmp:vehicle:leave (player, vehicle, seat)` | `playerExitVehicle(player, vehicle)` | `OnPlayerLeaveVehicle(vehicle, player, seat)` |
| `flovmp:vehicle:seat (player, vehicle, old, new)` | — | `OnPlayerChangeVehicleSeat` |
| `flovmp:vehicle:damage (vehicle, bodyLoss, engineLoss)` | `vehicleDamage(vehicle, bodyHealthLoss, engineHealthLoss)` | `OnVehicleDamage` |
| `flovmp:vehicle:destroyed (vehicle)` | `vehicleDeath(vehicle)` | `OnVehicleDestroy` |
| `flovmp:vehicle:siren (vehicle, on)` | `vehicleSirenToggle(vehicle, toggle)` | `OnVehicleSiren` |
| `flovmp:vehicle:removed (vehicle)` | `entityDestroyed(entity)` | `OnVehicleRemove` |

Клиентские события RAGE:MP называются иначе: `playerEnterVehicle(vehicle,
seat)` и **`playerLeaveVehicle`** (на сервере — `playerExitVehicle`).
Слой совместимости должен различать сторону.

Имена событий FloV:MP в 8c — предложение, утверждаются вместе с API.

Уже есть в коде: `FloVMP.Core/Bridge/RageCompatibility.cs` — атрибуты
`[Command]` и `[RemoteEvent]` в стиле серверного C# RAGE:MP (тесты
`RageCompatibilityTests`). Слой совместимости пункта 17 стоит строить от него.

## Проверка без игры

    dotnet run -c Release --project server/tools/FloVMP.VehicleHarness -- 17798
    python native/legacy-3889/client/tools/bot.py --port 17798 --vehicle-test

Стенд — настоящий шлюз `NativeServer` и `NativeVehicleService` без alt:V и
лицензии. Против живого сервера тот же сценарий (`--port 7798`) проверяет
ещё и вид машины у старого клиента через `PSTATE`.
