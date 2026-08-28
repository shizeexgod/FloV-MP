# GTAMP — референс независимого MP на alt:V

Обновлено: 2026-08-28
Источник: папку скинули на форуме (`...\UnigramPreview...\gtamp`). НЕ в репозитории.

GTAMP — чужой готовый проект: независимый мультиплеер поверх alt:V без
бэкенда alt:V. Ровно то, что делает FloV:MP. Изучаем архитектуру и берём
из него **официальные бинарники alt:V** (это свободно распространяемый SDK,
как и наш бэкап). Код (C++) — только для понимания подхода, не копипастим.

## Что в папке

| Путь | Что |
|---|---|
| `sources/payload/` | **полный официальный клиент alt:V 16.4.39** (sdk `c150769`), 86/86 hash-match с манифестом. `altv-client.dll` = `287a4443…` — бит-в-бит наш сервер. Единственный не-сток файл — `cef/altv-webengine.exe` (модифицирован под их UI) |
| `sources/cpp/connect.cpp` | их лаунчер-коннектор: AES-256 расшифровка `.pack`, менеджмент процессов, self-update по манифесту через `httplib` |
| `sources/cpp/proxy/proxy.cpp` | **обход мёртвого бэкенда** — DLL-прокси, инжектится в клиент, перехватывает сетевые вызовы к CDN/мастер-листу alt:V |
| `sources/cpp/config/config.cpp` | чтение `.config` |
| `sources/cpp/packer.cpp` + `mpN_extractor.cpp` | их формат `.pack` — payload клиента пакуется в 7 зашифрованных архивов (`mp1..mp7.pack`), `connect.exe` распаковывает в рантайме (анти-рип + раздача) |
| `sources/cpp/manifest_generator.cpp` | генератор update-манифеста |
| `sources/altv-ui/` | **Svelte-UI** — замена встроенного лаунчера/паузы alt:V (`App.svelte`, `PauseMenu.svelte`) |
| `sources/altv-resources/update_release_modified.json` | манифест клиента, версия `16.4.39` — переписан под их раздачу |
| `sources/altv-resources/launcher_update_modified.json` | манифест лаунчера `altv.exe`, версия `16.3.7` (лаунчер обновляется реже клиента) |
| `sources/altv-resources/EAC7B9E…` + `backup_update.json` | кэшированный `GTA5.exe` (58 МБ, Enhanced-билд) + манифест R* update-докачки — их реализация «Варианта 2» (подмена/докачка игры) |
| `sources/build.bat` | сборка всего: proxy.dll, config.dll, discord_rpc.dll, extractors, packer, manifest, connect.exe (нужен VS2022 + `cl.exe`) |
| `server/` | рабочий сервер: `gtamp-server.exe` (= `altv-server.exe`), `server.toml`, `.cfg`, `data/*.bin`, `modules/js-module/` |
| `client-new/` | их актуальный клиент: `connect.exe` + `packs/mp1..7.pack` + `.config` |
| `altv-js-module-v1/` | исходники JS-модуля alt:V (git-репо, для самостоятельной сборки) |

## Выводы для FloV:MP

### 1. Клиент — решено

`runtime/client/` пересобран из `sources/payload/` скриптом
`scripts/import-altv-client.ps1`. Настоящий официальный 16.4.39, та же
версия что сервер. Реконструкция (CEF+Majestic) больше не нужна, все 4
открытых вопроса сняты.

### 2. Обход мёртвого бэкенда — их способ лучше нашего

Сейчас наш лаунчер запускает `altv.exe -connecturl … -noupdate` (хрупко:
`-noupdate` может не сработать, `altv.exe` — это их launcher-UI 14.4).

GTAMP: свой `connect.exe` на C++ + `proxy.dll` (инжект, перехват вызовов к
CDN/мастер-листу) + модифицированный `update.json`, указывающий на свою
раздачу. Никакого `altv.exe`-UI вообще.

**План:** переписать «Играть» в нашем WPF-лаунчере по этому паттерну —
локальный перехват/заглушка бэкенд-запросов + прямой запуск клиента, а не
надежда на `-noupdate`. (Реализация — своя, C#/managed, не порт их C++.)

### 3. `server.toml` — сверились

Их конфиг = официальный шаблон alt:V (полный, с комментариями). Полезное,
чего у нас не было:
- `[threads]` — тюнинг под нагрузку: `syncSend = 8`, `syncReceive = 2` на
  12-поточное железо (прямо про наш таргет 1500–3000).
- `allowUnknownRPCEvents = false` → кик за неизвестные RPC (анти-чит).
- `maxClientScriptEventSize` / `maxServerScriptEventSize` — лимиты размера
  событий (защита от DoS).
- `hashClientResourceName = true` — обфускация имён ресурсов (анти-рип).
- `[maxStreaming]`, `[pools]` — лимиты стриминга/пулов GTA.
- `players = 1052`, `spawnAfterConnect = false` (как у нас).

Обновить наш `config/server.toml` этими секциями (закомментированными, с
пояснениями) — отдельная мелкая задача.

### 4. `.pack` + Svelte-UI + свой `connect.exe`

Их полноценная дистрибуция клиента. Нам актуально позже, когда лаунчер
пойдёт к игрокам. Пока `runtime/client/` как папка — достаточно для теста.
