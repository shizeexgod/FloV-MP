# Архитектура гибридного рантайма FloV:MP: C#, Node.js и NUI/CEF

## 1. Поддержка серверных языков: C# (.NET 8) и Node.js (JavaScript / TypeScript)

Движок FloV:MP поддерживает **одновременную работу ресурсов на двух языках** на одном сервере через модули:
- `csharp-module` (`coreclr-module`) — для высокопроизводительного C# (.NET 8).
- `js-module` (`v8` / `libnode`) — для серверного JavaScript / Node.js.

### 1.1. Конфигурация в `server.toml`
В секции `modules` включаются оба модуля:
```toml
modules = [
    "csharp-module",
    "js-module"
]
```

### 1.2. Структура ресурсов
В папке `resources/` сервера могут находиться как C# ресурсы, так и Node.js ресурсы:
- **C# ресурс:**
  `resources/my-csharp-gamemode/resource.toml`:
  ```toml
  type = "csharp"
  main = "MyGamemode.dll"
  ```
- **Node.js (JavaScript) ресурс:**
  `resources/my-nodejs-resource/resource.toml`:
  ```toml
  type = "js"
  main = "index.js"
  client-main = "client.js"
  client-files = ["client.js"]
  ```

### 1.3. Кросс-коммуникация между C# и Node.js
C# и Node.js могут обмениваться данными через встроенную шину событий alt:V:
- **Отправка из C# в Node.js:**
  ```csharp
  Alt.Emit("eventFromCSharp", "hello from .NET", 123);
  ```
- **Получение в Node.js:**
  ```javascript
  import * as alt from 'alt-server';
  alt.on('eventFromCSharp', (msg, num) => {
      alt.log(`[JS Received]: ${msg}, ${num}`);
  });
  ```
- **И наоборот:** Node.js вызывает `alt.emit('eventFromJS', ...)`, а C# ловит через `Alt.On("eventFromJS", ...)`.

---

## 2. NUI vs CEF: Разъяснение концепции

В сообществе GTA V часто путают термины:
- **NUI (Natural User Interface)** — термин из экосистемы FiveM, обозначающий внутриигровой браузер для отрисовки интерфейсов поверх игры.
- **CEF (Chromium Embedded Framework)** — общепринятая мировая технология встраиваемого браузера Chromium, используемая в FiveM, RageMP и alt:V.

### **Технический факт:**
**NUI в FiveM и WebView в alt:V/FloV:MP — это буквально один и тот же CEF (Chromium)!**
Все веб-технологии работают на 100%:
- HTML5, CSS3, SCSS, Flexbox, CSS Grid
- TailwindCSS, PostCSS
- React, Vue, Svelte, SolidJS, Angular
- Аппаратное GPU-ускорение (WebGL, Canvas, 3D CSS)
- Аудио и видео форматы (`.mp3`, `.ogg`, `.webm`, `.mp4`)

### 2.1. Передача данных NUI ↔ Клиент ↔ Сервер
```
+---------------+           +---------------+           +---------------+
|   NUI (CEF)   |  alt.emit |  Client (JS)  | alt.emit  |  Server (C#)  |
|  (HTML / JS)  | --------> | (client/index)| --------> | (or Node.js)  |
|               | <-------- |               | <-------- |               |
|               |  view.emit|               | player.Emit|               |
+---------------+           +---------------+           +---------------+
```
1. **NUI в Клиент:** В JS интерфейса: `if ('alt' in window) alt.emit('ui:buttonClicked', payload);`
2. **Клиент в NUI:** В клиентском JS: `myWebView.emit('nui:updateState', state);`
3. **Клиент на Сервер:** `alt.emitServer('net:buyItem', itemId);`
4. **Сервер Клиенту:** `player.Emit('chat:addMessage', 'Покупка успешна');`
