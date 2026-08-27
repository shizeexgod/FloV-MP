# Фаза 3 — каркас авторизации (задача #12)

Обновлено: 2026-08-28
Статус: **логика + проводка готовы, сборка и boot-тест зелёные, 12 тестов. Визуальный поток NUI не проверен (нужен живой клиент).**

Перенос паттерна `az_auth` Florida V на C#/alt:V. Первый вертикальный срез
Фазы 3: сервер C# ↔ клиент JS ↔ NUI HTML.

## Поток

```
connect            сервер НЕ спавнит игрока → Emit flovmp:auth:show
  клиент           открывает NUI (login/register), курсор, статичная камера,
                   toggleGameControls(false)
  NUI → клиент     flovmp:auth:submit {mode, user, pass}
  клиент → сервер  flovmp:auth:login | flovmp:auth:register {user, pass}
  сервер           AuthService.Login/Register → Emit flovmp:auth:result {ok, msg}
  сервер (при ok)  _authed[player.Id] = account
                   Emit flovmp:auth:hide → PlayerLifecycle.SpawnAuthed(player, accountId)
  клиент           закрывает NUI, возвращает управление и камеру
```

## Код

### Чистая логика — `server/src/FloVMP.Core/Auth/` (без alt:V, тестируется)

| Файл | Роль |
|---|---|
| `PasswordHasher.cs` | PBKDF2-SHA256, 120k итераций, формат `pbkdf2$sha256$iters$salt$hash`, `FixedTimeEquals`. Пароли в открытом виде не хранятся/не логируются |
| `Account.cs` | модель + правила `IsValidUsername` (3–20, `[A-Za-z0-9_]`) / `IsValidPassword` (6–100) |
| `IAccountStore.cs` / `JsonAccountStore.cs` | хранилище; JSON-файл, потокобезопасно (`lock`), durable-запись (tmp + `File.Replace`). Позже — БД по той же схеме |
| `AuthService.cs` | `Register` / `Login` → `AuthResult{Outcome, Message, Account?}`; троттлинг подбора (N попыток за окно на ключ = ip игрока) |

### Проводка alt:V — `server/src/FloVMP.Gamemode/`

| Файл | Роль |
|---|---|
| `Systems/Auth/AuthSystem.cs` | подписка на `OnPlayerConnect` (не спавнит, шлёт `auth:show`), `Alt.OnClient<string,string>("flovmp:auth:login"/"register")`, вызов `AuthService`, `_authed` map по `player.Id` (uint), при успехе → колбэк спавна |
| `Systems/PlayerLifecycle.cs` | спавн **вынесен** из connect в `SpawnAuthed(player, accountId)` — вызывается только после входа |
| `GamemodeResource.cs` | создаёт `PlayerLifecycle` + `AuthSystem` (стор → `<serverRoot>\flovmp-data\accounts.json`), связывает их колбэком |

### Клиент — `client/resources/flovmp-client/`

| Файл | Роль |
|---|---|
| `client/index.js` | `openAuth()` / `closeAuth()`: `alt.WebView('http://resource/client/html/auth/index.html')`, курсор, `toggleGameControls`, статичная камера; мост NUI↔сервер |
| `client/html/auth/index.html` | экран входа/регистрации, self-contained, тёмная тема + `#ff3d8a`, вкладки Вход/Регистрация, поле сообщения об ошибке |

`resource.toml`: `client-files = [ "client/**/*" ]` (рекурсивно, ради `html/`).

## Тесты — `server/tests/FloVMP.Core.Tests/` (xUnit, 12)

- `PasswordHasherTests` (3): round-trip, соль различается, отказ на кривом формате.
- `AuthServiceTests` (9): register→login; отказ на плохих имени/пароле;
  дубль (case-insensitive); неизвестный юзер / неверный пароль; **rate-limit
  после N попыток и разблок по истечении окна**; успешный вход сбрасывает
  счётчик; учётки переживают пересоздание стора (персист).

## boot-тест (2026-08-28)

Сервер поднимается с новым геймодом:
`[C#] core: auth store -> ...\flovmp-data\accounts.json`,
`[C#] core: systems attached`, `[C#] core: server fully started`. Оба
ресурса грузятся, ошибок нет (кроме известного `curl code 6`).

## Не проверено / дальше

- Визуальный поток NUI (нужен живой клиент — см. `docs/live-test-guide.md`).
- Персонаж/скин, привязка позиции к аккаунту (сейчас всегда Legion Square,
  `FreemodeMale01`).
- БД вместо JSON.
- Следующие срезы Фазы 3: HUD, инвентарь.
