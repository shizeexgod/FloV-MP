/**
 * FloV:MP — клиентский движок мультиплеера.
 * Сервер: Держава Онлайн.
 */

import * as alt from 'alt-client';
import * as native from 'natives';

alt.log('[FloV:MP] Клиентский модуль FloV:MP загружен');

let authView = null;
let authCamera = null;
let inGame = false;
let chatView = null;
let chatTyping = false;
let settingsView = null;
let hudView = null;
let inventoryView = null;

// --- NoClip (Полет на F4 с невидимостью) ---------------------------------
let noClip = false;
let noClipPos = null;

function toggleNoClip() {
    if (!inGame || authView || chatTyping) return;
    const player = alt.Player.local;
    if (!player || !player.valid) return;

    noClip = !noClip;

    if (noClip) {
        noClipPos = { ...player.pos };
        native.freezeEntityPosition(player.scriptID, true);
        native.setEntityCollision(player.scriptID, false, false);
        native.setEntityInvincible(player.scriptID, true);
        native.setEntityVisible(player.scriptID, false, 0);
        native.setEntityAlpha(player.scriptID, 0, false);
        alt.log('[FloV:MP] NoClip включен (инвиз)');
    } else {
        native.freezeEntityPosition(player.scriptID, false);
        native.setEntityCollision(player.scriptID, true, true);
        native.setEntityInvincible(player.scriptID, false);
        native.setEntityVisible(player.scriptID, true, 0);
        native.resetEntityAlpha(player.scriptID);
        alt.log('[FloV:MP] NoClip выключен (видимый)');
    }
}

// --- Постоянный игровой цикл (Каждый тик) --------------------------------
alt.everyTick(() => {
    // 1) Полное отключение стандартного трафика и NPC
    native.setPedDensityMultiplierThisFrame(0.0);
    native.setScenarioPedDensityMultiplierThisFrame(0.0, 0.0);
    native.setVehicleDensityMultiplierThisFrame(0.0);
    native.setRandomVehicleDensityMultiplierThisFrame(0.0);
    native.setParkedVehicleDensityMultiplierThisFrame(0.0);

    // 2) Отключение служб полиции / скорой / спавна копов
    for (let i = 1; i <= 15; i++) {
        native.enableDispatchService(i, false);
    }
    native.setCreateRandomCops(false);
    native.setCreateRandomCopsNotOnScenarios(false);
    native.setCreateRandomCopsOnScenarios(false);

    const player = alt.Player.local;
    if (player && player.valid) {
        native.setPlayerWantedLevel(player.scriptID, 0, false);
        native.setPlayerWantedLevelNow(player.scriptID, false);
    }

    // 3) Блокировка колеса выбора оружия на Tab
    native.disableControlAction(0, 37, true); // INPUT_SELECT_WEAPON (TAB)
    native.disableControlAction(0, 157, true); // 1
    native.disableControlAction(0, 158, true); // 2
    native.disableControlAction(0, 159, true); // 3
    native.disableControlAction(0, 160, true); // 4
    native.disableControlAction(0, 161, true); // 5
    native.disableControlAction(0, 162, true); // 6
    native.disableControlAction(0, 163, true); // 7
    native.disableControlAction(0, 164, true); // 8
    native.disableControlAction(0, 165, true); // 9

    // 4) Скрытие стандартных элементов интерфейса GTA
    native.hideHudComponentThisFrame(6);  // Vehicle Name
    native.hideHudComponentThisFrame(7);  // Area Name
    native.hideHudComponentThisFrame(8);  // Vehicle Class
    native.hideHudComponentThisFrame(9);  // Street Name
    native.hideHudComponentThisFrame(19); // Weapon Wheel
    native.hideHudComponentThisFrame(20); // Weapon Wheel Stats
    native.hideHudComponentThisFrame(22); // Weapons HUD

    // 5) Блокировка вызова Social Club оверлея на клавишу HOME
    native.disableControlAction(0, 212, true); // INPUT_FRONTEND_SOCIAL_CLUB_HOME
    native.disableControlAction(0, 213, true); // INPUT_FRONTEND_SOCIAL_CLUB_SECONDARY

    // 6) Логика NoClip
    if (noClip && player && player.valid) {
        native.disableControlAction(0, 30, true); // A-D
        native.disableControlAction(0, 31, true); // W-S
        native.disableControlAction(0, 21, true); // Shift
        native.disableControlAction(0, 22, true); // Space
        native.disableControlAction(0, 36, true); // Ctrl
        native.disableControlAction(0, 44, true); // Q
        native.disableControlAction(0, 24, true); // Attack
        native.disableControlAction(0, 25, true); // Aim

        const camRot = native.getGameplayCamRot(2);
        const radZ = camRot.z * (Math.PI / 180.0);
        const radX = camRot.x * (Math.PI / 180.0);
        const cosX = Math.abs(Math.cos(radX));

        const forward = {
            x: -Math.sin(radZ) * cosX,
            y: Math.cos(radZ) * cosX,
            z: Math.sin(radX)
        };

        const right = {
            x: Math.cos(radZ),
            y: Math.sin(radZ),
            z: 0.0
        };

        let speed = 0.9;
        if (native.isDisabledControlPressed(0, 21) || native.isControlPressed(0, 21)) {
            speed = 3.8; // Shift (Fast)
        } else if (native.isControlPressed(0, 19)) {
            speed = 0.15; // Alt (Slow)
        }

        // W / S
        if (native.isDisabledControlPressed(0, 32) || native.isControlPressed(0, 32)) {
            noClipPos.x += forward.x * speed;
            noClipPos.y += forward.y * speed;
            noClipPos.z += forward.z * speed;
        }
        if (native.isDisabledControlPressed(0, 33) || native.isControlPressed(0, 33)) {
            noClipPos.x -= forward.x * speed;
            noClipPos.y -= forward.y * speed;
            noClipPos.z -= forward.z * speed;
        }

        // A / D
        if (native.isDisabledControlPressed(0, 34) || native.isControlPressed(0, 34)) {
            noClipPos.x -= right.x * speed;
            noClipPos.y -= right.y * speed;
        }
        if (native.isDisabledControlPressed(0, 35) || native.isControlPressed(0, 35)) {
            noClipPos.x += right.x * speed;
            noClipPos.y += right.y * speed;
        }

        // Space / Ctrl
        if (native.isDisabledControlPressed(0, 22) || native.isControlPressed(0, 22)) {
            noClipPos.z += speed;
        }
        if (native.isDisabledControlPressed(0, 36) || native.isControlPressed(0, 36)) {
            noClipPos.z -= speed;
        }

        native.setEntityCoordsNoOffset(player.scriptID, noClipPos.x, noClipPos.y, noClipPos.z, false, false, false);
        native.setEntityHeading(player.scriptID, camRot.z);
    }
});

// --- Авторизация ---------------------------------------------------------
function openAuth() {
    if (authView) return;
    try {
        // Камера с видом на Красную площадь / Кремль (Москва)
        authCamera = native.createCamWithParams(
            'DEFAULT_SCRIPTED_CAMERA',
            -220.0, -1080.0, 65.0, -15.0, 0.0, 340.0, 60.0, false, 2);
        native.setCamActive(authCamera, true);
        native.renderScriptCams(true, false, 0, true, false, 0);
    } catch (err) {
        alt.log('[FloV:MP] Камера авторизации: ' + err);
    }

    authView = new alt.WebView('http://resource/client/html/auth/index.html');
    authView.focus();
    alt.showCursor(true);
    alt.toggleGameControls(false);

    authView.on('flovmp:auth:submit', (mode, user, pass) => {
        const evt = mode === 'reg' ? 'flovmp:auth:register' : 'flovmp:auth:login';
        alt.emitServer(evt, String(user), String(pass));
    });
}

function closeAuth() {
    if (authView) {
        authView.destroy();
        authView = null;
    }
    try { alt.showCursor(false); } catch (e) { }
    alt.toggleGameControls(true);

    try {
        native.renderScriptCams(false, false, 0, true, false, 0);
        if (authCamera) {
            native.destroyCam(authCamera, false);
            authCamera = null;
        }
    } catch (err) {
        alt.log('[FloV:MP] Сброс камеры: ' + err);
    }
}

// --- Чат -----------------------------------------------------------------
function openChat() {
    if (chatView) return;
    chatView = new alt.WebView('http://resource/client/html/chat/index.html');
    chatView.on('flovmp:chat:say', (text) => alt.emitServer('flovmp:chat:say', String(text)));
    chatView.on('flovmp:chat:done', () => {
        chatTyping = false;
        try { chatView.unfocus(); } catch (e) { }
        alt.toggleGameControls(true);
    });
}

function closeChat() {
    if (!chatView) return;
    chatView.destroy();
    chatView = null;
    chatTyping = false;
}

function startTyping() {
    if (!chatView || chatTyping || !inGame) return;
    chatTyping = true;
    chatView.focus();
    alt.toggleGameControls(false);
    chatView.emit('flovmp:chat:openinput');
}

// --- Настройки (акцентный цвет и т.д.) ------------------------------------
function openSettings() {
    if (settingsView || !inGame || authView) return;
    settingsView = new alt.WebView('http://resource/client/html/settings/index.html');
    settingsView.focus();
    alt.showCursor(true);
    alt.toggleGameControls(false);

    settingsView.on('flovmp:settings:close', closeSettings);
    settingsView.on('flovmp:settings:accent', (colorId) => {
        alt.log(`[FloV:MP] Акцентный цвет изменён на: ${colorId}`);
        if (hudView) hudView.emit('flovmp:settings:accent', colorId);
        if (chatView) chatView.emit('flovmp:settings:accent', colorId);
        if (inventoryView) inventoryView.emit('flovmp:settings:accent', colorId);
        if (consoleView) consoleView.emit('flovmp:settings:accent', colorId);
    });
}

function closeSettings() {
    if (!settingsView) return;
    settingsView.destroy();
    settingsView = null;
    try { alt.showCursor(false); } catch (e) { }
    alt.toggleGameControls(true);
}

// --- Игровой HUD (Здоровье, Броня, Деньги, Онлайн) -----------------------
function openHud() {
    if (hudView) return;
    hudView = new alt.WebView('http://resource/client/html/hud/index.html');
}

function closeHud() {
    if (!hudView) return;
    hudView.destroy();
    hudView = null;
}

// --- Инвентарь (Клавиша I) -----------------------------------------------
function openInventory() {
    if (inventoryView || !inGame || authView || chatTyping || consoleView) return;
    inventoryView = new alt.WebView('http://resource/client/html/inventory/index.html');
    inventoryView.focus();
    alt.showCursor(true);
    alt.toggleGameControls(false);

    inventoryView.on('flovmp:inv:close', closeInventory);
    inventoryView.on('flovmp:inv:use', (slot) => alt.emitServer('flovmp:inv:use', slot));
    inventoryView.on('flovmp:inv:drop', (slot, qty) => alt.emitServer('flovmp:inv:drop', slot, qty));
    inventoryView.on('flovmp:inv:move', (from, to) => alt.emitServer('flovmp:inv:move', from, to));
}

function closeInventory() {
    if (!inventoryView) return;
    inventoryView.destroy();
    inventoryView = null;
    try { alt.showCursor(false); } catch (e) { }
    alt.toggleGameControls(true);
}

function toggleInventory() {
    if (inventoryView) closeInventory();
    else openInventory();
}

// --- Внутриигровая консоль разработчика (F8 / F11) ------------------------
let consoleView = null;
let consoleStatsInterval = null;

function openDevConsole() {
    if (consoleView) return;
    consoleView = new alt.WebView('http://resource/client/html/console/index.html');
    consoleView.focus();
    alt.showCursor(true);
    alt.toggleGameControls(false);

    consoleView.on('flovmp:console:close', closeDevConsole);
    consoleView.on('flovmp:console:cmd', (cmd) => {
        alt.emitServer('flovmp:chat:say', '/' + cmd);
    });
    consoleView.on('flovmp:console:hotreload', () => {
        alt.log('[FloV:MP] NUI Hot-Reload requested via F8 console');
        if (chatView) chatView.reload(true);
        if (hudView) hudView.reload(true);
        if (inventoryView) inventoryView.reload(true);
        if (settingsView) settingsView.reload(true);
        if (consoleView) consoleView.emit('flovmp:console:log', 'HOTRELOAD', 'All active WebViews reloaded from disk.');
    });
    consoleView.on('flovmp:console:quit', () => {
        native.restartGame();
    });

    if (consoleStatsInterval) alt.clearInterval(consoleStatsInterval);
    consoleStatsInterval = alt.setInterval(() => {
        if (!consoleView) return;
        let fps = 60;
        try {
            const ft = native.getFrameTime();
            if (ft > 0) fps = Math.min(240, Math.round(1.0 / ft));
        } catch (e) {
            fps = 60;
        }
        let ping = 14;
        try {
            if (typeof alt.getPing === 'function') ping = alt.getPing();
        } catch (e) { }
        consoleView.emit('flovmp:console:stats', fps, ping, 60);
    }, 500);
}

function closeDevConsole() {
    if (consoleStatsInterval) {
        alt.clearInterval(consoleStatsInterval);
        consoleStatsInterval = null;
    }
    if (!consoleView) return;
    consoleView.destroy();
    consoleView = null;
    try { alt.showCursor(false); } catch (e) { }
    alt.toggleGameControls(true);
}

function toggleDevConsole() {
    if (consoleView) closeDevConsole();
    else openDevConsole();
}

function loadCollisionAndUnfreeze(targetPos) {
    const player = alt.Player.local;
    if (!player || !player.valid) return;

    native.freezeEntityPosition(player.scriptID, true);
    native.requestCollisionAtCoord(targetPos.x, targetPos.y, targetPos.z);
    native.setFocusPosAndVel(targetPos.x, targetPos.y, targetPos.z, 0, 0, 0);

    let attempts = 0;
    const interval = alt.setInterval(() => {
        attempts++;
        if (!player || !player.valid) {
            alt.clearInterval(interval);
            native.clearFocus();
            return;
        }

        native.requestCollisionAtCoord(targetPos.x, targetPos.y, targetPos.z);
        const [hasGround, groundZ] = native.getGroundZFor3dCoord(targetPos.x, targetPos.y, targetPos.z + 10.0, 0, false);
        const collisionLoaded = native.hasCollisionLoadedAroundEntity(player.scriptID);

        if ((hasGround && collisionLoaded) || attempts >= 40) {
            alt.clearInterval(interval);
            native.clearFocus();
            if (hasGround && Math.abs(groundZ - targetPos.z) < 25.0) {
                native.setEntityCoords(player.scriptID, targetPos.x, targetPos.y, groundZ + 0.5, false, false, false, true);
            }
            native.freezeEntityPosition(player.scriptID, false);
            alt.log(`[FloV:MP] Коллизия местности загружена (попыток: ${attempts}, groundZ: ${hasGround ? groundZ.toFixed(2) : 'n/a'})`);
        }
    }, 100);
}

// --- Обработчики событий -------------------------------------------------
alt.onServer('flovmp:auth:show', openAuth);
alt.onServer('flovmp:auth:hide', () => {
    closeAuth();
    openChat();
    openHud();
    inGame = true;

    const player = alt.Player.local;
    if (player && player.valid) {
        loadCollisionAndUnfreeze(player.pos);
    }
});

alt.onServer('flovmp:auth:result', (ok, message) => {
    if (authView) authView.emit('flovmp:auth:result', ok, message);
});

alt.onServer('flovmp:chat:msg', (kind, author, text) => {
    if (chatView) chatView.emit('flovmp:chat:msg', kind, author, text);
});

alt.onServer('flovmp:hud:init', (serverName) => {
    openHud();
    if (hudView) hudView.emit('flovmp:hud:init', serverName);
});

alt.onServer('flovmp:hud:tick', (hp, armor, cash, online, hour, minute) => {
    if (!hudView && inGame) openHud();
    if (hudView) hudView.emit('flovmp:hud:tick', hp, armor, cash, online, hour, minute);
});

alt.onServer('flovmp:inv:sync', (json) => {
    if (inventoryView) inventoryView.emit('flovmp:inv:sync', json);
});

alt.onServer('flovmp:inv:notice', (text) => {
    if (inventoryView) inventoryView.emit('flovmp:inv:notice', text);
    if (chatView) chatView.emit('flovmp:chat:msg', 'system', 'Инвентарь', text);
});

// --- Состояние транспорта и спидометр ----------------------------------
let seatbeltOn = false;
let inVehiclePrev = false;

alt.setInterval(() => {
    if (!inGame || !hudView) return;
    const player = alt.Player.local;
    if (!player || !player.valid) return;

    const veh = player.vehicle;
    if (veh && veh.valid) {
        inVehiclePrev = true;
        let speed = 0;
        try {
            speed = Math.round(native.getEntitySpeed(veh.scriptID) * 3.6);
        } catch (e) {
            speed = 0;
        }

        let fuel = 100.0;
        try {
            if (veh.hasStreamSyncedMetaData('fuel')) {
                fuel = veh.getStreamSyncedMetaData('fuel');
            }
        } catch (e) { }

        let gear = 1;
        try {
            gear = veh.gear;
        } catch (e) { }

        let engine = false;
        try {
            engine = native.getIsVehicleEngineRunning(veh.scriptID);
        } catch (e) { }

        let locked = false;
        try {
            locked = veh.lockState === 2;
        } catch (e) { }

        let lights = false;
        try {
            const [hasLights, lightsOn, highbeamsOn] = native.getVehicleLightsState(veh.scriptID);
            lights = lightsOn || highbeamsOn;
        } catch (e) { }

        hudView.emit('flovmp:hud:speedo', true, speed, fuel, gear, engine, locked, seatbeltOn, lights);
    } else if (inVehiclePrev) {
        inVehiclePrev = false;
        seatbeltOn = false;
        try {
            native.setPedConfigFlag(player.scriptID, 32, true);
        } catch (e) { }
        hudView.emit('flovmp:hud:speedo', false, 0, 0, 0, false, false, false, false);
    }
}, 50);

// Клавиши: F4 — NoClip, T — Чат, I — Инвентарь, F8/F11 — Консоль разработчика, F9 — Настройки, B — Ремень, 2 — Двигатель, L — Двери
alt.on('keyup', (key) => {
    if (key === 119 || key === 122) { // F8 (119) or F11 (122)
        toggleDevConsole();
        return;
    }
    if (consoleView) return;

    if (key === 73) { // I (73) — Инвентарь
        if (inGame && !chatTyping && !authView) {
            toggleInventory();
            return;
        }
    }

    if (key === 66) { // B (66) — Ремень безопасности
        if (inGame && !chatTyping && !authView) {
            const player = alt.Player.local;
            if (player && player.valid && player.vehicle) {
                seatbeltOn = !seatbeltOn;
                try {
                    native.setPedConfigFlag(player.scriptID, 32, !seatbeltOn);
                } catch (e) { }
                if (chatView) {
                    const status = seatbeltOn ? 'пристёгнут' : 'отстёгнут';
                    chatView.emit('flovmp:chat:msg', 'system', 'Транспорт', `Ремень безопасности ${status}.`);
                }
                return;
            }
        }
    }

    if (key === 50) { // 2 (50) — Двигатель авто
        if (inGame && !chatTyping && !authView) {
            const player = alt.Player.local;
            if (player && player.valid && player.vehicle) {
                alt.emitServer('flovmp:chat:say', '/engine');
                return;
            }
        }
    }

    if (key === 76) { // L (76) — Замок дверей авто
        if (inGame && !chatTyping && !authView) {
            const player = alt.Player.local;
            if (player && player.valid) {
                alt.emitServer('flovmp:chat:say', '/lock');
                return;
            }
        }
    }

    if (chatTyping || inventoryView) return;

    if (key === 115) { // F4
        toggleNoClip();
    } else if (key === 84) { // T
        startTyping();
    } else if (key === 120) { // F9
        if (settingsView) closeSettings();
        else openSettings();
    }
});

alt.on('connectionComplete', () => {
    alt.log('[FloV:MP] Успешное подключение к серверу');
    alt.emitServer('flovmp:client:ready');
});

alt.on('disconnect', () => {
    if (noClip) toggleNoClip();
    closeAuth();
    closeChat();
    closeHud();
    closeInventory();
    closeSettings();
    closeDevConsole();
    inGame = false;
    alt.log('[FloV:MP] Отключено от сервера');
});

alt.onServer('flovmp:client:welcome', (name, index) => {
    alt.log(`[Держава Онлайн] Добро пожаловать на сервер, ${name}!`);
});
