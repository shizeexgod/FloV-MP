// Клиентский код сервера — пример. Как client_packages в RAGE:MP.
//
// Чтобы включить: скопируйте эту папку в server/client_packages и перезапустите
// сервер (или командой reloadclient в консоли сервера). Игроки скачают её при
// входе и запустят index.js.
//
// Здесь: свой HUD (деньги, спидометр) и клавиша, которая отправляет событие
// на сервер. Всё рисуется нативами GTA — mp.game.<пространство>.<функция>.

const speedometer = require('./hud/speedometer');
const config = require('./config.json');

let money = 0;

// Сервер присылает деньги: Alt.Emit("flovmp:client:call", id, "hud:money", "[5000]").
mp.events.add('hud:money', (value) => {
    money = Number(value) || 0;
});

mp.events.add('playerSpawn', () => {
    mp.gui.chat.push(`Привет, ${mp.players.local.name}! HUD из client_packages загружен.`);
});

mp.events.add('render', () => {
    // Деньги в правом верхнем углу, цвет — из config.json.
    const digits = String(Math.trunc(money)).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    mp.game.graphics.drawText(`$${digits}`, [0.93, 0.04], {
        font: 7,
        color: config.accent,
        scale: [0.6, 0.6],
        outline: true,
    });
    speedometer.draw(config);
});

// F5 — событие на сервер: Alt.OnServer("flovmp:client:event", (id, name, json) => ...).
mp.keys.bind(0x74, true, () => {
    mp.events.callRemote('hud:f5', mp.players.local.position);
});

// Сообщить серверу, что HUD готов: в шаблоне gamemode он в ответ присылает деньги.
mp.events.callRemote('hud:ready');
