# Совместимость RAGE:MP: первый срез

Статус на 26.09.2026: аудит исходников, без запуска GTA. «Есть» означает
только наличие указанного API в коде FloV:MP; совместимость поведения ещё
нуждается в сквозных тестах на реальных ресурсах RAGE:MP.

Документация RAGE:MP для сверки: [события](https://wiki.rage.mp/wiki/Getting_Started_with_Events),
[вызов клиентского события для одного игрока](https://wiki.rage.mp/wiki/Player%3A%3Acall),
[вызов для всех игроков](https://wiki.rage.mp/wiki/Players%3A%3Acall),
[переменные сущностей](https://wiki.rage.mp/wiki/Getting_Started_with_Entity_variables).

| Возможность RAGE:MP | FloV:MP сейчас | Где проверено / пробел |
|---|---|---|
| Клиент `mp.events.add/remove/call/callRemote` | Есть базовый API | `native/legacy-3889/client/src/script.cpp`; семантику аргументов и событий сверить в GTA |
| Клиент `mp.players`, `mp.vehicles`, `at`, `forEach`, `toArray` | Частично | `script.cpp`: read-only представления; методы сущностей RAGE покрыты не полностью |
| Клиент `player.getVariable`, `mp.events.addDataHandler` | Частично | `script.cpp`, серверный `StarterResource.Roster.cs`; только игроки, нет общих metadata других сущностей |
| Клиент `mp.browsers`, `browser.call` | Частично | `script.cpp`; CEF в GTA ещё не проверен |
| Клиент `mp.game` native и `mp.keys` | Частично | `script.cpp`; нужна сверка сигнатур и событий с реальными ресурсами |
| Сервер `mp.events.add/call` | Нет | Серверный JS runtime не реализован; есть только C# SDK и события alt:V |
| Сервер `mp.players`, `mp.vehicles`, `player.call`, `setVariable` | Нет как JS API | Низкоуровневые реестры и события C# есть; адаптера RAGE нет |
| Сервер `mp.colshapes`, `mp.markers`, `mp.blips`, `mp.checkpoints`, `mp.world` | Нет как JS API | В C# есть world object/blip/marker, но семантика RAGE и JS-объекты не реализованы |
| Переносчик существующих серверных ресурсов | Нет | Нужны диагностика unsupported API, преобразование конфигурации и проверка на примере |

Первый вертикальный срез: из серверного JS обработать `playerJoin` и
`mp.events.add` → `player.call` → клиентский `mp.events.add` с аргументами;
добавить `mp.players.at/toArray`, `player.setVariable/getVariable` и тесты
подключения бота. Затем машины и world-примитивы. Это даст проверяемый путь
переноса, не создавая пустые заглушки всего API сразу.
