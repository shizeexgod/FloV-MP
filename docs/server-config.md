# Формат конфига сервера alt:V (восстановлено)

Обновлено: 2026-08-27

В бэкапе не было **ни одного** примера `server.toml` / `resource.toml` —
формат восстановлен по открытой документации alt:V и проверен живым стартом.
Рабочие шаблоны в репозитории:

- `config/server.toml` — конфиг сервера
- `server/resources/flovmp-core/resource.toml` — C#-ресурс
- `client/resources/flovmp-client/resource.toml` — JS-ресурс

## `server.toml`

TOML в корне рабочей папки сервера (рядом с `altv-server.exe`). Наши ключи:

| Ключ | Значение у нас | Смысл |
|---|---|---|
| `name` | `"FloV:MP Dev"` | Имя сервера |
| `host` | `"0.0.0.0"` | Слушать все интерфейсы |
| `port` | `7788` | UDP+HTTP порт (alt:V держит оба на одном номере) |
| `players` | `128` | Лимит слотов (dev) |
| `announce` | `false` | **Не** анонсироваться в мастер-листе alt:V (он выключен Take-Two, и нам он не нужен — свой independent-MP) |
| `useEarlyAuth` | `false` | Не использовать early-auth сервис alt:V |
| `useCdn` | `false` | Не тянуть ничего с CDN alt:V |
| `token` | *(не задан)* | Нужен только при `announce = true` |
| `debug` | `true` | Dev-режим: разрешён reconnect и подключение dev-клиента. **Снять перед продакшеном.** |
| `duplicatePlayers` | `4` | Сколько коннектов с одного HWID. Нужно для локального теста: 2 клиента GTA V с одной машины |
| `modules` | `["csharp-module","js-module"]` | Какие модули движка грузить |
| `resources` | `["flovmp-core","flovmp-client"]` | Наши ресурсы, в порядке загрузки |
| `[tags]` | пустая секция | Валидный якорь формата; теги мастер-листа не нужны |

### Прочие известные ключи (не используем сейчас)

`password`, `gamemode`, `website`, `language`, `description` (косметика /
мастер-лист), `streamingDistance`, `migrationDistance`, `timeout`,
`[voice]`-секция (`bitrate`, `externalSecret`, `externalHost`,
`externalPort`).

### `curl code 6` при старте

Даже с `announce=false`/`useCdn=false` движок 16.4.39 пишет 1–2
`HTTP Request failed, curl code: 6`. Не блокирует. Подробнее —
`docs/engine-recon.md`.

## `resource.toml`

Лежит в корне папки ресурса (`resources/<имя>/resource.toml`).

### C# (серверный геймод) — `flovmp-core`

```toml
type = "csharp"
main = "FloVMP.Gamemode.dll"
```

Рядом с `main` должны лежать зависимости: `AltV.Net.dll`,
`AltV.Net.CApi.dll`, `AltV.Net.Shared.dll`, `FloVMP.Gamemode.deps.json`.
Их кладёт `dotnet publish` (в проекте включены `EnableDynamicLoading` +
`CopyLocalLockFileAssemblies`).

### JS (клиентский) — `flovmp-client`

```toml
type = "js"
client-main = "client/index.js"
client-files = [ "client/*" ]
```

Серверного `main` нет — вся серверная логика в C#-ресурсе. `client-files`
перечисляет, что движок отдаёт клиенту (маска путей внутри ресурса).

## Порядок загрузки и связь ресурсов

1. `flovmp-core` (C#) — подписывается на `OnPlayerConnect`, спавнит игрока,
   шлёт ему клиентское событие `flovmp:client:welcome`.
2. `flovmp-client` (JS) — ловит `flovmp:client:welcome` через
   `alt.onServer(...)`, логирует и пробует нативное уведомление.

Это единственная точка связи C# ↔ JS на Фазе 1 (round-trip проверка).
