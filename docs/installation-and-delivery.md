# Установка и состав поставки FloV:MP

Документ описывает два разных товара. Они не объединяются в одну команду и не
требуют сайта или лаунчера.

## 1. Runtime-лицензия

Клиент получает готовый серверный архив и ключ лицензии. В архиве нет GTA V,
клиента игрока, лаунчера или `web/`; это серверный runtime с шаблонами ресурсов.

### Windows (PowerShell)

Клиент создаёт пустую папку, открывает PowerShell в ней и запускает команду,
которую продавец выдаёт вместе с URL и SHA-256 конкретного ZIP-пакета:

```powershell
& ([scriptblock]::Create((irm https://HOST/bootstrap-windows.ps1))) `
  -PackageUrl 'https://HOST/flovmp-server-VERSION-windows.zip' `
  -Sha256 '64-символьный-SHA256' `
  -LicenseKey 'FLV-XXXX-XXXX-XXXX' `
  -InstallDir "$PWD\FloVMP"
```

`bootstrap-windows.ps1` скачивает ZIP во временную папку, проверяет SHA-256,
проверяет манифест и запускает штатный установщик. Ключ записывается только в
`config/flovmp.env`; его не нужно добавлять в командную строку запуска сервера.
Для уже скачанного ZIP можно выполнить `install.cmd` или `install.ps1` напрямую.

### Linux/VDS

Для VDS используется тот же принцип, но архив — `tar.gz`:

```bash
mkdir -p ~/flovmp && cd ~/flovmp
curl -fsSL https://HOST/install.sh -o install.sh
chmod +x install.sh
sudo ./install.sh \
  --package-url https://HOST/flovmp-server-VERSION-linux.tar.gz \
  --sha256 64-символьный-SHA256 \
  --key FLV-XXXX-XXXX-XXXX
```

Установщик ставит runtime в `/opt/flovmp`, создаёт отдельного системного
пользователя, применяет миграции и unit systemd. Пользовательские
`config/flovmp.env`, ресурсы и данные при обновлении сохраняются.

После установки владелец настраивает `server/server.toml` и открывает игровые
порты, указанные в конфигурации (по умолчанию TCP+UDP `7788` и UDP `7895`).

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
  -E2EReport 'C:\FloVMP\reports\legacy-3889-e2e.json' `
  -WriteReport 'C:\FloVMP\reports\legacy-3889-native.json'
```

Скрипт проверяет версию и SHA-256 `GTA5.exe`, `update.rpf`, `update2.rpf`,
наличие native adapter и все пять E2E-гейтов. При любой ошибке код возврата
ненулевой: такой пакет нельзя помечать как готовый к продаже.

## Структура установленной папки

```text
FloVMP/
├─ server/                 # flovmp-server(.exe), modules/, resources/, data/
├─ config/                 # flovmp.env и локальные настройки
├─ sql/migrations/         # миграции БД
├─ sdk/ref/                # DLL для кастомного gamemode
├─ gamemode/               # папка владельца под свой мод
├─ backups/                # резервные копии установщика
├─ install.ps1|install.sh  # повторная установка/обновление
└─ manifest.json           # контроль целостности поставки
```

Игровой клиент подключается к серверу через совместимый внешний runtime и
native adapter. Отсутствие launcher/web в поставке является намеренным и не
мешает серверу работать локально или на VDS.
