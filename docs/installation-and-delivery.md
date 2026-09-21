# Установка и состав поставки FloV:MP

Документ описывает два разных товара. Они не объединяются в одну команду и не
требуют сайта или лаунчера.

## 1. Runtime-лицензия

Клиент получает готовый серверный архив и ключ лицензии. В архиве нет GTA V,
клиента игрока, лаунчера или `web/`; это серверный runtime с шаблонами ресурсов.

Два способа — оба с ключом лицензии, оба проверяют целостность сами.

### Способ 1: одной командой (с VDS раздачи FloV:MP)

**Linux (Ubuntu / Debian):**

```bash
curl -fsSLo flovmp-get.sh http://188.127.229.224/cdn/get.sh
sudo bash flovmp-get.sh --key FLV-XXXX-XXXX-XXXX --owner-sc <SocialClubId владельца>
```

**Windows (PowerShell):**

```powershell
iwr http://188.127.229.224/cdn/get.ps1 -OutFile flovmp-get.ps1
powershell -ExecutionPolicy Bypass -File .\flovmp-get.ps1 -Key FLV-XXXX-XXXX-XXXX
```

Загрузчик по ключу получает описание последнего релиза (`release-<os>.txt`),
проверяет его **подпись ключом релизов FloV:MP** (открытый ключ вшит в
загрузчик), скачивает пакет, сверяет SHA-256 и запускает штатный установщик
из пакета. Раздача идёт по HTTP, но подмена пакета — на сервере раздачи или по
дороге — не пройдёт: без закрытого ключа релизов (он только на ПК владельца
платформы) верную подпись не сделать. Параметры `install.sh` (`--public-host`,
`--owner-sc`, `--no-db`...) передаются как есть.

Сервер раздачи выдаёт пакет только по действующему ключу из своего списка
(`flovmp-dist keys ...`, см. раздел «Раздача пакетов» ниже). Неверный ключ —
отказ с понятной причиной; 20 неверных попыток за 10 минут с одного IP — пауза.

### Способ 2: из архива

Покупатель получает архив (`flovmp-server-<версия>-linux.tar.gz` или
`-windows.zip`) и ключ.

- Linux: `tar -xzf flovmp-server-*-linux.tar.gz && cd flovmp-server-* && sudo ./install.sh --key FLV-... --owner-sc ...`
- Windows: распаковать ZIP и запустить `install.cmd` (или `install.ps1 -LicenseKey FLV-...`).

Установщик проверяет SHA-256 каждого файла по `manifest.txt`. На Linux он ставит
сервер в `/opt/flovmp`, создаёт системного пользователя, базу, миграции и unit
systemd; повторный запуск из пакета новой версии — обновление с резервной
копией и откатом при неудачном старте. Файлы владельца (`config/flovmp.env`,
`server/server.toml`, `server/config/*`, ресурсы, данные) не трогаются никогда.

С ключом установщик записывает его в `config/flovmp.env` и получает подписанный
`license.flv` с портала. Сервер проверяет подпись локально и периодически
получает online lease; при сбое сети действует ограниченный offline grace
(по умолчанию 24 часа). При отзыве ключа новые подключения блокируются сразу.

**Порты:** игра — TCP+UDP `7788`; голос alt:V — UDP `7895`; клиенты GTA Legacy
1.0.3889.0 и их голос — TCP+UDP `7798` (порт игры + 10). Установщик Linux
открывает их в ufw сам.

### Раздача пакетов (для владельца платформы)

Один раз на VDS: `sudo bash scripts/distribution/setup-vds.sh <папка с flovmp_dist.py, get.sh, get.ps1>`.

Выпуск версии:

1. `python scripts/pack_server.py` — пакеты в `dist/server`.
2. `python scripts/distribution/sign_release.py` — подписанный релиз в `dist/release`
   (первый раз: `--init-key`; закрытый ключ `%USERPROFILE%\.flovmp\release-signing.pem`
   хранить в резервной копии — без него новые релизы загрузчики не примут).
3. Залить папку на VDS и `sudo flovmp-dist publish <папка>`.

Ключи покупателей: `sudo flovmp-dist keys add FLV-XXXX-XXXX-XXXX --note "Проект" [--expires 2027-09-21] [--os linux]`,
`keys list`, `keys disable|enable|remove`. Журнал скачиваний —
`/var/lib/flovmp-dist/downloads.log` (вместо ключа — его отпечаток).

## 2. Source-kit

Source-kit — отдельный архив для кастомизации. В нём есть исходники сервера,
клиентский ресурс, SDK, миграции, шаблоны и инструменты сборки. В нём намеренно
нет `launcher/`, `web/`, внутренних закрытых компонентов и бинарников alt:V.

Владелец source-kit сначала передаёт свой разрешённый backup/runtime alt:V и
импортирует его безопасной командой:

```bash
python scripts/prepare_engine.py \
  --source /path/to/authorized-runtime.zip \
  --dest engine --platform all
```

Допустимы каталог, ZIP или `tar.gz`. Скрипт отвергает traversal, symlink и
специальные файлы, копирует только требуемые серверные бинарники и `data/*.bin`,
а затем создаёт `engine/ENGINE-MANIFEST.json` с SHA-256. Перед передачей нужно
самостоятельно подтвердить права на сторонний runtime.

Далее source-kit собирается в два обычных runtime-архива:

```bash
python scripts/pack_server.py --os all
```

Windows-native adapter и реальный двухклиентский тест GTA выполняются отдельно
на Windows. Команда проверки выпуска:

```powershell
.\scripts\verify-windows-native.ps1 `
  -Profile legacy-3889 `
  -GtaDir 'C:\Games\GTAV' `
  -NativeAdapterPath 'C:\FloVMP\native\flovmp-legacy-native-3889.dll' `
  -AdapterManifestPath 'C:\FloVMP\native\flovmp-legacy-native-3889.dll.json' `
  -E2EReport 'C:\FloVMP\reports\legacy-3889-e2e.json' `
  -WriteReport 'C:\FloVMP\reports\legacy-3889-native.json'
```

Скрипт проверяет версию и SHA-256 `GTA5.exe`, `update.rpf`, `update2.rpf`,
наличие native adapter, его sidecar-манифест (profile/id/version/binary/SHA-256)
и все пять E2E-гейтов. При любой ошибке код возврата
ненулевой: такой пакет нельзя помечать как готовый к продаже.

Минимальный sidecar-файл рядом с DLL выглядит так:

```json
{
  "profile": "legacy-3889",
  "id": "flovmp-legacy-native-3889",
  "version": "1.0.0",
  "binary": "flovmp-legacy-native-3889.dll",
  "sha256": "SHA256_БИНАРНИКА_64_СИМВОЛАМИ"
}
```

## Структура установленной папки

```text
FloVMP/
├─ server/                 # flovmp-server(.exe), modules/, resources/, data/
├─ config/                 # flovmp.env и локальные настройки
├─ sql/migrations/         # миграции БД
├─ sdk/ref/                # DLL для кастомного gamemode
├─ gamemode/               # папка владельца под свой мод
├─ license.flv             # выданный подписанный файл (не входит в архив)
├─ license.lease           # короткий online lease (создаётся runtime)
├─ backups/                # резервные копии установщика
├─ install.ps1|install.sh  # повторная установка/обновление
└─ manifest.json           # контроль целостности поставки
```

Игровой клиент подключается к серверу через совместимый внешний runtime и
native adapter. Отсутствие launcher/web в поставке является намеренным и не
мешает серверу работать локально или на VDS.

Для последующих обновлений используется единый CLI:

```bash
python scripts/flo_update.py --product runtime-license --root /opt/flovmp \
  --package-url https://HOST/flovmp-server-VERSION-linux.tar.gz \
  --sha256 SHA256_АРХИВА --license-key FLV-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX
```

Онлайн-кабинет и desktop-приложение в будущем будут выдавать тот же подписанный
манифест и запускать тот же проверяемый workflow. Архитектурный план находится
в `docs/ecosystem-control-plane-plan.md`.
