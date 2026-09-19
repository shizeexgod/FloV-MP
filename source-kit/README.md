# FloV:MP — исходный код сервера

Мультиплеер для GTA V на движке alt:V 16.4: сервер на C# (.NET 8), клиентские
скрипты и интерфейсы на JavaScript/HTML, база данных MariaDB.

Условия использования — [LICENSE.md](LICENSE.md). В source-kit входят только
сервер, клиентские ресурсы, SDK и инструменты сборки. Веб-сайт и launcher —
внутренние компоненты рабочего репозитория и в поставку не включаются.

Важно: серверный source-kit не заменяет клиентский native-адаптер GTA V.
Профиль Legacy `1.0.3889.0` хранится в `runtime/compat/legacy-3889` и
проверяется отдельным PowerShell-скриптом, но до
подтверждённого Windows E2E-прогона адаптер считается неподдержанным; запуск
с клиентом, рассчитанным на RPF 3521, намеренно блокируется.

## Что нужно

| Для чего | Что |
|---|---|
| сборка | .NET SDK 8, Python 3.10+ |
| сборка Linux-пакета на Windows | Git for Windows (для проверки bash-скриптов) |
| запуск на Windows | .NET 8 Runtime, MariaDB (необязательно) |
| запуск на Linux | Ubuntu 22.04/24.04 или Debian 12 — всё ставит `install.sh` |

## Быстрый старт

```bash
# сборка и тесты
dotnet build server/FloVMP.sln -c Release
dotnet test server/FloVMP.sln

# готовые пакеты для установки (dist/server/): только если владелец отдельно
# добавил разрешённый runtime alt:V в `engine/`
python scripts/pack_server.py --os all
```

Обычная выгрузка исходников намеренно не содержит `engine/`, поэтому без
разрешённого стороннего runtime эта последняя команда должна быть отложена;
сборка и тесты собственного кода при этом работают без него.

Пакеты:

- `flovmp-server-<версия>-windows.zip` — установка через `install.cmd`, затем запуск `FloVMP-Server.exe`;
- `flovmp-server-<версия>-linux.tar.gz` — установка `sudo ./install.sh`.

При выгрузке исходников экспортёр добавляет `SOURCE-MANIFEST.json` с размером и
SHA-256 каждого файла, а рядом с ZIP пишет файл `<архив>.sha256`. Перед передачей
в Linux перейдите в каталог с архивом и выполните `sha256sum -c <архив>.sha256`
(имя ZIP в sidecar хранится относительно этого каталога); в Windows используйте
`Get-FileHash <архив> -Algorithm SHA256`.
После распаковки полной папки выполните `python scripts/verify_source.py` —
скрипт проверит пути, размеры и хэши исходников на обеих ОС.

Как ставить и настраивать пакет — `README.md` внутри пакета
(исходник: `scripts/package-templates/common/README.md`).

## Структура

| Путь | Что это |
|---|---|
| `server/src/FloVMP.Core` | библиотека платформы: база и миграции, администраторы, баны, лицензия, очистка чата |
| `server/src/FloVMP.Starter` | ресурс `flovmp-starter` — базовая платформа: спавн, администрирование, баны, голос, чат, API для своих ресурсов |
| `server/src/FloVMP.ServerHost` | `FloVMP-Server.exe` — запуск сервера на Windows |
| `server/tests` | юнит-тесты Core |
| `server/tools/FloVMP.LoadTest` | нагрузочный стенд |
| `client/resources/flovmp-client` | клиентский ресурс: чат, консоль F8, экран загрузки, админ-инструменты |
| `sql/migrations` | схема базы; сервер применяет миграции сам при старте |
| `config/server.toml` | эталонная конфигурация сервера (из неё собирается `server.toml.example`) |
| `scripts/pack_server.py` | сборка пакетов |
| `scripts/install.sh` | установщик для Linux |
| `scripts/verify-legacy-3889.ps1` | Windows-проверка профиля GTA V Legacy b3889 |
| `scripts/collect-legacy-3889-e2e.ps1` | Windows-диагностический прогон пяти E2E-гейтов b3889 |
| `scripts/verify-legacy-3889-contract.py` | Mac/Linux-проверка контракта b3889 без запуска GTA |
| `scripts/package-templates` | файлы, которые кладутся в пакет: README, шаблоны настроек, SDK для своего сервера |
| `runtime/compat/legacy-3889` | профиль отпечатков и E2E-гейты для будущего native-адаптера b3889 |
| `engine/` | локальный сторонний runtime alt:V; в обычный source-kit не входит, нужен только для сборки runtime-пакетов при наличии прав |
| `docs/` | архитектура и разработка |

## Документация

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — как устроен сервер, потоки, хранение данных, безопасность;
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — как дорабатывать: ресурсы, события, команды, база, клиент;
- [DELIVERY-MODES.md](DELIVERY-MODES.md) — чем отличаются runtime-пакет и source-kit;
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) — границы прав на alt:V и другие зависимости.
