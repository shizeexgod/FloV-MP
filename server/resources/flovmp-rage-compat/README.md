# Серверный JS API RAGE:MP — экспериментальный срез

Этот ресурс рассчитан на сервер с `js-module` и `flovmp-starter`, загружаемый
после Starter. Пока проверен Node-тестами с подменой событий alt-server, но
не запущен в самом `js-module` и не включён в release-пакет.

Реализованы: `mp.events.add/remove/reset/call/getAllOf/addCommand`,
`mp.players.at/atRemoteId/exists/toArray/forEach/call/length`, события
`playerJoin`, `playerReady`, `playerQuit`, вызов клиентского обработчика через
`player.call`, `player.setVariable/getVariable/hasVariable` для игроков
GTA Legacy b3889. Клиентское `mp.events.callRemote` попадает в серверный
обработчик первым аргументом `player`.

Для пробного запуска скопировать каталог в `server/resources/`, добавить
`"flovmp-rage-compat"` после `"flovmp-starter"` в `server.toml` и заменить
`gamemode/index.js` своим кодом. Нужен работающий `js-module` на сервере.
Запускать на отдельном тестовом сервере; обратимость — убрать ресурс из
списка и перезапустить сервер.

Ограничения: только native-игроки; переменные кешируются в этом ресурсе и
не отражают изменения из других ресурсов; нет серверных `mp.vehicles`,
colshapes, markers, blips, checkpoints, world, переноса БД и ресурсов.
`playerJoin` вызывается после READY клиента, что пока отличается от момента
подключения в RAGE:MP. Это не полная совместимость готового RAGE-сервера.

Проверка на Mac:

```sh
node --test server/resources/flovmp-rage-compat/compat.test.mjs
```

Основа маршрутизации: `flovmp:native:ready/left`, `flovmp:client:event`,
`flovmp:client:call`, `flovmp:player:setVariable` и реестр команд Starter.
API событий alt-server: <https://docs.altv.mp/js/articles/events/index.html>.
