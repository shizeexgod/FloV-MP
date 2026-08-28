# Фаза 3 — инвентарь (задача #15)

Обновлено: 2026-08-28
Статус: **логика + проводка готовы, 23 server-теста (11 по инвентарю), boot-тест зелёный. Визуально не проверен.**

Третий срез Фазы 3, паттерн `az_inventory`. Сервер владеет состоянием,
клиент только рисует и шлёт намерения.

## Поток

```
игрок вошёл  → InventorySystem.OnAuthed → грузим из стора; если пусто —
               стартовый набор (phone, water x2, bread x2, bandage x3);
               Emit flovmp:inv:sync (JSON строкой)
клиент: I    → toggle NUI-грид; отрисовка по последнему sync
NUI действия →
  ЛКМ по слоту        → flovmp:inv:use {slot}
  ПКМ по слоту        → flovmp:inv:drop {slot, 1}
  drag слот→слот      → flovmp:inv:move {from, to}
сервер       → валидация, мутация, persist, повторный flovmp:inv:sync
disconnect   → persist в стор
```

## Код

### Чистая логика — `server/src/FloVMP.Core/` (namespace `FloVMP.Core.Items`)

| Файл | Роль |
|---|---|
| `Inventory/ItemDef.cs` | `ItemDef` (id, name, weight, maxStack) + `ItemCatalog` (7 предметов: water/bread/phone/bandage/cash/pistol/ammo9) |
| `Inventory/Inventory.cs` | класс `Inventory`: слоты фикс. размера + лимит веса; `Add` (стекование по maxStack, атомарный откат при нехватке места/веса), `Remove` (по нескольким стекам), `Move` (в пустой / merge с overflow / swap), `Snapshot`/`LoadSnapshot` |
| `Inventory/JsonInventoryStore.cs` | `IInventoryStore` + JSON-файл по `accountId`, потокобезопасно, durable |

### Проводка alt:V — `server/src/FloVMP.Gamemode/Systems/Inventory/InventorySystem.cs`

`ConcurrentDictionary<uint, Inventory>` на вошедших; `Alt.OnClient<int,int>`
для move/drop, `Alt.OnClient<int>` для use; `use` для bandage лечит +25 HP;
`Sync` сериализует `{slotCount, maxWeight, weight, slots[], defs{}}` строкой
JSON и шлёт `flovmp:inv:sync`. Persist на каждое изменение и на disconnect.

`GamemodeResource`: `_inv = new InventorySystem(<dataDir>/inventories.json,
p => _auth.AccountOf(p))`; в `OnPlayerAuthed` — `_inv.OnAuthed(player, account)`.

### Клиент — `client/resources/flovmp-client/`

| Файл | Роль |
|---|---|
| `client/html/inventory/index.html` | грид 6×N, drag&drop между слотами, ЛКМ/ПКМ, шапка вес/макс, строка уведомлений. Тёмная тема + `#ff3d8a`. Self-contained |
| `client/index.js` | `openInventory`/`closeInventory`/`toggleInventory` по `keyup` key `73` (I); кэш `lastInvSync`; мост NUI↔сервер. Открывается только `inGame` (после auth) |

## Тесты — `server/tests/FloVMP.Core.Tests/InventoryTests.cs` (11)

Стекование до maxStack + новый слот; отказ на неизвестном предмете;
атомарный лимит веса; откат при нехватке слотов; `Remove` по нескольким
стекам; отказ `Remove` при недостатке; `Move` в пустой слот; `Move` merge
с overflow; `Move` swap разных; отказ на плохом индексе слота; round-trip
через `JsonInventoryStore`.

## Не сделано / дальше

- Дроп на землю как объект мира (сейчас предмет просто исчезает).
- Разделение стека (split N), перетаскивание количества.
- Использование pistol/ammo (выдача оружия), phone (интерфейс).
- Хранилища/багажники/обмен между игроками.
- БД вместо JSON.
