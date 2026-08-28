# Клиент alt:V: прямое подключение и состояние бэкапа

Обновлено: 2026-08-27

## Что на машине разработки (разведка)

| Что | Значение |
|---|---|
| GTA V | **Epic**, legacy-билд (`GTA5.exe` 47 МБ), путь `C:\Program Files\9d2d0eb64d5c44529cece33fe2a46482` |
| Реестр | `HKLM\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V` → `InstallFolderEpic` = тот же путь |
| Rockstar Launcher | стоит, `Language = ru-RU` |
| Steam | стоит (`C:\Program Files (x86)\Steam`), GTA V в Steam-библиотеках **нет** |
| Конфиг alt:V | `%LOCALAPPDATA%\altv\altv.toml` уже есть: `branch='release'`, `gtaPlatform='egs'`, `gtapath=<epic путь>`, `lang='ru'`, `permissionsSet=false` |

`altv.toml` — **глобальный** файл клиента alt:V (общий с обычным alt:V, если
игрок им пользуется). Лаунчер его читает; писать в него — только по явному
согласию (галка «синхронизировать altv.toml», делает `.bak`).

## `altv.exe` — это launcher-UI, не голый клиент

`client/release/x64_win32/altv.exe` — RmlUi-лаунчер alt:V (в логе
`Starting alt:V Launcher 14.4`). Он же апдейтер. Штатно тянет клиент с CDN
alt:V (мёртв → `curl code 6`).

### Флаги командной строки (вытащены из бинарника)

| Флаг | Смысл |
|---|---|
| `-connecturl "<altv://connect/...>"` | цель прямого подключения (этот же флаг получает protocol-handler) |
| `-noupdate` | **не обновляться с CDN** — ключ к обходу мёртвого CDN |
| `-branch <release\|rc\|dev>` | ветка клиента |
| `-directlaunch` | сразу в игру, без окна лаунчера |
| `-offline` | оффлайн-режим |
| `-gtaexe <path>` | путь к GTA5.exe |
| `-changegamepath` | сбросить путь к игре |
| `-skipprocesscheck`, `-skipprocessconfirmation` | не проверять уже запущенные процессы (нужно для 2 клиентов на одной машине) |
| `-templauncher`, `-debug` | служебные |

### Схема прямого подключения, которую делает лаунчер FloV:MP

```
altv.exe -connecturl "altv://connect/127.0.0.1:7788?nickname=<ник>" -noupdate -branch release -skipprocesscheck -skipprocessconfirmation
```
рабочая директория = папка ядра клиента.

Ник: явного `-nickname` во флагах нет. Передаём в query connect-URL и/или
пишем в `altv.toml` ключ `name` (при включённой синхронизации). Что именно
подхватит клиент при `-connecturl` — **проверяется живым тестом**.

## БЛОКЕР: бэкап клиента неполный

`scripts/assemble-client-core.ps1` сверяет папку клиента с её же
`update.json` (85 файлов). В бэкапе — **17 из 85**. Отсутствуют критичные:

- `libs/legacy.dll` — поддержка legacy-`GTA5.exe` (ровно наш случай)
- `libs/resources.pak`, `libs/icudtl.dat`, `libs/snapshot_blob.bin`,
  `libs/v8_context_snapshot.bin` — CEF/V8, без них webengine не стартует
- `libs/libcrypto-3-x64.dll`, `libs/libssl-3-x64.dll` — TLS
- `libs/d3dcompiler_47.dll`, `libs/dxcompiler.dll`, `libs/dxil.dll` — шейдеры
- все `cef/locales/*.pak` (~55 файлов)

Полный список — `runtime/client/MISSING.txt` после запуска скрипта.

**Пока эти файлы не найдены, `altv.exe` игру не запустит.** Всё остальное
(сервер, лаунчер, компоновка команды) готово и ждёт только полный клиент.

### Как закрыть блокер

Нужен **полный набор файлов клиента alt:V 16.4.39 (ветка release, x64_win32)**.
Возможные источники: рабочая установка alt:V на другой машине/диске (папка,
куда alt:V ставил клиент — не `%LOCALAPPDATA%\altv`, а выбранная при
установке), другой бэкап, зеркало CDN alt:V, если сохранилось.

Затем:
```
powershell -File scripts/assemble-client-core.ps1 -FillFrom "<путь к полному клиенту>"
```
Скрипт дособерёт `runtime/client/` и, если все 85 на месте, отчитается
`Client core complete`.
