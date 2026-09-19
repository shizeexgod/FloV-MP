# Контракт native-адаптера FloV:MP

Обновлено: 2026-09-18

FloV:MP разделяет серверный движок и версионно-зависимый native-слой,
который запускает GTA V и подключает клиентский runtime. Сервер, C#-гамемод,
NUI и лаунчер не могут заменить этот слой: они начинают работать после
успешного старта игрового процесса.

## Профиль

Каждый поддерживаемый билд хранится отдельно в `runtime/compat/<profile>/`.
Профиль обязан содержать:

- издание и платформу (`legacy`/`enhanced`, `egs`/`steam`/`rgl`);
- версию и SHA-256 игрового exe;
- SHA-256 всех RPF, которые участвуют в version-dependent проверке;
- версию native-клиента и адаптера;
- статус поддержки и результаты E2E-гейтов.

Текущий Legacy-профиль владельца: `legacy-3889-epic`, GTA V Legacy
`1.0.3889.0`, Epic Games Store. Его файлы проверяются:

```powershell
powershell -ExecutionPolicy Bypass -File C:\FloV-MP\scripts\verify-legacy-3889.ps1
```

Enhanced ведётся независимо: `runtime/compat/enhanced-1158` для
`GTA5_Enhanced.exe` `1.0.1158.13`. До захвата чистых Windows-хэшей этот
профиль имеет `fingerprintStatus=pending-windows-capture`, а требуемый адаптер
именуется `flovmp-enhanced-native-1158`. Legacy fingerprint/RPF и Enhanced
fingerprint/RPF нельзя смешивать.

Проверка Enhanced на Windows:

```powershell
powershell -ExecutionPolicy Bypass -File C:\FloV-MP\scripts\verify-enhanced-1158.ps1 `
  -GtaDir 'C:\Program Files\Rockstar Games\Grand Theft Auto V' `
  -Capture C:\FloV-MP\runtime\compat\enhanced-1158\windows-capture.json
```

## Правила запуска

1. Лаунчер сначала определяет профиль и проверяет fingerprint.
2. При несовпадении exe/RPF запуск блокируется с диагностическим кодом.
3. При отсутствии E2E-подтверждённого адаптера запуск блокируется.
4. FloV:MP не подмешивает Steam-файлы в Epic-установку и не раздаёт старый
   `GTA5.exe` через backup CDN.
5. `--allow-unsupported` допускается только для локальной диагностики и не
   является режимом поддержки или релизным путём.

## E2E-гейты

Профиль можно перевести в `supported` только после подтверждения всех пунктов:

1. игровое окно GTA открывается и остаётся живым;
2. native-клиент подключается к FloV:MP;
3. сервер принимает клиента и загружает `flovmp-client`;
4. игрок появляется в игровом мире;
5. два клиента видят друг друга и синхронно двигаются.

Открытие `GTA5.exe` само по себе не считается поддержкой.
