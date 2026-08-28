# Фаза 3 — HUD (задача #14)

Обновлено: 2026-08-28
Статус: **проводка готова, boot-тест зелёный. Визуально не проверен (нужен живой клиент).**

Второй срез Фазы 3. Сервер — источник истины, клиент только рисует.

## Поток

```
игрок вошёл + заспавнен  → HudSystem.OnAuthed → Emit flovmp:hud:init {serverName}
                            клиент создаёт постоянный HUD-WebView
каждый тик ресурса        → GamemodeResource.OnTick → HudSystem.Tick
  (троттл 1000 мс)        → по каждому вошедшему: Emit flovmp:hud:tick
                            {hp, armor, cash, online, hour, minute}
disconnect               → HudSystem.OnDisconnect, клиент closeHud()
```

## Код

### Сервер

| Файл | Роль |
|---|---|
| `server/src/FloVMP.Gamemode/Systems/Hud/HudSystem.cs` | `ConcurrentDictionary<uint, Account>` вошедших; `Tick()` с аккумулятором по `Stopwatch` (порог 1000 мс), обход `Alt.GetAllPlayers()`, `player.Emit("flovmp:hud:tick", …)`. HP: `player.Health - 100` в 0..100 (в GTA здоровье игрока 100..200). Armor: `player.Armor` 0..100. `cash` — из `Account.Cash` |
| `FloVMP.Core/Auth/Account.cs` | добавлено поле `Cash` (стартовый баланс `StartingCash = 5000`), персистится вместе с учёткой |
| `GamemodeResource.cs` | `OnTick()` → `_hud.Tick()`; колбэк `OnPlayerAuthed(player, account)` теперь и спавнит, и включает HUD; `OnPlayerDisconnect` → `_hud.OnDisconnect` |
| `AuthSystem.cs` | колбэк изменён на `Action<IPlayer, Account>`; добавлен `AccountOf(player)` |

### Клиент

| Файл | Роль |
|---|---|
| `client/html/hud/index.html` | оверлей: справа-сверху бренд + `$cash` + часы + онлайн; слева-снизу полоски HP/AR. Полупрозрачные панели, `#ff3d8a` акцент. Self-contained |
| `client/index.js` | `openHud()`/`closeHud()` (постоянный WebView, создаётся на `flovmp:auth:hide`), проброс `flovmp:hud:init`/`flovmp:hud:tick` в NUI |

## Не сделано / дальше

- Экономика: `cash` пока статичен (5000), нет заработка/трат.
- Часы — реальное локальное время сервера, не игровое; игровое время в мире
  не синхронизируется.
- HP/armor — формула упрощённая, проверить на живом клиенте.
- Инвентарь — следующий срез.
- Игровое время/погода, чат.
