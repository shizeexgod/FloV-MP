# Живой тест FloV:MP (нужен GTA V + владелец за ПК)

Обновлено: 2026-08-28
Цель: два игрока (владелец + заказчик, 2 разных ПК) заходят на сервер и видят друг друга.

Всё, что можно было сделать без живого клиента, сделано. Этот шаг —
единственный, который не автоматизируется.

## Предпосылки на машине с сервером

- .NET SDK + .NET 8 Desktop Runtime (стоит).
- Бэкап alt:V в `C:\ViMP backup\backup-altv` (для пересборки).
- `runtime/client/` уже собран (85 файлов + manifest.json, 318 МБ) —
  см. `docs/client-recovery-plan.md`. Пересобрать при нужде:
  ```
  powershell -File scripts/fetch-cef.ps1
  powershell -File scripts/assemble-client-core.ps1 -FillFrom runtime\client-fill
  powershell -File scripts/make-manifest.ps1 -SourceDir runtime\client -OutFile runtime\client\manifest.json -Version rec-1 -BaseUrl runtime\client
  ```

## Шаг 1. Поднять сервер

```powershell
powershell -File C:\FloV-MP\scripts\assemble-runtime.ps1
powershell -File C:\FloV-MP\scripts\run-server.ps1
```

Ждём в консоли `[C#] [FloV:MP] core: server fully started` и
`Server started in debug mode`. Оставить окно открытым.

Порт **7788** (UDP+TCP). Для заказчика с другого ПК — пробросить/открыть
7788 на роутере/файрволе, дать ему внешний IP (или оба через VPN/LAN).

## Шаг 2. Лаунчер

```powershell
dotnet build C:\FloV-MP\launcher\FloVMP.Launcher.slnx -c Debug
C:\FloV-MP\launcher\src\FloVMP.Launcher\bin\Debug\net8.0-windows\FloVMP.Launcher.exe
```

В окне:
1. **Сервер**: `127.0.0.1` / `7788` (заказчик — внешний IP владельца).
   Кнопка **Проверить** → должно стать «онлайн».
2. **Никнейм**: любой (у владельца и заказчика — разные).
3. **GTA V**: путь подхватится сам (Epic:
   `C:\Program Files\9d2d0eb64d5c44529cece33fe2a46482`). Внизу — «GTA5.exe найден».
4. **Ядро клиента alt:V**: указать `C:\FloV-MP\runtime\client`.
   Внизу — «ядро клиента: файлы на месте».
   *Либо* заполнить «Обновление ядра по манифесту» =
   `C:\FloV-MP\runtime\client\manifest.json` и нажать «Проверить / Обновить» —
   тогда ядро скачается/сверится в `%LOCALAPPDATA%\FloVMP\client`.
5. Галка «Разрешить несколько клиентов» — можно оставить.
6. **ИГРАТЬ**.

Лаунчер соберёт и запустит:
```
altv.exe -connecturl "altv://connect/127.0.0.1:7788?nickname=<ник>" -noupdate -branch release -skipprocesscheck -skipprocessconfirmation
```

## Что проверяем

- [ ] `altv.exe` стартует, не падает на проверке обновления (флаг `-noupdate`).
- [ ] Клиент цепляет `GTA5.exe`, грузится в игру (loading screen alt:V).
- [ ] Персонаж спавнится на Legion Square (центр Los Santos).
- [ ] В F8-консоли клиента: `[FloV:MP] client: ресурс flovmp-client загружен`,
      `connectionComplete`, `welcome "<ник>"`.
- [ ] В логе сервера (`runtime/server/server.log`): `connect: <ник> ... -> spawn #0`.
- [ ] **Второй игрок** заходит так же → оба видят педов друг друга, движение
      синхронно.

## Если не заводится — вероятные места

| Симптом | Причина / что делать |
|---|---|
| `altv.exe` ругается на версию/обновление, не пускает | `-noupdate` не сработал. Вариант: инжектить `altv-client.dll` напрямую, минуя `altv.exe` (отдельная задача). |
| Чёрный экран / краш CEF на старте | ванильные CEF-ресурсы не подошли к пропатченному `libce2.dll`. Нужен полный официальный клиент alt:V 16.4.x. |
| Краш на шрифтах / странный UI | `freetype.dll` от Majestic (2.13.3, размер отличается). Заменить на официальный alt:V-шный. |
| Краш на ICU / локали | `icudtl_v8.dat` — сейчас это копия `icudtl.dat` (догадка). Нужен настоящий. |
| Игра запускается, но alt:V не хукает | `legacy.dll` из форка Majestic не подошёл. Нужен стоковый alt:V `legacy.dll`. |
| Клиент подключился, но педов не видно | серверная синхронизация: проверить `server.log` на `spawn`, проверить dimension. |
| Заказчик не может подключиться | 7788 закрыт на файрволе/роутере; проверить `announce=false` не мешает (не мешает). |

Любой из этих исходов — конкретный маленький файл найти или один хук
написать, а не «всё заново».

## Отчёт

После теста — записать в `agent_state.md` (задача #4): что заработало, где
упал, приложить хвост `server.log` и скрин F8-консоли клиента.
