# Agent State — FloV:MP

Updated: 2026-08-29

## ГДЕ МЫ СЕЙЧАС (живой прогон, ночь 28→29.08)

Гоняем свой коннектор `FloVMP.Connect` (`launcher/src/FloVMP.Connect/`,
`docs/engine/flovmp-connector.md`) — запускает настоящий клиент alt:V
16.4.39 на наш сервер `127.0.0.1:7788`, обход мёртвого CDN через
`LocalCdn` (HttpListener на 127.0.0.1:**9988** — порт жёстко зашит в alt:V).

Пройденные грабли (все закоммичены):
- порт 9988 (не из `-customui` URL);
- манифесты отдаём дословно из GTAMP (`runtime/client/cdn/*.json`) —
  минимальный alt:V не переваривал;
- `/backup/update.json` → `{"files":[]}` (не 404) — иначе alt:V клинит;
- **BattlEye**: GTA V b3889 legacy требует BE; переименование файлов /
  глушение службы — ТУПИК (ломает запуск, `ERR_GEN_INVALID`). Коннектор
  файлы BE НЕ трогает. Решение — **галка BattlEye в Rockstar Launcher
  выключена владельцем** (Путь A);
- **SteamAppId**: коннектор ставил `env SteamAppId=271590` (из GTAMP,
  Steam) → на Epic GTA5.exe вылетал «Не удалось запустить Steam».
  Исправлено (`119c285`): ставим только для `DetectPlatform=="steam"`.

**Следующий шаг владельца:** пересобрать коннектор и запустить (BE выключен,
Steam-env убран). Если GTA5.exe всё равно open-and-close → пробовать
`--no-directlaunch`. Логи: `runtime/client/logs/patcher.log` +
`launcher_*.log`.

Мой косяк за сессию: глушил службу `BEService` → сломал обычный запуск GTA
(`ERR_GEN_INVALID`). Владелец починил (`Set-Service BEService -StartupType
Manual; Start-Service BEService`). В коннекторе убрано.

## Current task

**Ядро в состоянии «готово к живому тесту».** Всё, что можно без GTA-клиента —
сделано:
- Сервер: auth + HUD + инвентарь + чат (C# + NUI), 41 тест, boot-тест зелёный,
  устойчивость (Safe-обёртки, автосейв, single-session, карантин сторов,
  client:ready-хендшейк). Версия геймода `0.5.0-hardening`.
- Лаунчер: детект GTA / CDN-манифест / совместимость (Вар.1/2) / direct-connect.
- Клиент alt:V реконструирован 85/85 (`runtime/client/`).
- CI зелёный.

**Клиент — решён (2026-08-28 вечер).** Форумчанин прислал проект GTAMP
(`docs/engine/gtamp-reference.md`). `runtime/client/` пересобран из его
`sources/payload/` — **настоящий официальный alt:V 16.4.39, 84/85 exact
hash-match** (`scripts/import-altv-client.ps1`). Все 4 открытых вопроса сняты.

**Согласованный порядок (2026-08-28):**
1. **Прогон** — клиент GTAMP на наш сервер (`docs/instructions/quick-connect-test.md`).
   Сервер запущен в фоне, `runtime/gtamp-client/` готов, `.config` правлен
   под Epic. Владелец запускает `connect.exe -connect 127.0.0.1:7788`.
2. **Перенос сервера** — вынести игровой сервис (server + runtime-сборка) в
   отдельную папку/репо; лаунчер отдельно. (Владелец подтвердил сплит.)
3. **Лаунчер** — дизайн + логика (свой коннектор по паттерну GTAMP, #19).
4. **Перенос FloridaV (FiveM) логика-в-логику** — В САМОМ КОНЦЕ, после 1-3.
   Ничего не сломать/не пропустить/не потерять. НЕ начинать раньше.

Дальнейшая доводка ядра (раунд 3+) — по результатам прогона.

## Queue
| # | Задача | Статус |
|---|------|--------|
| 0 | `altv-server.exe` стартует автономно | ✅ done |
| 1 | Формат `server.toml` / `resource.toml` | ✅ done — `docs/engine/server-config.md`, проверено стартом |
| 2 | Минимальный C#-ресурс | ✅ done — `flovmp-core` грузится |
| 3 | Минимальный JS клиентский ресурс | ✅ done — `flovmp-client` грузится |
| 4 | Тест живым клиентом GTA V | **разблокирован** — клиент собран 85/85 (`runtime/client/`). Нужен владелец за ПК с GTA V. Гайд: `docs/instructions/live-test-guide.md` |
| 5 | Лаунчер — скелет WPF | ✅ done — детект GTA V, статус сервера, сборка direct-connect, UI |
| 6 | Отключить Sentry-телеметрию сервера | ✅ done — DSN обнуляется в рабочей копии `altv-server.exe` при сборке, запросов на `sentry-alt.com` нет (`docs/engine/telemetry-sentry.md`) |
| 7 | Полный клиент alt:V 16.4.x | ✅✅ **настоящий официальный 16.4.39** взят из GTAMP-пейлоада (форумчанин), `runtime/client/` пересобран `scripts/import-altv-client.ps1`, 84/85 exact hash-match (только `altv-webengine.exe` не-сток). Реконструкция CEF+Majestic больше не нужна. `docs/engine/gtamp-reference.md` |
| 8 | Лаунчер: CDN-манифест + сверка хэшей + загрузчик (Вариант 1) | ✅ done — `FloVMP.Launcher.Core/Services/Cdn/*`, 6/6 тестов, подключено в UI, `scripts/make-manifest.ps1`, `docs/launcher/launcher-cdn.md`. Реального CDN нет — тест на локальной раздаче |
| 9 | Лаунчер: совместимость с патчами GTA V (Вар.1 offset-CDN / Вар.2 exe-swap) | ✅ каркас — `Core/Services/Compat/*`, 11 тестов, watchdog + маркер + startup-восстановление. Нет своего хука и реального compat.json. `docs/launcher/launcher-compat.md` |
| 10 | Реконструкция клиента alt:V из сторонних источников | ✅ done — `scripts/fetch-cef.ps1` (CEF 131.0.6778.205), fill из Majestic, 85/85, `runtime/client/manifest.json` (318 МБ). `docs/instructions/live-test-guide.md` |
| 11 | Репо-гигиена: `.gitattributes`, `README.md`, CI (GitHub Actions build+test) | todo |
| 12 | Фаза 3: каркас авторизации (C# сервер + NUI клиент) | ✅ done — `FloVMP.Core/Auth/*` (PBKDF2, стор, троттлинг), `AuthSystem` (спавн только после входа), NUI `html/auth/`, 12 тестов. Визуальный поток не проверен. `docs/gameplay/phase3-auth.md` |
| 13 | CI — GitHub Actions | ✅ зелёный (#11 run success 1m26s). Server-тесты добавлены в CI (#12). |
| 14 | Фаза 3: HUD (C# события + NUI) | ✅ done — `HudSystem` (тик 1с через `OnTick`), NUI `html/hud/`, `Account.Cash`. Визуально не проверен. `docs/gameplay/phase3-hud.md` |
| 15 | Фаза 3: инвентарь (C# + NUI) | ✅ done — `FloVMP.Core.Items` (Inventory/ItemCatalog/store), `InventorySystem`, NUI грид с drag&drop (клавиша I), 11 тестов. Визуально не проверен. `docs/gameplay/phase3-inventory.md` |
| 16 | Фаза 3: чат (C# + NUI) | ✅ done — `ChatSanitizer` + `ChatSystem` (rate-limit, команды /help /me /online /pos), NUI `html/chat/` (клавиша T), 14 тестов. `docs/gameplay/phase3-chat.md` |
| 17 | **Доведение MP-ядра до идеала** — устойчивость, автосейв, обработка ошибок, ревью систем. НЕ переносить геймплей Florida V пока не готово | in progress — раунды 1-2 (`docs/engine/core-hardening.md`): Safe-обёртки, фикс порядка disconnect-обработчиков, один аккаунт = один сеанс, автосейв + флаш, карантин битых сторов, хендшейк client:ready (иначе чёрный экран), HUD шлёт только дельты, чистка троттла. 41 тест |
| 18 | Живой тест ядра (2 игрока: auth+HUD+инвентарь+чат, видят друг друга, двигаются) — нужен владелец + GTA V | todo — клиент есть (`runtime/client/`), сервер есть |
| 19 | Свой коннектор клиента (`FloVMP.Connect`) | ✅ v1 — `launcher/src/FloVMP.Connect/`: `LocalCdn` (HttpListener-заглушка бэкенда alt:V), `AltvToml`, запуск `altv.exe -directlaunch -customui`. Без подмены GTA5.exe (играем на legacy игрока). Роуты проверены headless. Живой запуск — за владельцем. `docs/engine/flovmp-connector.md` |
| 20 | Дополнить `config/server.toml` секциями из GTAMP (`[threads]`, лимиты событий, `allowUnknownRPCEvents=false`, `[maxStreaming]`) | todo, мелкая |

## Done (сессия 2026-08-28, часть 13 — прогон + свой коннектор)
- **Прогон GTAMP-клиентом** — не пошёл: их клиент собран под GTA V Enhanced
  (подменяет `GTA5.exe` кэшированным 58МБ), у владельца legacy 47МБ →
  Enhanced-exe на legacy-данных падает. Игра владельца НЕ тронута.
- Разобран весь стек GTAMP (`connect.cpp`/`proxy.cpp`/`config.cpp`/
  `multiplayer.cpp`/`manifest.h`): обход мёртвого CDN = флаг `-customui
  http://127.0.0.1:PORT` (стоковый `altv.exe`, не патч) → alt:V шлёт туда
  весь бэкенд-трафик. `altv_patched.exe` — их IP, не берём.
- **`FloVMP.Connect`** (`launcher/src/FloVMP.Connect/`, console net8.0-windows):
  `LocalCdn` (HttpListener: манифест из реальных файлов, `/backup/*`→404 =
  без подмены GTA5.exe), `AltvToml`, `Program` (resolve client/GTA →
  старт CDN → altv.toml → `altv.exe -directlaunch -customui`). Роуты
  проверены headless (200/404 как надо, loopback без админа).
- `runtime/client/cache/skin.bin` добавлен (из GTAMP altv-resources),
  `import-altv-client.ps1` копирует его. `docs/engine/flovmp-connector.md`.

## Done (сессия 2026-08-28, часть 12 — настоящий клиент из GTAMP)
- Форумчанин прислал проект **GTAMP** (независимый MP на alt:V без бэкенда —
  ровно наша задача). Разобран: `docs/engine/gtamp-reference.md`.
- `sources/payload/` = бит-в-бит официальный клиент alt:V **16.4.39**
  (sdk `c150769`), 86/86 hash-match. `altv-client.dll` = `287a4443…` —
  протокол-совместим с нашим сервером.
- `scripts/import-altv-client.ps1` — раскладывает полный payload в
  `runtime/client/` + сверка хэшей. Прогнан: **84/85 exact**, не-сток только
  `cef/altv-webengine.exe` (GTAMP-модификация). Манифест перегенерён.
- Удалены `runtime/client-fill`, `runtime/client-reconstructed-bak`.
- Из GTAMP вынесены в задачи: #19 (свой коннектор вместо `-noupdate`),
  #20 (секции `server.toml`).
- Открытые вопросы реконструкции (freetype/icudtl_v8/legacy.dll/CEF) — сняты.

## Done (сессия 2026-08-28, часть 11 — устойчивость ядра, задача #17 раунд 1)
- `Safe.Run` — изоляция исключений во всех обработчиках событий alt:V.
- **Баг-фикс**: порядок обработчиков `OnPlayerDisconnect` — Auth чистил
  `_authed` первым, из-за чего инвентарь не сохранялся при выходе.
  Inv теперь хранит `(Inventory, accountId)`, Chat — снимок ника; не зависят
  от Auth на disconnect.
- Один аккаунт = один сеанс (`AuthSystem._activeAccounts`), вход отклоняется
  для уже играющего аккаунта.
- Автосейв инвентарей раз в 60с (`OnTick`) + флаш на `OnStop`.
- `StoreFiles.QuarantineCorrupt` — битый JSON-стор уезжает в
  `*.corrupt-<ticks>`, не затирается.
- `PlayerLifecycle._spawnCounter` → `Interlocked.Increment`.
- +3 теста (`StoreRobustnessTests`), итого 40. boot-тест зелёный.
- версия геймода → `0.5.0-hardening`. `docs/engine/core-hardening.md`.

## Done (сессия 2026-08-28, часть 10 — Фаза 3: чат, задача #16)
- `FloVMP.Core/Chat/ChatSanitizer` — Clean (trim/control/схлопывание/лимит
  256), IsCommand (одиночный `/`), ParseCommand.
- `Systems/Chat/ChatSystem` — `Alt.OnClient<string>`, rate-limit 4/3с,
  `//` эскейп, команды `/help /me /online /pos`, `Broadcast`/`SendSystem`,
  сообщения о входе/выходе. Рассылка только вошедшим.
- `GamemodeResource`: `_chat` подключён, в `OnPlayerAuthed`.
- Клиент: `client/html/chat/index.html` (лог 40 строк, затухание, экранир.
  HTML), `index.js` — `openChat`/`startTyping` по клавише T (`keyup` 84),
  `flovmp:chat:done` возвращает фокус.
- 14 xUnit (итого 37 server-тестов). boot-тест зелёный.
- версия геймода → `0.4.0-phase3-chat`. `docs/gameplay/phase3-chat.md`.

## Done (сессия 2026-08-28, часть 9 — Фаза 3: инвентарь, задача #15)
- `FloVMP.Core` namespace `FloVMP.Core.Items`: `ItemDef`/`ItemCatalog`
  (7 предметов), класс `Inventory` (слоты + лимит веса, Add со стекованием
  и атомарным откатом, Remove по стекам, Move пустой/merge+overflow/swap),
  `JsonInventoryStore` (persist по accountId).
- `Systems/Inventory/InventorySystem` — `Alt.OnClient` move/drop/use,
  `flovmp:inv:sync` строкой JSON, persist на изменение и disconnect,
  стартовый набор. bandage use → +25 HP.
- `GamemodeResource`: `_inv` подключён, стор `<dataDir>/inventories.json`.
- Клиент: `client/html/inventory/index.html` (грид 6×N, drag&drop, ЛКМ/ПКМ),
  `index.js` toggle по клавише I (`keyup` 73), кэш последнего sync.
- 11 xUnit (итого 23 server-теста). boot-тест зелёный.
- версия геймода → `0.3.0-phase3-inv`. `docs/gameplay/phase3-inventory.md`.

## Done (сессия 2026-08-28, часть 8 — Фаза 3: HUD, задача #14)
- `Systems/Hud/HudSystem.cs` — раз в 1с (аккумулятор в `OnTick`) шлёт
  каждому вошедшему `flovmp:hud:tick {hp, armor, cash, online, hour, minute}`.
- `Account.Cash` (стартовый 5000), персистится.
- `GamemodeResource.OnTick` → `_hud.Tick()`; `OnPlayerAuthed` теперь спавн + HUD.
- `AuthSystem` колбэк → `Action<IPlayer, Account>`, добавлен `AccountOf`.
- Клиент: `client/html/hud/index.html` (полоски HP/AR, $cash, часы, онлайн,
  `#ff3d8a`), постоянный WebView в `index.js`.
- boot-тест зелёный. `docs/gameplay/phase3-hud.md`.

## Done (сессия 2026-08-28, часть 7 — Фаза 3: авторизация, задача #12)
- **`server/src/FloVMP.Core/`** — новый проект чистой логики (net8.0, без
  AltV.Net). `Auth/`: `PasswordHasher` (PBKDF2-SHA256 120k), `Account`
  (+правила валидации), `IAccountStore`/`JsonAccountStore` (потокобезопасно,
  durable), `AuthService` (Register/Login + троттлинг подбора).
- **`FloVMP.Gamemode`**: `Systems/Auth/AuthSystem` — connect больше не
  спавнит, шлёт `auth:show`; `Alt.OnClient` login/register; при успехе
  `_authed[player.Id]` + колбэк спавна. `PlayerLifecycle.SpawnAuthed`
  вынесен из connect. `GamemodeResource` связывает. Стор →
  `<serverRoot>\flovmp-data\accounts.json`.
- **Клиент NUI**: `client/html/auth/index.html` (вход/регистрация, тёмная
  тема + `#ff3d8a`), `client/index.js` — WebView + камера + мост NUI↔сервер.
  `resource.toml` client-files → `client/**/*`.
- **`server/tests/FloVMP.Core.Tests/`** — 12 xUnit (PBKDF2 round-trip/соль,
  Register/Login, дубль case-insensitive, rate-limit + разблок, персист).
- boot-тест зелёный; версия геймода → `0.2.0-phase3-auth`.
- `docs/gameplay/phase3-auth.md`.

## Done (сессия 2026-08-28, часть 6 — реконструкция клиента, задача #10)
- **Определена версия CEF клиента** — Chromium 131.0.6778.205 (по `chrome_elf.dll`).
- **`scripts/fetch-cef.ps1`** — качает официальный CEF
  `131.3.5+chromium-131.0.6778.205` windows64 minimal (182 МБ) с
  `cef-builds.spotifycdn.com`, раскладывает Release/ + Resources/ в layout alt:V.
- **Находка**: на машине стоит **Majestic RP** (форк alt:V),
  `%APPDATA%\majestic-launcher\Multiplayer\libs\` — оттуда взяты 6
  недостающих: `legacy.dll` (99448 Б, точное совпадение с манифестом alt:V!),
  OpenSSL 3 (`libcrypto`/`libssl-3-x64`), `bassmix.dll`, `discord_game_sdk.dll`
  (все точное совпадение размера), `freetype.dll` (другой билд, под вопросом).
  `icudtl_v8.dat` — копия `icudtl.dat` (догадка).
- **`runtime/client/` собран 85/85** + `manifest.json` (86 записей, 318 МБ).
- Живой запуск НЕ проверен — 4 открытых вопроса в `docs/archive/client-recovery-plan.md`.
- `docs/instructions/live-test-guide.md` — пошаговый гайд теста для владельца.
- Задача #4 (живой тест) — разблокирована.

## Done (сессия 2026-08-28, часть 5 — совместимость с патчами GTA V, задача #9)
- `Core/Services/Compat/`: `GameVersion` (детект версии GTA5.exe),
  `CompatManifest` (модели), `CompatService` (вердикт Ok/Untested/
  NeedsFallback/Unknown/NoManifest), `PendingRestore` (durable-маркер),
  `GameExeManager` (Вариант 2: атомарная подмена/откат GTA5.exe со всеми
  мерами безопасности из HANDOFF), `WatchdogSpawner`.
- Лаунчер: режим `--watchdog <pid> <marker>` в `App.OnStartup` (без UI,
  ждёт выход игры → откат); startup-проверка маркера перед показом окна;
  `CompatGateAsync` в Play (блокирует запуск на Broken-версии).
- 11 тестов (CompatService 5 + GameExeManager 6) — все зелёные. Итого 17.
- UI: поле «Манифест совместимости». Настройка `CompatManifestUrl`.
- `docs/launcher/launcher-compat.md`.

## Done (сессия 2026-08-28, часть 4 — CDN-манифест лаунчера, задача #8)
- **Рефактор лаунчера на 3 проекта**: `FloVMP.Launcher.Core` (net8.0-windows,
  без WPF — вся логика, тестируемо), `FloVMP.Launcher` (WPF-UI, ссылается на
  Core), `FloVMP.Launcher.Tests` (xUnit).
- **Система CDN-манифеста** `Core/Services/Cdn/`: `Manifest`/`ManifestEntry`,
  `ManifestClient` (http/локальный/`file://`), `ContentHasher` (SHA-256),
  `SyncPlanner` (Ok/Missing/WrongSize/WrongHash + лишние), `FileDownloader`
  (параллельно, HTTP Range докачка, ретраи+backoff, `.part`→атомарный Move,
  проверка хэша после), `SyncService` (Check/Sync + прогресс/лог).
- **6 xUnit-тестов** на локальной раздаче — все зелёные (актуальность,
  классификация, докачка+починка, прогресс до 1.0, ретрай→провал с чистым
  `.part`, манифест без baseUrl).
- **`scripts/make-manifest.ps1`** — генератор манифеста из папки (SHA-256).
- **UI**: секция «Обновление ядра по манифесту» — поле URL, кнопка
  Проверить/Обновить, прогресс-бар, статус (файлы/МБ/скорость). Настройка
  `CoreManifestUrl`. Управляемая папка по умолчанию `%LOCALAPPDATA%\FloVMP\client`.
- `docs/launcher/launcher-cdn.md`.

## Done (сессия 2026-08-27, часть 3 — Sentry + сеть)
- **Sentry-телеметрия отключена.** `altv-server.exe` содержит sentry-native
  0.6.5 с хардкод-DSN `...@sentry-alt.com/4`; хост живой (Cloudflare, ingest
  отвечает 400), envelope уходит уже при старте сервера. `SENTRY_DSN` env не
  помогает (alt:V перетирает через `sentry_options_set_dsn`). Решение:
  `assemble-runtime.ps1` обнуляет строку DSN в рабочей копии exe (бэкап цел).
  Проверено `SENTRY_DEBUG=1` — `sentry_init failed`, запросов нет. Опция
  `-KeepSentry` отключает патч. `docs/engine/telemetry-sentry.md`.
- **Сеть проверена**: CDN alt:V (`cdn.alt-mp.com`, `cdn.altv.mp`,
  `cdn.alt-mp.dev`) — DNS мёртв. `altv.mp` — origin 502 (Cloudflare жив).
  `docs.altv.mp` — 200 (только SPA-оболочка, статьи по угадай-URL 404).
  Полного клиента alt:V на дисках C:/D:/E: нет (искал по всем `altv.exe`).

## Done (сессия 2026-08-27, часть 2 — лаунчер + разведка клиента)
- **Лаунчер** `launcher/` (WPF, net8.0-windows, `FloVMP.Launcher.slnx`):
  - `Services/GtaLocator.cs` — детект GTA V по реестру (Rockstar/Epic/Steam);
    на этой машине находит Epic-инсталл по `InstallFolderEpic`.
  - `Services/AltvClientCore.cs` — валидация папки ядра клиента (16
    обязательных файлов), сборка `altv://connect/...` + аргументов
    `-connecturl -noupdate -branch -skipprocesscheck`, запуск `altv.exe`,
    опц. правка глобального `altv.toml` (`.bak`).
  - `Services/ServerStatus.cs` — HTTP-пинг `:7788`.
  - `Services/SettingsStore.cs` + `Models/LauncherSettings.cs` — JSON в
    `%LOCALAPPDATA%\FloVMP\launcher.settings.json`.
  - UI: тёмная тема + акцент `#FF3D8A`, MVVM-lite. Сборка + smoke-run ok.
- **Разведка клиента alt:V** (`docs/engine/client-direct-connect.md`):
  - GTA V на машине = **Epic legacy**, `C:\Program Files\9d2d0eb64d5c44529cece33fe2a46482`.
  - `%LOCALAPPDATA%\altv\altv.toml` уже настроен (branch release, egs, gtapath).
  - `altv.exe` = launcher-UI 14.4; флаги: `-connecturl`, `-noupdate`,
    `-branch`, `-directlaunch`, `-skipprocesscheck`, `-gtaexe`, `-offline`.
  - **Бэкап клиента неполный: 17/85 файлов.** Нет `legacy.dll`,
    `resources.pak`, `icudtl.dat`, V8-снапшотов, crypto-DLL, всех
    `cef/locales/*.pak`. `scripts/assemble-client-core.ps1` собирает что есть
    и пишет `runtime/client/MISSING.txt`.
- Документация: `docs/launcher/launcher.md`, `docs/engine/client-direct-connect.md`.

## Blockers
- **Полный клиент alt:V 16.4.39 (release, x64_win32).** Без него `altv.exe`
  не запустит игру → нет живого теста (#4), кнопка ИГРАТЬ в лаунчере
  неактивна. Нужен источник: рабочая установка alt:V на другой машине/диске,
  другой бэкап или зеркало CDN. Затем `assemble-client-core.ps1 -FillFrom <...>`.

## Notes / принятые решения
- Ветка alt:V — **release 16.4.39**.
- `AltV.Net` 16.4.21 (NuGet), при проблемах SDK — `16.4.28-rc.2`.
- Скрипты `.ps1` и `Alt.Log` в C# — **только ASCII** (PS 5.1 CP1251 /
  консоль alt:V). Русский — в `docs/*.md`, комментариях, WPF-UI.
- `runtime/` (сервер и клиент) — в `.gitignore`.
- Решения `.slnx` (не `.sln`) — дефолт `dotnet` 10.
- Правка глобального `altv.toml` — только по явной галке, всегда `.bak`.
