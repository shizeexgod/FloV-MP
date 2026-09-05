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
    settingsView.on('flovmp:settings:accent', () => {
        // Пока чисто клиентская настройка (localStorage, общий для всех NUI-экранов
        // этого resource) — без записи на сервер/аккаунт.
    });
}

function closeSettings() {
    if (!settingsView) return;
    settingsView.destroy();
    settingsView = null;
    try { alt.showCursor(false); } catch (e) { }
    alt.toggleGameControls(true);
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

// Клавиши: F4 — NoClip, T — Чат, F9 — Настройки
alt.on('keyup', (key) => {
    if (chatTyping) return;
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
    closeSettings();
    inGame = false;
    alt.log('[FloV:MP] Отключено от сервера');
});

alt.onServer('flovmp:client:welcome', (name, index) => {
    alt.log(`[Держава Онлайн] Добро пожаловать на сервер, ${name}!`);
});
