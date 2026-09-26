// Клиентский код сервера — пример. Как client_packages в RAGE:MP.
//
// Чтобы включить: скопируйте эту папку в server/client_packages и перезапустите
// сервер (или командой reloadclient в консоли сервера). Игроки скачают её при
// входе и запустят index.js.
//
// Здесь оба способа сделать интерфейс:
//   - страница HTML/CSS/JS (ui/index.html) — деньги и панель по F2;
//   - нативы GTA (hud/speedometer.js) — спидометр, mp.game.<пространство>.<функция>.

const speedometer = require('./hud/speedometer');
const config = require('./config.json');

// Страница из этой же папки: package://ui/index.html. Можно и https://…
const ui = mp.browsers.new('package://ui/index.html');
ui.inputEnabled = false; // HUD виден, но до открытия меню ввод проходит в игру.
let panelOpen = false;

mp.events.add('browserDomReady', (browser) => {
    if (browser === ui) ui.call('hud:player', mp.players.local.name);
});
mp.events.add('browserRestored', (browser) => {
    // CEF host автоматически поднялся после падения: вернуть состояние HUD.
    if (browser === ui) {
        ui.call('hud:player', mp.players.local.name);
        if (panelOpen) ui.focus();
    }
});

// Сервер присылает деньги: Alt.Emit("flovmp:client:call", id, "hud:money", "[5000]").
mp.events.add('hud:money', (value) => ui.call('hud:money', Number(value) || 0));

// Панель: курсор для страницы, управление персонажем на это время выключено.
function togglePanel(open) {
    panelOpen = open;
    ui.inputEnabled = open;
    if (open) ui.focus(); else ui.blur();
    ui.call('panel:toggle', open);
    mp.gui.cursor.show(open, open);
}
mp.keys.bind(0x71, true, () => togglePanel(!panelOpen));        // F2
mp.events.add('panel:close', () => togglePanel(false));          // из страницы: mp.trigger
mp.events.add('panel:submit', (text) => {
    mp.events.callRemote('hud:report', String(text).slice(0, 200));
    togglePanel(false);
});

mp.events.add('playerSpawn', () => {
    mp.gui.chat.push(`Привет, ${mp.players.local.name}! F2 — панель на HTML из client_packages.`);
});

mp.events.add('render', () => speedometer.draw(config));

// F5 — событие на сервер: Alt.OnServer("flovmp:client:event", (id, name, json) => ...).
mp.keys.bind(0x74, true, () => {
    mp.events.callRemote('hud:f5', mp.players.local.position);
});

// Сообщить серверу, что HUD готов: в шаблоне gamemode он в ответ присылает деньги.
mp.events.callRemote('hud:ready');
