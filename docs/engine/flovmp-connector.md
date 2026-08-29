# FloV:MP connector (свой запуск клиента alt:V)

Обновлено: 2026-08-28
Задача #19. Родилась из прогона: GTAMP-клиент завязан на GTA V Enhanced
(подменяет `GTA5.exe`), у владельца legacy — не пошло. Нужен свой запуск.

## Что выяснено про механизм alt:V

- Мёртвый CDN alt:V обходится флагом **`-customui http://127.0.0.1:PORT/...`**:
  стоковый `altv.exe` шлёт на этот host:port ВЕСЬ бэкенд-трафик (манифесты
  обновления, проверка веток/токена, скин лаунчера), не только UI.
  Подтверждено разбором `proxy.cpp` GTAMP + живым логом
  (`[GET] //client/release/x64_win32/update.json -> 200` на локальный порт).
- `altv.exe` GTAMP — **сток** 16.3.7 (`sha1 2800e0d6`), не патч. Патч
  (`altv_patched.exe`) — их IP, не используем.
- Запуск: `altv.exe -connecturl altv://connect/<ip> -directlaunch -customui <base>`.
- Подмена `GTA5.exe` (их Вариант 2) — НЕ нужна: `altv-client.dll` 16.4.39 +
  `legacy.dll` умеют legacy-игру. Играем на настоящем `GTA5.exe` игрока.

## Реализация — `launcher/src/FloVMP.Connect/` (console, net8.0-windows)

| Файл | Роль |
|---|---|
| `LocalCdn.cs` | `HttpListener` на `127.0.0.1:<port>`. Роуты (по образцу `proxy.cpp`): `/client/*/update.json` → манифест, собранный из РЕАЛЬНЫХ файлов `runtime/client/` (хэши сходятся, клиент ничего не качает); `/client/*/<file>` → файл с диска; `/launcher/*/update.json` → минимальный; `/skin*` → `cache/skin.bin`; `branch-access`/`auth`/`token` → `{"access":true}`; `client-branches`/`/` → `{"release":"16.4.39"}`; `/backup/*` → только явно подготовленный backup-манифест, иначе пустой список; для Legacy 3889 этот маршрут не используется; прочее → `{}` |
| `AltvToml.cs` | пишет `runtime/client/altv.toml`: `gtapath` = настоящая папка игры; для Epic/Rockstar flow указывает `gtaPlatform='rgl'`, для Steam — `steam`; `update=false`, `crashReporterEnabled=false` |
| `Program.cs` | resolve client-dir + GTA (altv.toml → реестр `InstallFolderEpic` → частые пути) → старт LocalCdn → altv.toml → запуск `altv.exe` → ждать выхода игры → стоп CDN. Флаг `--cdn-only` — поднять только бэкенд (диагностика роутов). |

Проверено headless: все роуты отдают 200/404 как надо. `HttpListener` на
loopback работает без админа.

## Запуск (прогон)

Сервер должен быть поднят (`scripts/run-server.ps1`), затем:

```
C:\FloV-MP\launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe -connect 127.0.0.1:7788
```

Опции: `--client <dir>` `--gta <dir>` `--port <n>` `--no-debug` `--keep-open`.

Логи клиента после запуска: `runtime/client/logs/launcher_*.log`.

## Если alt:V всё равно лезет на мёртвый CDN

Значит `-customui` не переопределяет CDN в этой сборке. Тогда из
`launcher_*.log` берём точный хост CDN (строка после «Using global cdn» /
`curl` fail) и добавляем hosts-запись `127.0.0.1 <host>` + слушаем на :80.
Это фолбэк, встроим в connector.

## Дальше

- Ребренд `skin.bin`: `customUiGameAppName`→`FloVMP`, `primaryColor`→`#ff3d8a`,
  `logo`, `servers` → наш сервер. (Сейчас — GTAMP-версия как есть.)
- Своя `ui/` (Svelte или простой HTML) вместо `-customui` заглушки.
- Обёртка в WPF-лаунчер (дизайн-фаза): та же логика, кнопка «Играть».
- Переименование `altv.exe`→`FloVMP.exe` в рантайме (не критично, игрок
  видит только наш лаунчер).
