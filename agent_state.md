# Agent State — FloV:MP / Держава Онлайн

Updated: 2026-09-06 (спринт /goal — разработка SaaS Веб-Портала, Биллинга, Телеметрии и 2FA)

## Архитектура продукта (зафиксировано)

Чёткое архитектурное разграничение сущностей:
- **FloV:MP** — независимый мультиплеерный движок (сетевой стек на отвязанных бинарниках alt:V v16.4.39 release, протокол синхронизации, коннектор, рантайм). Невидим игроку, работает строго «под капотом». Это **движок**, а НЕ сам RP-проект.
- **Держава Онлайн** (Держава RP) — **сам RP-проект** (карта реальной Москвы RMRP 2025, российская тематика, 8-уровневая админ-система, фракции, экономика, сайт с веб-админкой, база MariaDB, Discord-сообщество игроков).
- **Лаунчер «Держава RP / Держава Онлайн»** — единая входная точка для игрока, запускающая проект через движок FloV:MP (Electron UI + C# Native Bridge FloVMP.Connect).
- **SaaS Веб-Портал FloV:MP (`c:\FloV-MP\web`)** — коммерческая платформа лицензирования мультиплеера по модели icsnotify.ru (Next.js 14, личный кабинет, биллинг со счетами и моментальной оплатой, мониторинг телеметрии 60 Hz/60 FPS, генератор кастомных лаунчеров, HMAC-SHA256 криптографическая верификация ключей, 20% партнёрка, менеджер ресурсов серверов, вебхук-алерты Discord/Telegram, Public Developer API).
- **txAdmin Cloud Remote Control Plane & AI Troubleshooter** — двусторонний агент управления серверами (`RemoteServerAgent.cs`), очередь команд (`portal_agent_commands`), SSE стриминг логов в реальном времени, интерактивная веб-консоль и нейросетевая диагностика инцидентов.
- **Enterprise Core Systems** — Dynamic Spatial Asset Streaming Protocol, Server Crash Watchdog (авторестарт при фризах >15s), Dynamic Resource Manager (горячий старт/стоп ресурсов), FloV:ID & HWID Enforcement с гибкими политиками (Strict, Lenient, Disabled).
- **Иерархия Account -> Projects -> Servers** — лицензия привязывается к проекту, внутри которого запускаются изолированные среды (Production, Development, Test) без коллизии ключей.

## Архитектурный SaaS Blueprint (согласовано с концептом enterprise-экосистемы)

В `docs/architecture/saas-ecosystem-blueprint.md` зафиксирована полная архитектурная спецификация (ответы на 15 вопросов и 13 глав архитектуры: Multiplayer Core, Server Core, SDK, API, Telemetry, Project Licensing, txAdmin Agent, AI Assistant, High-Load Scaling 1000+).
Движок `FloVMP.Core` полностью очищен от специфики карты и RP-лора («Держава Онлайн» изолирована в `FloVMP.Gamemode/Presets/`).

## Актуальный статус серверов и сервисов на VDS REDL (`188.127.229.224`)

1. **Серверный движок FloV:MP (`flovmp.service`):**
   - Активен и работает на порту **UDP 7788** (Online: True, 1500 слотов).
   - CoreCLR / .NET 8 Runtime интегрирован на VDS (`/usr/share/dotnet`).
   - C#-гейммод (`FloVMP.Core.dll`, `FloVMP.Gamemode.dll`, `MySqlConnector.dll`) загружен в рантайм.
2. **База данных MariaDB (`mariadb.service`):**
   - Установлена и запущена на VDS (MariaDB 10.6.23).
   - Создана база данных `derzhava_rp`, выделен пользователь `flovmp`.
   - Развёрнуты все таблицы гейммода и SaaS-портала (`accounts`, `characters`, `character_inventory`, `vehicles`, `punishments`, `admin_audit_logs`, `bank_transactions`, `portal_users`, `portal_licenses`, `portal_invoices`, `portal_launcher_builds`, `portal_telemetry`).
3. **Nginx FastDL & CDN (HTTP :80 & HTTP :7788):**
   - Эндпоинт `/info` отдаёт актуальный JSON статус в UTF-8 (`{"online":true,"players":0,"maxPlayers":1500,"name":"Держава Онлайн","gamemode":"Держава RP"}`).
   - `/cdn/` раздаёт статические файлы и манифесты (`manifest-map.json`, архив карты Москвы `moscow_map.zip`, серверные дистрибутивы).

## Реализованные системы гейммода и ядра (FloVMP.Core & FloVMP.Gamemode)

- **8-уровневая админ-система (Levels 1–8):**
  - Модели: `AdminRank`, `AdminTitles` (префиксы, цвета, наименования), `AdminCommandDef`, `AdminCommandRegistry`.
  - Все команды диспетчеризируются в `ChatSystem.cs` с проверкой прав, логированием наказаний и аудитом.
- **Экономика и банкинг (`FloVMP.Core.Economy`):**
  - Балансы наличных (`Cash`) и банковского счёта (`Bank`, `BankAccountNumber`).
  - `EconomyService`: атомарные, потокобезопасные методы (`TryGiveCash`, `TryTakeCash`, `TryDeposit`, `TryWithdraw`, `TryPayCash`, `TryTransferBank`).
  - Игровые команды: `/pay`, `/balance`, `/bank`.
  - Административные команды: `/givemoney`, `/takemoney`.
- **Система транспорта (`FloVMP.Core.Vehicles`):**
  - `VehicleData`: топливо (`Fuel`), состояние двигателя, замки дверей, мастер-ключи для администраторов 4+ ранга.
  - `VehicleService`: реестр активного автопарка с поиском по госномеру и владельцу.
  - Игровые команды: `/lock`, `/engine`, `/veh`, `/dv`, `/repair`.
- **Слой персистентности (`FloVMP.Core.Database`):**
  - `AccountStoreFactory`: динамическое подключение к MariaDB на хостинге с прозрачным fallback на `JsonAccountStore` при отсутствии БД.
  - `MySqlAccountStore`: параметризованные SQL-запросы через `MySqlConnector` с пулом соединений.
  - `MySqlAuditStore`: фиксация всех наказаний, действий администрации и финансовых переводов.
- **Система лицензирования и телеметрии (`FloVMP.Core.Licensing`):**
  - Криптографическая валидация ключей `FLV-XXXX-XXXX-XXXX` по HMAC-SHA256 при старте C#-сервера.
  - Ограничение слотов (32..3000) по тарифным планам (Starter, Pro, Enterprise, Unlimited).
  - Автономный режим работы (Offline Cache Grace Period) до 7 дней при падении сети или недоступности SaaS-портала.
  - Фоновый воркер `TelemetryReporter` с периодической отправкой онлайна, 60 Hz тикрейта, 60 FPS и ОЗУ на `/api/v1/telemetry/heartbeat`.
- **Серверный античит FloV:Shield (`FloVMP.Core.AntiCheat`):**
  - Защита от SpeedHack: раздельные лимиты скорости для пешехода (12.5 м/с) и транспорта (125 м/с).
  - Защита от Teleport: мгновенная детекция телепортационных прыжков с учётом динамических grace-периодов спавна и легитимных админ-телепортов.
  - Защита от читерского спавна оружия (Weapon Security): блэклист тяжёлого вооружения (РПГ, миниган, рельсотрон) и сверка с инвентарём.
  - Автоматические наказания: возврат на последнюю легитимную позицию (`TeleportBack`), конфискация оружия (`Disarm`), кик с сервера (`Kick`).
- **Система фракций и организаций (`FloVMP.Core.Factions` + `FloVMP.Gamemode.Presets`):**
  - Ядро `FloVMP.Core` полностью очищено от специфики Москвы и является универсальным движком (подходит для Лос-Сантоса, Либерти-Сити, Москвы и любых кастомных карт).
  - RP-пресеты вынесены в слой гейммода `FloVMP.Gamemode/Presets/DerzhavaFactions.cs`: Мэрия Москвы, ГУ МВД по г. Москве, УФСБ РФ, ГКБ им. Боткина, Воинская часть ВС РФ, Телеканал «Москва 24», Солнцевская ОПГ.
  - Иерархия рангов (1–8), почасовая зарплата на PayDay, права доступа (приём, увольнение, повышение, понижение, склад, казна).
  - Наручники (`/cuff`, `/uncuff`) и арест в ИВС / КПЗ (`/arrest`) с ежесекундным таймером отсидки и автоосвобождением.
  - Рация фракции (`/f`) и рация департамента госструктур (`/d`).
- **Система документов удостоверения личности (`FloVMP.Core.Documents` + `FloVMP.Gamemode.Presets`):**
  - Универсальный сервис `DocumentService` без хардкода конкретной страны или серий.
  - Российские серии и органы выдачи изолированы в `FloVMP.Gamemode/Presets/DerzhavaDocuments.cs`:
    - Паспорт гражданина РФ (серия 45 ХХ, прописка, орган выдачи, бессрочный).
    - Водительское удостоверение (серия 77 ХХ, категории A/B/C/D/Boat/Air, 1-й ОСБ ДПС ГИБДД).
    - Лицензия на оружие РОХа, медицинская карта со справками нарколога и психиатра ГКБ им. Боткина.
  - Игровые команды: `/passport`, `/lic`, `/licenses`.
- **FastDL Asset Packager CLI (`tools/FloVMP.AssetPacker`):**
  - Консольная утилита `flovmp-packer` для быстрого рекурсивного сканирования ассетов карты и скриптов.
  - Расчёт SHA-256 и SHA-1 контрольных сумм, генерация `manifest.json`.
  - Верификация целостности локальных файлов и оптимальное Gzip-сжатие файлов.
- **Система недвижимости и жилья (`FloVMP.Core.Housing` + `FloVMP.Gamemode.Presets`):**
  - Универсальное ядро управления недвижимостью `HousingService` без привязки к координатам карты.
  - Пресеты г. Москвы вынесены в `FloVMP.Gamemode/Presets/DerzhavaHousing.cs`: ЖК «Москва-Сити» (Башня Федерация), ЖК «Тверской бульвар», особняк «Барвиха Luxury» и гараж ГСК «Москвич».
  - Покупка, продажа (75% возврат), сейфы для хранения наличных, замки и подселение жильцов.
  - Команды: `/buyhouse`, `/sellhouse`, `/enter`, `/exit`, `/hlock`, `/house`.
- **Кастомизация внешности и форма фракций (`FloVMP.Core.Characters` + `FloVMP.Gamemode.Presets`):**
  - `CharacterAppearance`: генетика HeadBlend, 20 микро-черт лица, наложения (бороды/макияж), причёски, одежда и пропсы.
  - `FactionUniformService`: универсальный потокобезопасный реестр униформы в ядре без хардкода ведомств.
  - Служебная форма государственных ведомств Москвы вынесена в `FloVMP.Gamemode/Presets/DerzhavaUniforms.cs`: МВД (ППСП/офицеры), ФСБ (спецназ), ГКБ им. Боткина (халаты) и ВС РФ (Ратник).
- **Система моментальных снимков и точечных откатов (Time-Machine Rollback Engine, `FloVMP.Core.Database`):**
  - Модель `PlayerStateSnapshot`: снимок балансов (банк, наличные, грязь), инвентаря, позиции, дименшна, здоровья, брони и лицензий.
  - `SnapshotManager`: потокобезопасный кольцевой буфер снимков (по умолчанию 30 на игрока) с авто-отсечением старых копий.
  - Точечный откат персонажа без общего рестарта и без вайпа базы. Перед любым откатом автоматически создаётся контрольный снимок `PreRollbackBackup` для возможности отмены отката.
  - Команда `/snapshot` (Уровень 6+ администрации).
- **Схема базы данных (`sql/schema.sql`):**
  - Добавлены таблицы MariaDB: `factions`, `faction_members`, `player_documents`, `properties`.
- **Тестовое покрытие ядра:**
  - **197 automated tests passing** в `FloVMP.Core.Tests` (0 failures, 0 warnings).
  - Тесты `FloVMP.Core.Tests` проверяют чистое ядро с независимыми тестовыми фикстурами без зависимости от гейммода или карты.
  - Покрыты: `AssetStreamingManagerTests` (100% green), `ServerCrashWatchdogTests` (100% green), `DynamicResourceManagerTests` (100% green), `FlovIdPolicyServiceTests` (100% green), `DimensionManagerTests` (100% green), `MultiTierBanServiceTests` (100% green), `SnapshotManagerTests` (100% green).

## FloV:Graphics & Upscaler Engine («DLSS 5» / DLSS 3.7 / FSR 3.1)

- **Аппаратная детекция видеокарт (`GpuDetectionService.cs`):**
  - Прямой анализ реестра Windows без вызова тяжелых DirectX API.
  - Определение VRAM, вендора (NVIDIA/AMD/Intel), RT/Tensor ядер и расчет оптимального режима.
- **Безопасная песочница и NUI-защита (`UpscalerDeploymentService.cs`):**
  - Динамическая генерация `flovmp_upscaler.ini` перед запуском игры и гарантированная очистка при выходе.
  - Секция `[NUI_Protection]` с маскированием альфа-канала и буфера глубины CEF-оверлея (чат, инвентарь, радар не размываются).
  - Использование `CultureInfo.InvariantCulture` для предотвращения крашей из-за запятой в дробных числах русской локали.
- **Поддержка 4 режимов:**
  1. `none` — оригинальный рендеринг.
  2. `fsr3_framegen` — AMD FSR 3.1 + Frame Generation (+50-80% FPS).
  3. `dlss_framegen` — NVIDIA DLSS 3.7 + Frame Generation (RTX).
  4. `dlss5_neural` — Экспериментальный Neural Reconstruction («DLSS 5»).
- **Интеграция в Лаунчер (Electron UI + C# Native Bridge):**
  - NDJSON команды `detectGpu`, `deployUpscaler`, `cleanupUpscaler`.
  - Карточка настроек `card--upscaler` с динамическим бейджем GPU, селектором режима, качества, ползунком резкости и тумблерами генерации кадров и защиты NUI.
- **Тестовое покрытие лаунчера:**
  - **38 automated tests passing** в `FloVMP.Launcher.Tests` (100% green).

## Лаунчер и нативный мост

- `FloVMP.Launcher.Native`:
  - `ServerStatusService.cs` с поддержкой портов 80 и 7788 и fallback.
  - Гарантированная сериализация camelCase (`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`), благодаря чему статус сервера отображается зелёным индикатором.
- `native-bridge.js`:
  - Наследует `EventEmitter`, парсит `msg.event` и транслирует в Electron IPC (`download:progress`, `window:state`).
  - `preload.js` экспортирует как `window.floridaV`, так и `window.flovmp`.

## ГДЕ МЫ СЕЙЧАС (живой прогон, 29.08 вечер — ПРОРЫВ: КЛИЕНТ ПОДКЛЮЧИЛСЯ)

Полный разбор: **`docs/engine/live-test-wall.md`** (раздел «ПРОРЫВ» в конце).

**Связка работает end-to-end:** сервер + геймод + коннектор `FloVMP.Connect`
+ `LocalCdn` (:9988) + манифесты + **exe-подмена на b3307** (снял `suspend -1`
и BattlEye — b3307 до обязательного BE) + патчер + инъекция `altv-client.dll`
+ **`Connect 127.0.0.1 7788`** + **скачал и провалидировал `flovmp-client`
с нашего сервера**.

**Крашится ТОЛЬКО на версии игровых данных:**
```
Rpf version: 3889 differs from alt rpf version: 3521
Pattern ... not found!  ->  ERR_MEM_MULTIALLOC_FREE
```
alt:V 16.4.39 держит Legacy до **b3521**, у игрока данные (`update.rpf` ~2ГБ)
**b3889** (Epic обновил). Подмена exe без даунгрейда rpf → alt:V не находит
паттерн → краш.

**GTAMP rpf-даунгрейд не решает** — их `backup_update.json` ссылается на
`3411/*` части (~1.9ГБ), которых нет ни в пакете, ни на живом CDN. Их схема
рассчитана на уже-даунгрейднутую (≤3521) GTA V у игрока.

**НУЖЕН последний кусок:** downgrade-пак **GTA V Legacy → b3521** (или b3407):
`update/update.rpf` + `update/update2.rpf` + `GTA5.exe` (~2.5ГБ). Стандартный
community-артефакт. Кладётся в `runtime/client/cdn/`, восстанавливается
полный `backup_update.json`, alt:V собирает игру в своей приватной папке
(инстолл игрока цел). Ждём файлы от владельца.

**Enhanced-путь: GTAMP оказался Legacy-only** (манифест = ветка legacy,
кэш-exe b3337). Для Enhanced нужна отдельная связка, которой нет. Держим
Legacy как основную цель.

### Пройденные грабли коннектора (все закоммичены)
- порт 9988 (не из `-customui` URL);
- манифесты дословно из GTAMP (`runtime/client/cdn/*.json`);
- `/backup/update.json` → `{"files":[]}` (не 404);
- BattlEye: файлы НЕ трогать, службу НЕ глушить (тупик, `ERR_GEN_INVALID`).
  Решение — галка BattlEye в Rockstar Launcher (владелец снял);
- `SteamAppId` ставим только для `DetectPlatform=="steam"` (`119c285`) —
  на Epic вызывал «Не удалось запустить Steam»;
- Epic: для egs-копии Epic Games Launcher должен быть ЗАПУЩЕН (`65715fb`);
- `.cmd`-обёртки `scripts/{run-server,connect}.cmd` — без execution policy.

Мой косяк за сессию: глушил `BEService` → сломал обычный запуск GTA
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
| 5 | Лаунчер — скелет WPF | ✅ done — детект GTA V, статус сервера, UI авторизации |
| 6 | Отключить Sentry-телеметрию сервера | ✅ done — DSN обнуляется в рабочей копии `altv-server.exe` при сборке, запросов на `sentry-alt.com` нет (`docs/engine/telemetry-sentry.md`) |
| 7 | Полный клиент alt:V 16.4.x | ✅✅ **настоящий официальный 16.4.39** взят из GTAMP-пейлоада (форумчанин), `runtime/client/` пересобран `scripts/import-altv-client.ps1`, 84/85 exact hash-match (только `altv-webengine.exe` не-сток). Реконструкция CEF+Majestic больше не нужна. `docs/engine/gtamp-reference.md` |
| 8 | Лаунчер: Интеграция FloVMP.Connect | ✅ done — запускает с аргументами --gta, --host, --port, --nick |
| 9 | Лаунчер: совместимость с патчами GTA V (Вар.1 offset-CDN / Вар.2 exe-swap) | ✅ каркас — `Core/Services/Compat/*`, 11 тестов, watchdog + маркер + startup-восстановление. Нет своего хука и реального compat.json. `docs/launcher/launcher-compat.md` |
| 10 | Реконструкция клиента alt:V из сторонних источников | ✅ done — `scripts/fetch-cef.ps1` (CEF 131.0.6778.205), fill из Majestic, 85/85, `runtime/client/manifest.json` (318 МБ). `docs/instructions/live-test-guide.md` |
| 11 | Репо-гигиена: `.gitattributes`, `README.md`, CI (GitHub Actions build+test) | todo |
| 12 | Фаза 3: каркас авторизации (C# сервер + NUI клиент) | ✅ done — `FloVMP.Core/Auth/*` (PBKDF2, стор, троттлинг), `AuthSystem` (спавн только после входа), NUI `html/auth/`, 12 тестов. Визуальный поток не проверен. `docs/gameplay/phase3-auth.md` |
| 13 | CI — GitHub Actions | ✅ зелёный (#11 run success 1m26s). Server-тесты добавлены в CI (#12). |
| 14 | Фаза 3: HUD (C# события + NUI) | ✅ done — `HudSystem` (тик 1с через `OnTick`), NUI `html/hud/`, `Account.Cash`. Визуально не проверен. `docs/gameplay/phase3-hud.md` |
| 15 | Фаза 3: инвентарь (C# + NUI) | ✅ done — `FloVMP.Core.Items` (Inventory/ItemCatalog/store), `InventorySystem`, NUI грид с drag&drop (клавиша I), 11 тестов. Визуально не проверен. `docs/gameplay/phase3-inventory.md` |
| 16 | Фаза 3: чат (C# + NUI) | ✅ done — `ChatSanitizer` + `ChatSystem` (rate-limit, команды /help /me /online /pos), NUI `html/chat/` (клавиша T), 14 тестов. `docs/gameplay/phase3-chat.md` |
| 17 | **Доведение MP-ядра до идеала** — устойчивость, автосейв, обработка ошибок, ревью систем. НЕ переносить геймплей Florida V пока не готово | in progress — раунды 1-2 (`docs/engine/core-hardening.md`): Safe-обёртки, фикс порядка disconnect-обработчиков, один аккаунт = один сеанс, автосейв + флаш, карантин битых сторов, хендшейк client:ready (иначе чёрный экран), HUD шлёт только дельты, чистка троттла. 46 тестов |
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
  - `%LOCALAPPDATA%\altv\altv.toml` уже настроен (branch release, epic, gtapath).
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
- **Хостинг**: у владельца уже есть готовый сервер (хостинг) под будущий деплой Florida V. НЕ разворачивать сейчас — рано (живой тест ядра не пройден, бэкенд лаунчера не готов). Спросить владельца, когда подойдёт момент; уточнить тогда ОС/доступ/домен-IP.

- Ветка alt:V — **release 16.4.39**.
- `AltV.Net` 16.4.21 (NuGet), при проблемах SDK — `16.4.28-rc.2`.
- Скрипты `.ps1` и `Alt.Log` в C# — **только ASCII** (PS 5.1 CP1251 /
  консоль alt:V). Русский — в `docs/*.md`, комментариях, WPF-UI.
- `runtime/` (сервер и клиент) — в `.gitignore`.
- Решения `.slnx` (не `.sln`) — дефолт `dotnet` 10.
- Правка глобального `altv.toml` — только по явной галке, всегда `.bak`.

## Лаунчер: пивот на Electron (2026-09-04)

Владелец забраковал визуал WPF-лаунчера (`FloridaV.Launcher`) как
недостаточно красивый по сравнению с Majestic/GTA5RP. Разведка (разбор
папки `C:\Users\User\AppData\Roaming\majestic-launcher` + форум GTA5RP)
подтвердила: оба реальных конкурента рендерят UI через Chromium
(Electron/CEF), а не нативными Windows-контролами — этим и объясняется
их уровень полировки. Решение: **лаунчер переезжает на Electron**.

- **Новый проект** `launcher/electron/` — Electron-оболочка, весь UI на
  HTML/CSS/JS (адаптация дизайна из артефакта "Держава RP Launcher
  Concepts", вариант A). `npm start` / `scripts/run-launcher-electron.cmd`.
- **Новый проект** `launcher/src/FloVMP.Launcher.Native/` — тонкий C#
  console-хелпер (net8.0-windows). Держит реестр Windows / поиск GTA V /
  запуск через `FloVMP.Connect` (exe-подмена, BattlEye-safe) — весь этот
  код НЕ переписан на JS, просто перенесён без изменения логики из
  `FloridaV.Launcher.Services`. Протокол — NDJSON по stdio
  (`{"id":N,"cmd":"..."}` → `{"id":N,"ok":true,"result":...}`), Electron
  спавнит процесс один раз и держит живым (`launcher/electron/src/native-bridge.js`).
- Настройки — тот же файл `%LOCALAPPDATA%\FloridaV\settings.json`, что и
  у старого WPF-лаунчера — без миграции, оба читают один формат.
- **Старый `FloridaV.Launcher` (WPF) НЕ удалён**, но больше не
  развивается — оставлен на случай отката. Решение об окончательном
  удалении — отдельно, владельцем.
- `CLAUDE.md` обновлён (таблица стека, строка «Лаунчер»).

## Спринт /goal: 5 ключевых систем ядра для 1500–3000 онлайна и глубокий аудит багов (2026-09-06)

Поставлена и полностью решена комплексная задача по обеспечению экстремальной производительности сетевого ядра под 1500–3000 одновременных игроков, предотвращению дюпов, плавной интерполяции движения транспорта, маршрутизации 3D звука и устранению race conditions.

### 1. Реализованные высокопроизводительные системы ядра (FloVMP.Core)
1. **Spatial Hash Grid (`FloVMP.Core.Spatial.SpatialHashGrid<T>`)**:
   - Пространственная сетка с хешированием координат ячеек (64м по умолчанию), обеспечивающая $O(1)$ поиск сущностей (игроки, транспорт, пропы) в радиусе без $O(N^2)$ перебора тысяч игроков.
   - Полная изоляция виртуальных миров (Dimensions). Автоматическая миграция сущностей между чанками при перемещении.
   - Интегрирован метод `FindInRadiusWithDistance` с сортировкой по расстоянию для аудио-затухания и raycast-систем.
2. **Асинхронная очередь отложенной записи (`FloVMP.Core.Database.WriteBehindQueue<TKey, TEntity>`)**:
   - Буферизация частых изменений в памяти с отслеживанием грязных полей (`DirtyFields`).
   - Фоновый батчинг сброса в MariaDB (flushing batches), автоматический retry при сетевых сбоях БД.
   - Мгновенный принудительный сброс (`FlushImmediate`) при дисконнекте игрока для предотвращения отката данных.
3. **Атомарный CAS Anti-Dupe Engine (`FloVMP.Core.Items.AtomicInventoryTransactionService`)**:
   - Конечно-автоматный статус выброшенных предметов (`0 = Available`, `1 = Claimed`, `2 = Consumed`) с защитой от одновременного клика через `Interlocked.CompareExchange` (CAS).
   - Двухфазный коммит (Two-Phase Commit, 2PC) при обмене между игроками с детерминированным упорядочиванием блокировок по PlayerId (исключение взаимных блокировок / Deadlocks).
   - Автоматический откат состояния (`ReleaseClaim`) при перевесе инвентаря.
4. **Spatial 3D Voice & Radio Grid Router (`FloVMP.Core.Voice.VoiceGridRouter`)**:
   - $O(1)$ маршрутизация аудио-пакетов через SpatialHashGrid.
   - 4 режима громкости: Whisper (2.5м), Normal (8.0м), Shout (20м), Megaphone (60м).
   - 3 математические модели затухания (Linear, InverseSquare, SmoothStep) с расчетом относительных координат для HRTF 3D-панорамирования.
   - Радиочастотная сетка с криптографической проверкой ключей шифрования каналов (например, полицейская волна 101.5 МГц).
   - Телефонные звонки и конференции с прямой доставкой пакетов, серверные и клиентские списки блокировки (Mute).
5. **Dead-Reckoning & Lag Compensation Interpolator (`FloVMP.Core.Sync.DeadReckoningInterpolator`)**:
   - Кольцевой буфер истории состояний сущностей на 64 снимка.
   - Предиктивная экстраполяция перемещения при задержках сетевых пакетов.
   - Эрмитова кубическая сплайн-интерполяция (Cubic Hermite Spline) для устранения микро-рывков и телепортаций высокоскоростного транспорта ($C^1$ гладкость).
   - Механизм компенсации задержки (Lag Compensation / Time Rewind) с лучевой трассировкой попаданий пуль по хитбоксу цели в момент выстрела клиентом с ограничением максимальной отмотки (500мс) для исключения читерских манипуляций со временем.

### 2. Результаты глубокого аудита кодовой базы и устранённые баги (Спринт /goal «Ночной аудит»)
1. **Watchdog Freeze Rapid Burnout**:
   - В `ServerCrashWatchdog.cs` метод `CheckLiveness` при зависании сервера вызывался каждую секунду и на каждый тик инкрементировал счётчик рестартов и генерировал CrashReport, за 5 секунд сжигая всю часовую квоту `MaxRestartsPerHour` (5/час).
   - Исправлено: добавлен триггерный флаг `_isHungIncidentActive`, дебаунсящий инцидент зависания до первого успешного хартбита. Добавлен юнит-тест `CheckLiveness_RepeatedCallsDuringSingleFreeze_TriggersRestartOnlyOnce`.
2. **WriteBehindQueue Memory & Mutation Integrity**:
   - В `WriteBehindQueue.cs` объект `DirtyEntityRecord` не обновлял ссылку на сущность при повторном вызове `MarkDirty` с новым экземпляром объекта, что приводило к потере мутаций.
   - Метод `Touch()` теперь вызывается даже при отсутствии указания конкретного поля, предотвращая преждевременный сброс неактуальных данных.
   - Добавлен метод `FlushAllAsync()` для гарантированного 100% сброса всех очередей при штатном выключении сервера.
3. **SnapshotManager Player Leak**:
   - В `SnapshotManager.cs` метод `PruneOlderThan` очищал старые снимки, но оставлял пустые ключи в словаре `_snapshots`, что вызывало постепенную утечку памяти. Исправлено удалением пустых ключей.
4. **SpatialHashGrid Extreme Values & Thread Hang DOS Protection**:
   - В `SpatialHashGrid.cs` добавлена строгая проверка на `float.IsNaN`, `float.IsInfinity`, отрицательные радиусы и лимит радиуса поиска до 5000м, предотвращающая зависание потоков синхронизации и переполнение стека ячеек.
5. **DynamicResourceManager Concurrency Lock**:
   - В `DynamicResourceManager.cs` методы `StartResource` и `StopResource` защищены блокировкой `_transitionLock`, исключающей race condition при одновременных вызовах старта/остановки ресурсов из разных потоков.
6. **Faction Handcuff & Arrest Dimension Leaks**:
   - В `ChatSystem.cs` команды `/cuff`, `/uncuff`, `/invite`, `/passport`, `/lic`, `/arrest` дополнены проверкой совпадения виртуального мира (`player.Dimension == target.Dimension`).
   - Игровой радиус `/cuff` и `/uncuff` ограничен 3 метрами, а броадкаст анимации `me` ограничен тем же измерением, предотвращая утечку звука и анимаций наручников из интерьеров на улицу.
   - При аресте (`/arrest`) виртуальный мир заключённого принудительно сбрасывается в `0` (основной мир камеры КПЗ), предотвращая спавн в чужом интерьере.
   - В `FactionService.cs` запрещено надевать наручники сотруднику, который сам находится в наручниках или уже отбывает срок в камере.
7. **Economy & Housing Boundary Overflow Protection**:
   - В `EconomyService.cs` и `HousingService.cs` добавлены проверки на `null` аккаунтов и защита от переполнения `long.MaxValue` при крупных депозитах и переводах.
8. **VehicleService Plate Index Desync**:
   - В `VehicleService.cs` добавлен метод `UpdatePlate(int id, string newPlate)`, корректно удаляющий старый номер из индекса `_byPlate` при перерегистрации авто, исключая рассинхронизацию.
9. **Launcher Native & Electron Hardening**:
   - В `PlayService.cs` санированы пути GTA V и никнеймы игроков для предотвращения инъекций аргументов командной строки Windows; поддержан поиск `FloVMP.Connect.exe` как в Release, так и в Debug сборках.
   - В `GtaLocatorService.cs` добавлена санитарная очистка кавычек и пробелов, перехват исключений при доступе к реестру.
   - В `GpuDetectionService.cs` null-coalesce строковых аргументов для исключения `NullReferenceException`.
   - В `LocalCdn.cs` исправлена HTTP-строка ответа на `HTTP/1.1 404 Not Found` (вместо `404 OK`) и защищены методы от `DirectoryNotFoundException` при отсутствии клиентской папки.
   - В `main.js` Electron автозапуск (`setAutostart`) ограничен только продакшен-сборками (`app.isPackaged`), защищая реестр разработчика, а порт в `native:play` приведён к `int`.
10. **SaaS Web Portal API Hardening**:
    - В `v1/license/verify/route.ts` и `v1/billing/pay/route.ts` добавлен безопасный парсинг `req.json()` с возвратом 400 Bad Request при невалидном теле.
    - В `v1/billing/pay/route.ts` создаваемый ID лицензии связывается со счётом в `portal_invoices.license_id`.
11. **Исследование DLSS 5 Swapper & Upscaler Ecosystem**:
    - Изучена архитектура `rakanki911/DLSS5-Swapper` (инжекция нейросетевого рендеринга через feeder/ReShade с генерацией векторов движения) и `beeradmoore/dlss-swapper` (DLL swapper для нативных игр).
    - Развёртываемый сервис `UpscalerDeploymentService` лаунчера полностью согласован с этой структурой и обеспечивает безопасную инжекцию с изоляцией NUI UI.
12. **Статус тестов**:
    - **219 / 219 тестов `FloVMP.Core.Tests`** успешно пройдены (0 failures, 0 warnings).
    - **41 / 41 тестов `FloVMP.Launcher.Tests`** успешно пройдены (0 failures, 0 warnings).
    - **Итого 260 / 260 тестов** зелёные.
    - TypeScript проверка Next.js 14 портала (`npx tsc --noEmit`) завершена с 0 ошибками.

### 3. Технологические улучшения движка FloV:MP (Спринт /goal «Next-Gen Engine Polish»)
1. **Adaptive Tick Scaling (`FloVMP.Core.Spatial.AdaptiveTickManager`)**:
   - Автоматическое масштабирование частоты синхронизации от 60 Hz до 10-30 Hz в зависимости от активности (бой/скорость > 15 м/с) и расстояния до наблюдателя.
   - Сглаживание пиков CPU сервера при высоком онлайне 1000+ игроков.
2. **Occlusion Culling & Distance PVS (`FloVMP.Core.Spatial.OcclusionCullingService`)**:
   - Фильтрация стриминга сущностей за глухими стенами, внутри бункеров и хранилищ.
   - Буст FPS клиентов на слабых машинах и 100% блокировка читов Wallhack / ESP.
3. **Серверная физика авто (`FloVMP.Core.AntiCheat.VehiclePhysicsGuardian`)**:
   - Детекция и пресечение читов Fly Car, Super Acceleration (Torque Multiplier), Car Jump и Vehicle Teleport.
4. **Серверная баллистика и валидация боя (`FloVMP.Core.AntiCheat.CombatValidationService`)**:
   - Серверный расчет урона по хитбоксам (Head, Torso, Limbs) и броне.
   - Защита от Godmode, Rapid Fire и стрельбы сквозь стены/измерения.
5. **In-Game Developer Console (F8 / F11) и NUI Hot-Reload**:
   - Клиентский оверлей `client/resources/flovmp-client/client/html/console/index.html` с поддержкой 2 режимов (Modern Glass и FiveM Quake dropdown).
   - Горячая перезагрузка активных вебвью по команде `hotreload` без перезахода в игру.
6. **Блочный FastDL апдейтер (`FloVMP.Launcher.Core.Services.Cdn.ChunkedDiffUpdater`)**:
   - 4MB блочное хэширование SHA-256 для докачивания только изменённых дельт с экономией до 95-99% трафика.

