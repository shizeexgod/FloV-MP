# Телеметрия alt:V (Sentry) — отключено

Обновлено: 2026-08-27

## Что нашли

`altv-server.exe` (16.4.39) содержит встроенный **sentry-native 0.6.5** с
захардкоженным DSN:

```
https://586f9304db234ff7bc949b843d4f92dd@sentry-alt.com/4
```

- `sentry-alt.com` — сторонний хост alt:V, **живой** (резолвится в Cloudflare
  `188.114.96.1/97.1`, отвечает `HTTP 400` на ingest — то есть бэкенд Sentry
  реально принимает запросы).
- Данные уходят **не только при краше**: при старте сервера с
  `SENTRY_DEBUG=1` в логе видно `sending request using winhttp to
  "https://sentry-alt.com:443/api/4/envelope/"` — то есть session/environment
  envelope шлётся при каждом запуске (ОС, версия сервера, возможно IP).
- Крэш-минидампы ушли бы на `/api/4/minidump/` через `altv-crash-handler.exe`
  (переименованный Google Crashpad из sentry-native).

Переменные `SENTRY_DSN` / `SENTRY_DEBUG` sentry-native читает, **но** alt:V
после этого вызывает `sentry_options_set_dsn()` с захардкоженным значением —
поэтому `SENTRY_DSN=""` в окружении не помогает.

## Как отключили

`scripts/assemble-runtime.ps1` (функция `Disable-SentryTelemetry`) при сборке
рантайма **обнуляет строку DSN** в `runtime/server/altv-server.exe`
(54 байта `0x00` по смещению строки). Бэкап `C:\ViMP backup\...` не трогается —
патчится только рабочая копия в `runtime/`.

С пустым DSN sentry-native ведёт себя так (проверено, `SENTRY_DEBUG=1`):

```
[sentry] WARN the provided DSN "" is not valid
[sentry] WARN failed to construct minidump URL
[sentry] WARN failed to initialize backend
[sentry] WARN `sentry_init` failed
[sentry] DEBUG shutting down transport
```

→ transport и crashpad-бэкенд не поднимаются, **ни одного запроса на
sentry-alt.com** (проверено grep'ом по отладочному логу). Сервер при этом
стартует полностью нормально: все потоки, `[C#] core: server fully started`.

Отключить патч: `assemble-runtime.ps1 -KeepSentry`.

Если alt:V-сборка другая и строка DSN не найдена — скрипт печатает
предупреждение и НЕ трогает бинарник (телеметрия останется включённой).
Тогда обновить строку `$dsn` в функции под новый бинарник.

## Дополнительная защита (по желанию, руками владельца)

Патч бинарника самодостаточен, но если хочется «пояс и подтяжки»:

1. **hosts** — `C:\Windows\System32\drivers\etc\hosts`:
   `0.0.0.0 sentry-alt.com` (нужны права админа, влияет на всю систему).
2. **Брандмауэр** — исходящее правило block для
   `runtime\server\altv-server.exe` и `altv-crash-handler.exe`
   (нужны права админа).

Оба — системные изменения, в репозиторий не входят.
