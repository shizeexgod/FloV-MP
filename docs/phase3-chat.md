# Фаза 3 — чат (задача #16)

Обновлено: 2026-08-28
Статус: **логика + проводка готовы, 37 server-тестов (14 по чату), boot-тест зелёный. Визуально не проверен.**

Четвёртый срез Фазы 3. Сервер принимает сырой текст, чистит, ограничивает
частоту, разбирает команды, рассылает.

## Поток

```
клиент: T           → startTyping(): focus чат-NUI, toggleGameControls(false),
                       NUI открывает поле ввода
NUI: Enter/Esc      → flovmp:chat:say {text} (только Enter с текстом) +
                       flovmp:chat:done → клиент unfocus + controls обратно
клиент → сервер     → flovmp:chat:say {text}
сервер              → ChatSanitizer.Clean → rate-limit → команда? →
                       рассылка flovmp:chat:msg {kind, author, text}
```

`kind`: `player` | `system` | `me`.

## Код

### Чистая логика — `server/src/FloVMP.Core/Chat/ChatSanitizer.cs`

- `Clean(raw)` — trim, срез control-символов, схлопывание пробелов, лимит
  `MaxLength = 256`; `null` если пусто.
- `IsCommand(text)` — одиночный ведущий `/` (не `//`).
- `ParseCommand("/cmd a b")` → `("cmd", ["a","b"])`, имя в lowercase.

### Проводка alt:V — `server/src/FloVMP.Gamemode/Systems/Chat/ChatSystem.cs`

- `Alt.OnClient<string>("flovmp:chat:say")`.
- Rate-limit: не более 4 сообщений за 3 с на игрока (`ConcurrentDictionary`).
- `//текст` → отправить как обычное сообщение с ведущим `/`.
- Команды: `/help`, `/me <действие>`, `/online`, `/pos`.
- `Broadcast(text)` / `SendSystem(player, text)` — системные сообщения
  (вход/выход игрока, приветствие).
- Рассылка только вошедшим (`_accountOf(p) is not null`).

`GamemodeResource`: `_chat = new ChatSystem(p => _auth.AccountOf(p))`;
в `OnPlayerAuthed` — `_chat.OnPlayerAuthed(player, account)`.

### Клиент — `client/resources/flovmp-client/`

| Файл | Роль |
|---|---|
| `client/html/chat/index.html` | лог сверх-слева (до 40 строк, затухает через 8 с), поле ввода по команде; экранирование HTML; типы строк player/system/me. `#ff3d8a` для ников |
| `client/index.js` | `openChat`/`closeChat` (постоянный WebView с HUD), `startTyping` по `keyup` key `84` (T); `flovmp:chat:done` возвращает фокус/управление |

## Тесты — `server/tests/FloVMP.Core.Tests/ChatSanitizerTests.cs` (14)

Clean: trim+схлопывание, срез control, null на пустом (+Theory), лимит
длины. IsCommand: `/help`/`/me` → true, `//literal`/`hi`/`/` → false.
ParseCommand: разбор имени и аргументов, lowercase имени.

## Не сделано / дальше

- Локальный чат по радиусу (сейчас глобальный).
- Цвета/роли, история для новоподключившихся.
- Античит на флуд одинаковыми сообщениями.
- Больше команд (`/give`, `/tp`, ...), права.
