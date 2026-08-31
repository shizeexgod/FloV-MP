# FloV:MP / Florida V — HANDOFF (актуально на 2026-08-30)

Стартовое чтение для нового чата Claude Code. Полный статус и очередь —
`agent_state.md`. Правила и контекст — `CLAUDE.md`. Не сокращать.

## Правило №1

При любой неясности — задавать вопрос явным списком уточнений. Ничего не
делать молча/по догадке. Отвечать на русском.

## Три продукта (не путать)

| Продукт | Что это | Кто трогает |
|---|---|---|
| **FloV:MP** | движок-мультиплеер: серверный runtime + клиентский runtime + сеть + лаунчер. Игрок его не видит | «проблемы запуска GTA, синхронизации, протокола, лаунчера» |
| **Florida V Launcher** | WPF-.exe, единственная дверь для игрока. Детект GTA, профили Legacy/Enhanced, direct-connect, ставит FloV:MP в AppData | UI/UX, поток игрока |
| **Florida V** | сам RP-сервер: C#-геймод + JS/NUI ресурсы (auth/HUD/чат/инвентарь/логи), позже — фракции/бизнесы/семьи/админка | «серверные системы поверх платформы» |

Границы: `docs/architecture/product-boundaries.md`, `platform-boundaries.md`.

## Как устроен запуск (техпоток)

1. **`FloVMP.Connect`** (CLI, `launcher/src/FloVMP.Connect/`) или Florida V
   Launcher: находит GTA (Epic/Steam/RGL), выбирает профиль, поднимает
   **`LocalCdn` на `127.0.0.1:9988`** — заглушка мёртвого бэкенда alt:V
   (отдаёт манифесты `launcher/client/backup update.json`, файлы клиента с
   диска, фейк `{"access":true}` на авторизацию, `{"release":"16.4.39"}` на
   проверку версии). Порт **9988 жёстко зашит** в alt:V.
2. Пишет `altv.toml` (`gtaPlatform` = `rgl` для Epic-сборки — `egs`/`epic`
   этот клиент отклоняет), опц. патчит `skin.bin` (SkinPatcher → SHA-256 URL
   нашей NUI-оболочки).
3. Запускает `runtime/client/altv.exe` (сток launcher **16.3.7**) с
   `-connecturl altv://connect/<ip:port> -directlaunch -customui
   http://127.0.0.1:9988/ui/index.html` → весь бэкенд-трафик alt:V идёт на
   нашу заглушку.
4. alt:V патчит `GTA5.exe`, инъектит `altv-client.dll` (**16.4.39**),
   коннектится на сервер.
5. **`altv-server.exe`** (Sentry-DSN обнулён) + C#-геймод `FloVMP.Gamemode`
   (грузит `FloVMP.Core`: Auth/Items/Chat/Logging) + JS-ресурс
   `flovmp-client` (NUI: логин/HUD/чат/инвентарь).

## Что РАБОТАЕТ (доказано живым прогоном 29.08)

- alt:V-**сервер стартует автономно**, без бэкенда alt:V.
- Коннектор + `LocalCdn` **обходят мёртвый CDN** (alt:V шлёт запросы на
  `127.0.0.1:9988`, получает 200).
- alt:V-клиент: exe-подмена на **b3307** сняла `suspend -1` и BattlEye
  (b3307 до обязательного BE) → патчер работает → инъекция `altv-client.dll`
  → **`Connect 127.0.0.1 7788`** → **скачал и провалидировал `flovmp-client`
  с нашего сервера**.
- C#-геймод: Auth (PBKDF2 + single-session), HUD (тик 1с), инвентарь
  (стекование/вес/persist/drag&drop NUI), чат (команды `/help /me /online
  /pos`), система логов (`FloVMP.Core/Logging`). **46 тестов**, boot-тест
  зелёный. Версия `0.5.0-hardening`. Устойчивость: Safe-обёртки, автосейв,
  карантин битых сторов, `client:ready`-хендшейк.
- Лаунчер: детект GTA / CDN-манифест (6 тестов) / совместимость Вар.1+2
  (11 тестов) / профили Legacy+Enhanced (`ProfileService`, 26 тестов).
- CI зелёный.

## Единственный БЛОКЕР живого теста

Игрок на **GTA V Legacy b3889** (свежак Epic). alt:V 16.4.39 держит Legacy
**только до b3521**:
```
Rpf version: 3889 differs from alt rpf version: 3521
Pattern ... not found!  ->  ERR_MEM_MULTIALLOC_FREE
```
Краш **до игрового окна**, в version-dependent слое клиента. **Не наш код** —
сервер/геймод/сеть/NUI готовы и ждут за этим слоем.

### Два пути закрыть (детали: `docs/engine/legacy-3889-plan.md`, `native-client-gap.md`)

1. **Даунгрейд-данные b3521** (`update/update.rpf` + `update/update2.rpf`
   ~2.5 ГБ) → `LocalCdn` отдаёт их по полному `backup_update.json`, alt:V
   собирает даунгрейднутую игру в СВОЕЙ приватной папке (инстолл игрока
   цел). **Ближе всего.** DepotDownloader почти достал: `GTA5.exe` b3504
   (Feb 2025, ≤3521) скачан в `C:\FloV-MP\tools\DepotDownloader\depots\
   271591\...`; `update.rpf`/`update2.rpf` — в другом депоте (271594/271595,
   свой manifest за ~Feb 2025 из SteamDB). Steam-аккаунт владельца с GTA V
   есть (`bountifulelect685`, ограничения не мешают depot-скачке).
2. **Свой version-адаптер под b3889** (свои сигнатуры/адреса) — большая
   работа, начата как исследование. `runtime/compat/legacy-3889/`,
   `scripts/inspect-legacy-compat.ps1`.

Enhanced-путь: GTAMP оказался **Legacy-only**, совместимой Enhanced-связки
нет. Legacy — основная цель.

## Что можно делать БЕЗ Windows/GTA (на MacBook)

- **Сервер C#** (`FloVMP.Core` + `FloVMP.Gamemode`) — кроссплатформенный
  `net8.0`, тесты гоняются на Mac: `dotnet test server/FloVMP.slnx`.
- **Веб/игровая админ-панель + система логов** — `docs/admin/admin-and-logging.md`
  (живой дизайн-док, обновлять НА МЕСТЕ). `FloVMP.Core/Logging` уже есть.
- **Перенос геймплея Florida V (FiveM)** — В САМОМ КОНЦЕ, логика-в-логику,
  ничего не терять. Источник: `C:\Arizona V fork project` (на Mac — если
  скопирован). Не начинать, пока платформа не стабильна.
- Доки, планирование, рефакторинг, тесты.

**Нельзя на Mac:** живой тест клиента, DepotDownloader, WPF-лаунчер
(`net8.0-windows`), сам alt:V, скрипты `.ps1`/`.cmd`.

## Согласованный порядок

1. Закрыть блокер b3889 (даунгрейд-данные ИЛИ адаптер) → живой тест ядра
   (2 игрока: auth+HUD+инвентарь+чат, видят друг друга).
2. Довести лаунчер (Florida V Launcher) — дизайн + логика «одна кнопка».
3. Перенести геймплей Florida V.

## Ключевые файлы

- `agent_state.md` — очередь задач, история сессий.
- `docs/engine/live-test-wall.md` — полный разбор прогона + где стена.
- `docs/engine/legacy-3889-plan.md` — план своей совместимости.
- `docs/engine/gtamp-reference.md` — разбор проекта GTAMP (откуда клиент).
- `docs/engine/flovmp-connector.md` — как устроен коннектор.
- `docs/admin/admin-and-logging.md` — дизайн админки/логов.
- `scripts/import-altv-client.ps1` — сборка `runtime/client/` из GTAMP payload.
- GitHub: `https://github.com/shizeexgod/FloV-MP` (приватный).
