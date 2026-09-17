# FloV:MP — исходный код сервера

Мультиплеер для GTA V на движке alt:V 16.4: сервер на C# (.NET 8), клиентские
скрипты и интерфейсы на JavaScript/HTML, база данных MariaDB.

Условия использования — [LICENSE.md](LICENSE.md).

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

# готовые пакеты для установки (dist/server/)
python scripts/pack_server.py --os all
```

Пакеты:

- `flovmp-server-<версия>-windows.zip` — запуск `FloVMP-Server.exe`;
- `flovmp-server-<версия>-linux.tar.gz` — установка `sudo ./install.sh`.

Как ставить и настраивать пакет — `README.md` внутри пакета
(исходник: `scripts/package-templates/common/README.md`).

## Структура

| Путь | Что это |
|---|---|
| `server/src/FloVMP.Core` | библиотека платформы: аккаунты, база и миграции, администраторы, баны, лицензия, экономика, фракции, инвентарь, античит |
| `server/src/FloVMP.Starter` | ресурс `flovmp-starter` — базовая платформа: вход, администрирование, баны, голос, чат, API для модов |
| `server/src/FloVMP.Gamemode` | ресурс `flovmp-core` — пример полного RP-режима на Core (авторизация, HUD, инвентарь, фракции, экономика) |
| `server/src/FloVMP.ServerHost` | `FloVMP-Server.exe` — запуск сервера на Windows |
| `server/tests` | юнит-тесты Core |
| `server/tools/FloVMP.LoadTest` | нагрузочный стенд |
| `client/resources/flovmp-client` | клиентский ресурс: чат, консоль F8, экраны входа и загрузки, HUD, админ-инструменты |
| `sql/migrations` | схема базы; сервер применяет миграции сам при старте |
| `config/server.toml` | эталонная конфигурация сервера (из неё собирается `server.toml.example`) |
| `scripts/pack_server.py` | сборка пакетов |
| `scripts/install.sh` | установщик для Linux |
| `scripts/package-templates` | файлы, которые кладутся в пакет: README, шаблоны настроек, SDK для своего сервера |
| `engine/` | движок alt:V 16.4.39 (release, Windows и Linux) — нужен сборщику пакетов |
| `docs/` | архитектура и разработка |

## Документация

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — как устроен сервер, потоки, хранение данных, безопасность;
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — как дорабатывать: ресурсы, события, команды, база, клиент.
