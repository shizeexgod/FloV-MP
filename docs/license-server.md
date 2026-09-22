# Сервер лицензий FloV:MP (для владельца платформы)

Пока нет сайта, ключи выдаются и управляются командами на VDS
`188.127.229.224`. Там же работает раздача пакетов покупателям.

## Как устроено

```
вы (SSH на VDS)            VDS 188.127.229.224                       сервер клиента
flovmp-license new  ──►   MariaDB flovmp_licensing                  config/flovmp.env: FLOVMP_LICENSE_KEY
                          служба flovmp-license (127.0.0.1:7800) ◄── при запуске: download-by-key → license.flv
                          nginx :80 /api/ → служба             ◄── каждые 5 мин: verify → подтверждение на 24 ч
```

- **Таблицы:**
  - `licenses` — ключ, проект, владелец, контакт, тариф, слоты, лимит серверов, статус, срок, заметка;
  - `activations` — серверы клиента: ID установки, IP, версия, когда был на связи;
  - `license_events` — журнал: выдача, активации, отказы, приостановки, продления.
- **Статусы ключа:** `issued` (выдан) → `active` (первый сервер активировал) →
  `suspended` (приостановлен, можно вернуть) / `revoked` (отозван навсегда).
  Истёкший срок — отдельная проверка: после `extend` ключ снова действует.
- **ID установки** — хэш «машина + папка установки». Повторный запуск и обновление
  его не меняют; копия папки на другую машину — это уже новый сервер в лимите ключа.
- **Привязка активации** — одновременно к ID установки и внешнему IP, который
  nginx получает из `$remote_addr`. Для переезда сначала выполняется `unbind`.
  Подписанный online lease тоже содержит ID установки и не принимается другим
  сервером. Число слотов проверяется по `maxPlayers`, а параллельные первые
  активации не могут обойти `maxServers`.
- **Подписи:** `license.flv` и подтверждения подписаны ключом
  `/etc/flovmp-license/authority.pem`, открытая часть вшита в сервер FloV:MP
  (`LicenseFile.AuthorityPublicKeyPem`). Подделать ответ без этого ключа нельзя.
  Ключам старого портала `flovmp.ru` сервер больше не доверяет.
- **Без связи с VDS** сервер клиента работает, пока действует последнее
  подтверждение (24 ч), потом закрывает вход.

## Команды (на VDS, через sudo)

```bash
flovmp-license new --project "Держава RP" --owner "Иван Петров" --contact "tg:@ivan" \
                   --plan business --slots 1000 --servers 1 --days 365     # или --lifetime
flovmp-license list [--status active]
flovmp-license show FLV-...                 # серверы клиента и последние события
flovmp-license suspend FLV-... --reason "неоплата"
flovmp-license resume FLV-...
flovmp-license revoke FLV-... --reason "возврат"
flovmp-license extend FLV-... --days 30     # или --until 2027-12-31, --lifetime
flovmp-license set FLV-... --slots 1500 --servers 3
flovmp-license unbind FLV-... <ID сервера|all>   # переезд клиента на другую машину
flovmp-license events [FLV-...] --limit 100
```

Приостановка, отзыв и продление доходят до серверов клиента при их следующей
проверке, то есть в течение 5 минут. При приостановке и отзыве игроков выкидывает
с объяснением. После продления или смены тарифа сервер сам получает новый
`license.flv`.

## Установка на VDS (один раз)

1. `python scripts/distribution/make_vds_bundle.py --with-key` — комплект в `dist/vds`
   (~1 МБ): программа, скрипт установки, загрузчики и ключ подписи.
2. Залить `dist/vds` на VDS и выполнить `sudo bash setup-vds.sh <папка>`.
   Скрипт ставит .NET 8 (если нет), создаёт базу и пользователя MariaDB со
   случайным паролем (`/etc/flovmp-license/license.env`), кладёт ключ подписи в
   `/etc/flovmp-license/authority.pem`, а из комплекта его стирает. Затем запускает
   службу `flovmp-license` и добавляет в nginx внутреннюю папку для отдачи пакетов.
3. Проверка: `curl http://188.127.229.224/api/v1/license/health`.

**Резервная копия ключа подписи обязательна:** `%USERPROFILE%\.flovmp\license-authority.pem`
на вашем ПК, плюс копия в надёжном месте. Потеряете ключ — придётся выпускать
новую сборку сервера для всех клиентов.

Обновление службы — тот же `setup-vds.sh` из нового комплекта, уже без `--with-key`.

## Выпуск версии для покупателей

1. `python scripts/pack_server.py` — пакеты в `dist/server`.
2. `python scripts/distribution/sign_release.py` — подписанный релиз в `dist/release`
   (ключ релизов `%USERPROFILE%\.flovmp\release-signing.pem`, тоже с резервной копией).
3. Залить и опубликовать через REDL:
   `python scripts/distribution/publish_release_vds.py dist/release-<версия>`.
   Скрипт возобновляет загрузку, сверяет удалённый SHA-256 и вызывает
   `flovmp-license publish`; вручную копировать большие архивы не требуется.

Покупатель скачивает пакет только по действующему ключу: `get.sh` / `get.ps1`
или `install.sh` одной командой (см. `INSTALL.md` в пакете).

## Когда появится сайт

Сайт работает с той же базой `flovmp_licensing` (или вызывает `flovmp-license`).
Протокол серверов клиентов менять не нужно. HTTPS: как только будет домен,
выпустить сертификат Let's Encrypt для nginx и сменить `LicenseConfig.DefaultAuthorityUrl`
на `https://домен`. Серверы, у которых адрес явно задан в `FLOVMP_LICENSE_URL`,
перенастраиваются этой переменной.
