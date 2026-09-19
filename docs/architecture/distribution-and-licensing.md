# FloV:MP — Поставка, лицензирование и обновления

> **Актуальная спецификация:** 2026-09-19
> **Статус:** серверная установка и локальная проверка лицензии реализованы;
> централизованный lease/revocation и desktop dashboard находятся в плане.

---

## 1. Концепция: два товара, один контрольный слой

Поставка разделена на runtime-license и source-kit. Оба продукта получают
доступ к релизам через централизованный backend, но никогда не смешиваются.

```
                         ┌─────────────────────────┐
                         │  Backend FloV:MP / ЛК   │
                         │  ключи, проекты, релизы │
                         └───────────┬─────────────┘
                                     │ entitlement + signed release
                 ┌───────────────────┴───────────────────┐
                 ▼                                       ▼
       runtime-license channel                    source-kit channel
       Windows ZIP / Linux TAR.GZ                 исходники и SDK
```

### Runtime-license

Готовый архив не содержит клиентский GTA, лаунчер, сайт, исходники платформы
или чужой runtime. `license.flv` и `config/flovmp.env` не входят в общий
пакет: установщик получает лицензию по ключу или владелец кладёт выданный файл
сам. Это не позволяет обновлению затереть настройки клиента.

### Установка в одну команду (Linux/VDS)
- **Команда:**
  ```bash
  curl -fsSL https://HOST/install.sh -o install.sh
  chmod +x install.sh
  sudo ./install.sh --package-url https://HOST/flovmp-server-VERSION-linux.tar.gz \
    --sha256 SHA256_АРХИВА --key FLV-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX
  ```
- **Что скрипт делает автоматически (за ~30–45 секунд):**
  1. Проверяет ОС (Ubuntu 22.04 / 24.04, Debian 12) и права root.
  2. Устанавливает системные библиотеки: `libatomic1`, `curl`, `jq`, `tar`, `ca-certificates`.
  3. Проверяет и при необходимости устанавливает `.NET 8 CoreCLR Runtime` из официального репозитория Microsoft.
  4. Создаёт рабочие каталоги `/opt/flovmp` и `/opt/flovmp/voice`.
  5. Скачивает только выбранный и проверенный runtime-релиз.
  6. Сохраняет ключ в `config/flovmp.env`, а `license.flv` получает только из разрешённого источника.
  7. Разворачивает локальную MariaDB, создаёт БД `flovmp_server` (или с именем проекта) и создаёт отдельного пользователя со случайным паролем; таблицы создаёт сервер миграциями `sql/migrations`.
  8. Регистрирует две связанные `systemd`-службы:
     - `flovmp.service` (основной игровой сервер).
     - `flovmp-voice.service` (внешний 3D-голос с привязкой `PartOf=flovmp.service`).
  9. Автоматически запускает сервер и выводит зелёный отчёт со статусом портов (UDP 7788, Voice 7797/7798).

### Source-kit и личный кабинет

Source-kit выдаётся отдельным entitlement. В нём нет `launcher/`, `web/`,
закрытых компонентов и alt:V engine по умолчанию. Импорт внешнего runtime и
сборка пакета описаны в `source-kit/DELIVERY-MODES.md`.

В личном кабинете показываются подписанная лицензия, команда установки/патча,
канал релиза и SHA-256. Загрузка и обновление доступны только при совпадении
entitlement с `deliveryMode` манифеста.

### Установка разработчиком на VDS клиента
- Старый `deploy-licensee.sh` удалён (2026-09-17): генерировал поддельную лицензию,
  ставил базу под root без пароля. Единственный установщик — `scripts/install.sh`
  внутри пакета `scripts/pack_server.py` (см. `docs/install-checklist.md`).
  Лицензия скачивается только настоящая, по `--key`; без ключа установка идёт без неё.

Для общего install/update CLI используется `scripts/flo_update.py`. Он
отвергает неправильный `deliveryMode`, проверяет SHA-256 архива и для source-kit
создаёт резервную копию перед заменой исходников. Полный план централизованных
ключей, отзывов, leases, релизов и desktop-приложения находится в
`docs/ecosystem-control-plane-plan.md`.

После ротации authority key приватная RSA-часть должна находиться только в
secret manager backend. Runtime содержит только соответствующий публичный ключ;
если секрет backend не настроен, выпуск `license.flv` останавливается с ошибкой.

---

## 2. Архитектура Голосового сервера (Решение проблемы с `PartOf=`)

### Проблема:
Внешний голосовой сервер `altv-voice-server` держит ровно **одно TCP-соединение** с игровым процессом. Если игровой сервер перезапускается (краш, админский рестарт, смена ресурсов), а процесс голоса остаётся работать, внешнее соединение рвётся, и голос **молча отваливается** без ошибок в консоли.

### Решение через Systemd:
В конфигурацию голосового сервиса добавлена директива `PartOf=flovmp.service`:

#### `/etc/systemd/system/flovmp-voice.service`:
```ini
[Unit]
Description=FloV:MP Voice Server (alt:V 16.4.39 external voice)
After=network.target
Before=flovmp.service
# PartOf гарантирует, что рестарт или стоп игрового сервера немедленно перезапускает голос:
PartOf=flovmp.service

[Service]
Type=simple
WorkingDirectory=/opt/flovmp/voice
ExecStart=/opt/flovmp/voice/altv-voice-server
Restart=always
RestartSec=3
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
```

#### `/etc/systemd/system/flovmp.service`:
```ini
[Unit]
Description=FloV:MP Game Server
After=network.target mariadb.service flovmp-voice.service
Wants=flovmp-voice.service

[Service]
EnvironmentFile=-/opt/flovmp/config/flovmp.env
Type=simple
WorkingDirectory=/opt/flovmp
ExecStart=/opt/flovmp/start.sh
Restart=always
RestartSec=5
LimitNOFILE=65535
StartLimitIntervalSec=0

[Install]
WantedBy=multi-user.target
```

---

## 3. Разделение Базы Данных: Портал vs Игровые Серверы Заказчиков

```
┌─────────────────────────────────────────────────────────────┐
│                    ВАШ VDS / ОБЛАКО                         │
│   БД ПОРТАЛА (MariaDB): portal_users, portal_projects,      │
│   portal_licenses, portal_servers, биллинг, токены          │
└──────────────────────────────┬──────────────────────────────┘
                               │ (Только защищённые HTTPS API:
                               │  проверка лицензии, телеметрия, команды)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                 СЕРВЕР ЗАКАЗЧИКА (ЕГО VDS)                  │
│   ИЗОЛИРОВАННАЯ ЛОКАЛЬНАЯ БД (MariaDB @ 127.0.0.1:3306):    │
│   accounts, characters, vehicles, inventory, factions       │
└─────────────────────────────────────────────────────────────┘
```

1. **База данных Портала (`portal_*`):**
   - Находится только на вашей инфраструктуре.
   - Серверы заказчиков **не имеют** прямых MySQL-доступов к ней.
   - Обмен данными идёт строго через REST API / Agent Polling по уникальному `agent_token`.
2. **База данных Игрового сервера заказчика:**
   - Разворачивается локально на VDS каждого заказчика (по умолчанию `flovmp_server` или кастомное имя проекта).
   - Нулевая задержка SQL-запросов (сокет `localhost`).
   - Изоляция данных: проблемы одного проекта никогда не затронут чужие базы или сайт.

---

## 4. Архитектура Оптимизации на 5000+ Онлайна

Для поддержки экстремального онлайна без лагов и фризов внедрены:

1. **`SpatialHashGrid<T>` (Хеш-сетка пространства $O(1)$):**
   - Вместо линейного перебора всех 5000 игроков (`Alt.GetAllPlayers()`) для проксимити-чата (`/me`, `/s`), войса и стриминга мир разбит на пространственные ячейки по 64 метра.
   - Поиск соседей выполняется за время $O(1)$.
2. **`AdaptiveTickManager` (Адаптивное масштабирование тиков):**
   - 4 уровня частоты тикрейта для сущностей:
     - **Combat / High Speed (60 Hz):** бой, стрельба, быстрая езда.
     - **Standard Nearby (45–60 Hz):** игроки в радиусе видимости.
     - **Passive / Distant (20–30 Hz):** игроки на средней дистанции.
     - **Interior / Far (10–15 Hz):** игроки в интерьерах или на другом конце карты.
   - Динамически адаптируется под загрузку CPU сервера (`ServerLoadFactor`).
3. **Отсутствие искусственных ограничений онлайна:**
   - План Enterprise поддерживает 5000+ слотов.
   - Владелец проекта может переключить любой сервер в режим **«Безлимит»** (`slot_limit = NULL`) в личном кабинете.
   - Для внутренних нужд (Dev, Test, Staging) владелец может задать фиксированный лимит слотов (например, 64 или 128) прямо из UI дашборда.
