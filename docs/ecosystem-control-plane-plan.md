# FloV:MP: централизованная экосистема лицензий, обновлений и проектов

Статус: архитектурный план, команда обновления уже добавлена в репозиторий.
Дата актуализации: 2026-09-19.

Этот документ является единой точкой правды для следующего этапа продукта. Его
нельзя смешивать с текущей серверной совместимостью GTA: Legacy 3889 и Enhanced
имеют отдельные native-gate и остаются неподтверждёнными до настоящего
Windows E2E.

## 1. Что строим

FloV:MP будет централизованной системой для двух сторон:

- владелец FloV:MP управляет ключами, тарифами, релизами, каналами поставки,
  зарегистрированными RP-проектами, серверами, отзывом доступа и аудитом;
- клиент владеет своим RP-проектом, привязанными серверами, настройками,
  лицензией, обновлениями и статусом инстансов в своём кабинете.

Сайт и будущий desktop `.exe` используют один backend и одну модель полномочий.
Desktop-приложение не получает отдельные привилегии: оно вызывает те же API,
показывает тот же dashboard и после проверки манифеста применяет патчи локально.

Лаунчер игрока и публичный сайт RP-проекта не входят в runtime-лицензию или
source-kit. Они могут быть отдельными продуктами экосистемы и подключаться к
проекту через разрешённые API.

## 2. Два товара и доступ

### Runtime-license

Готовый серверный архив для Windows/Linux: серверные бинарники, ресурсы,
шаблоны, SDK и установщик. Нет исходников платформы, `launcher/`, `web/`, GTA V
и неразрешённых бинарников внешнего engine.

### Source-kit

Исходники серверной платформы, клиентские скрипты, SDK, миграции и инструменты
кастомизации. Нет `launcher/`, `web/`, закрытых внутренних компонентов и
alt:V-runtime по умолчанию. Внешний runtime импортируется владельцем только
при наличии прав.

Ключи имеют явный `entitlement`:

```text
runtime-license  -> runtime пакеты, канал обновлений runtime, лимит серверов
source-kit       -> source-kit архивы, исходные обновления, инструменты сборки
launcher         -> отдельный доступ, пока не продаётся вместе с сервером
```

Серверный runtime никогда не должен принимать source-kit архив как обновление.
Source-kit updater никогда не должен накладывать runtime-бинарники в рабочий
сервер. Это проверяется полем `deliveryMode` в манифесте и локальным allow-list.

## 3. Реальные лицензионные ключи

Ключ — это не единственная защита. Он идентифицирует entitlement в backend, а
доступ к файлам выдаётся короткоживущим подписанным lease.

### Жизненный цикл

```text
issued -> active -> suspended -> revoked/refunded
                     \-> expired
```

- `issued`: ключ создан, но ещё не активирован;
- `active`: разрешены соответствующие канал, версия и число серверов;
- `suspended`: временная блокировка до выяснения оплаты/спора;
- `revoked`: окончательный отзыв, например при возврате денег после передачи
  файлов или нарушении условий;
- `expired`: закончился срок действия.

В базе хранится только хэш ключа для поиска и последние четыре символа для
поддержки. Полный ключ показывается владельцу один раз или восстанавливается
через защищённую процедуру. Закрытый RSA/Ed25519 ключ подписи никогда не лежит
в Git, source-kit, ZIP или на сервере клиента; он хранится в secret manager/KMS.

### Операционная ротация ключа подписи

Пара authority key должна быть подготовлена до публикации backend-релиза:

1. Сгенерировать новую RSA-2048 пару вне репозитория и сохранить приватную
   часть только в secret manager (`FLOVMP_AUTHORITY_PRIVATE_KEY`).
2. Обновить встроенный публичный ключ `LicenseFile.AuthorityPublicKeyPem` и
   fixture-тесты сервера в одном изменении.
3. Сначала развернуть backend с новым приватным ключом, затем серверный runtime
   с новым публичным ключом; старый ключ нельзя возвращать в `.env`, Git или
   архивы поставки.
4. Выпустить тестовый `license.flv`, проверить его на чистом runtime и только
   после этого включить выдачу клиентам.

Текущая рабочая копия уже не содержит прежний приватный fallback: без
`LICENSE_SIGNING_SECRET` и `FLOVMP_AUTHORITY_PRIVATE_KEY` web-приложение
отказывается выпускать или подписывать лицензии. Это намеренная fail-closed
политика, а не временная ошибка конфигурации.

### Отзыв после возврата денег

1. Владелец в админ-панели выбирает ключ и причину `refunded`.
2. Backend переводит entitlement в `revoked`, инвалидирует активные leases,
   отзывает download tokens и записывает audit event.
3. Сервер клиента при следующем онлайн-проверочном запросе получает отказ и
   блокирует новые подключения, отключает текущих игроков с предупреждением и
   не возобновляет вход до восстановления действующей лицензии.
4. Уже скачанный `license.flv` не может сам узнать о событии отзыва: поэтому
   для управляемого отзыва нужен lease/revocation слой. Полностью офлайн-режим
   по определению не даёт мгновенного отзыва.

Текущая политика runtime: подписанный lease выдаётся максимум на 24 часа и
проверяется в фоне примерно раз в 5 минут. При краткой недоступности backend
сервер использует только проверенный локальный lease; если lease отсутствует,
действует ограниченный offline grace (по умолчанию 24 часа, настраивается в
`FLOVMP_OFFLINE_GRACE_HOURS`, максимум 168 часов). После явного `revoked`,
истечения lease или offline grace новый вход запрещается, а уже подключённые
игроки получают предупреждение и отключаются. Это фактическая production-
логика; более длинные сроки могут быть отдельной тарифной политикой, но не
должны ослаблять отзыв ключа.

## 4. Централизованный backend

Backend владеет следующими объектами:

```text
User
 └─ Project
     ├─ Entitlements / License keys
     ├─ Releases and update channels
     ├─ Server instances (VDS/PC)
     ├─ Download tokens and leases
     ├─ Telemetry / health
     └─ Audit events
```

### Кабинет владельца FloV:MP

- выпуск, активация, приостановка и отзыв ключей;
- назначение тарифа, срока, лимита игроков/серверов и entitlement-каналов;
- регистрация релиза, загрузка Windows ZIP/Linux TAR.GZ/source-kit;
- SHA-256 и подпись артефакта, обязательные профили совместимости;
- публикация `stable`, `beta`, `hotfix` и минимальной версии обновления;
- список RP-проектов и серверов, health/telemetry, audit log;
- отзыв download token без удаления уже опубликованного релиза.

### Кабинет клиента

- проекты и участники проекта;
- production/staging/development серверы;
- привязка server agent через одноразовый токен;
- статус лицензии, lease, версии и native-gate;
- инструкция установки и готовая команда для ОС;
- выбор доступного обновления согласно entitlement;
- журнал патчей, резервных копий и ошибок.

Серверы клиентов не получают прямой доступ к MariaDB backend. Они используют
HTTPS API с уникальным agent token, TLS и ограниченным набором команд.

## 5. Манифесты релизов и обновления

Каждый релиз содержит подписанный манифест:

```json
{
  "schema": 2,
  "product": "runtime-license",
  "channel": "stable",
  "version": "1.1.0",
  "minVersion": "1.0.0",
  "os": "windows",
  "deliveryMode": "runtime-license",
  "requiredEntitlement": "runtime-license",
  "files": [{"path": "server/...", "sha256": "...", "size": 123}],
  "signature": "ed25519-signature"
}
```

SHA-256 защищает от повреждения, а подпись backend подтверждает происхождение.
URL и checksum без подписи не должны считаться достаточной защитой от подмены
CDN.

Сейчас в репозитории уже есть переносимая команда:

```bash
python scripts/flo_update.py \
  --product runtime-license --root /opt/flovmp \
  --package-url https://HOST/flovmp-server-VERSION-linux.tar.gz \
  --sha256 SHA256_АРХИВА --license-key FLV-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX
```

```powershell
python scripts\flo_update.py `
  --product runtime-license --root C:\FloVMP `
  --package-url https://HOST/flovmp-server-VERSION-windows.zip `
  --sha256 SHA256_АРХИВА --license-key FLV-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX
```

Команда проверяет архив и `deliveryMode`, затем делегирует установку штатному
`install.sh` или `install.ps1`. Их текущая логика сохраняет `config`,
`server.toml`, `voice.toml`, `gamemode`, `license.flv`, ресурсы и data.

Для source-kit используется явное переключение:

```bash
python scripts/flo_update.py \
  --product source-kit --root /work/my-flovmp-source \
  --package source-kit-VERSION.zip --sha256 SHA256_АРХИВА
```

Source-kit обновляется только по `SOURCE-MANIFEST.json`, перед заменой создаётся
`.flovmp-source-backups/<UTC>`. Runtime-пакет к этой команде не подходит.

## 6. Установка и патч как одна система

В будущем desktop `.exe` и CLI получают одинаковую операцию:

```text
выбор проекта -> выбор install/update -> получение entitlement lease
-> выбор ОС/канала -> скачивание подписанного манифеста
-> backup -> staging -> проверка файлов -> commit -> health-check
-> запись результата в audit log
```

Установка создаёт структуру пустой папки. Обновление никогда не удаляет
пользовательские файлы, пока они не перечислены как platform-owned в манифесте.
При ошибке health-check выполняется rollback из backup. Нельзя обновлять
работающий production без lock/stop/restart политики и записи результата.

## 7. Desktop-приложение

Первый вариант — Windows `.exe` с тем же dashboard, что и сайт:

- получает short-lived access token после входа;
- показывает только проекты пользователя;
- проверяет подпись манифеста до скачивания/запуска;
- применяет patch в выбранной папке через локальный privileged helper;
- не хранит приватный signing key и не может менять entitlement;
- умеет dry-run, backup, rollback и открыть отчёт проверки.

Для Linux/VDS остаётся CLI: desktop не нужен для серверной установки.

## 8. Минимальная backend-схема

Текущая `sql/portal_schema.sql` уже содержит пользователей, проекты, лицензии,
серверы и команды. Для управляемых обновлений нужны отдельные миграции:

- `portal_entitlements`: тип доступа, продукт, channel, срок, лимиты;
- `portal_license_events`: issued/activated/suspended/revoked/refunded;
- `portal_releases`: version, product, channel, OS, manifest URL, signature,
  minVersion, published/revoked;
- `portal_download_tokens`: одноразовые короткоживущие ссылки;
- `portal_server_leases`: server id, lease id, issued/validUntil/revokedAt;
- `portal_update_runs`: dry-run, staged, committed, rolled-back, error;
- `portal_audit_events`: кто, что, когда, IP/request id, причина.

Ключи не должны быть единственным foreign key проекта: проект и server id
нужны для аудита, переноса проекта и отзыва отдельного инстанса.

## 9. API-контракт следующего этапа

```text
POST /api/v1/auth/token
GET  /api/v1/projects
GET  /api/v1/projects/{id}/servers
POST /api/v1/projects/{id}/servers/register
POST /api/v1/licenses/{id}/revoke
POST /api/v1/licenses/{id}/lease
GET  /api/v1/releases/latest?product=runtime-license&os=linux&channel=stable
POST /api/v1/releases/{id}/download-token
POST /api/v1/servers/{id}/update-report
```

Лицензия/lease проверяется до выдачи download token. Все mutation endpoints
требуют CSRF-safe session/API token, RBAC и idempotency key.

## 10. Что не делаем сейчас

- не встраиваем в репозиторий закрытый alt:V runtime;
- не обещаем мгновенный отзыв старого офлайн `license.flv` без lease;
- не выдаём source-kit через runtime канал;
- не делаем desktop-приложение раньше стабилизации CLI и Windows E2E;
- не объявляем Legacy 3889 или Enhanced supported без native adapter и
  двухклиентского теста.
- не переводим портал на Next 16 автоматически: текущие route handlers используют
  синхронные `params`/`cookies`, а Next 16 требует Promise-контракты. Для этого
  нужен отдельный миграционный PR с обновлением всех dynamic API routes и
  повторным security-аудитом; текущий портал закреплён на проверенном Next
  14.2.35 + PostCSS 8.5.28.

## 11. Критерии готовности экосистемы

1. Ключ с `revoked/refunded` не получает новый lease или download token.
2. Сервер с валидным cached lease переживает краткий сбой backend до `validUntil`.
3. После истечения grace или явного policy-revoke сервер блокирует вход,
   отключает текущих игроков с предупреждением и не удаляет пользовательские
   данные.
4. Runtime update не содержит `sourceIncluded=true`; source update не содержит
   runtime binaries.
5. Windows и Linux выполняют install/update/rollback на копии, а результат
   отображается в audit log.
6. Один и тот же signed release устанавливается CLI, сайтом и desktop helper.
