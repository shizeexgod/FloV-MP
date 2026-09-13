/**
 * FloV:MP — Клиентский движок мультиплеера (Clean Standalone Engine).
 * Платформа: FloV:MP Core Engine.
 */

import * as alt from 'alt-client';
import * as native from 'natives';

alt.log('[FloV:MP] Клиентский модуль чистого мультиплеера загружен');

// =============================================================================
// КОНФИГУРАЦИЯ БИНДОВ И УПРАВЛЕНИЯ (Полная свобода настройки для владельцев)
// =============================================================================
export const KEYBINDS = {
    chat: 84,        // T — Открытие чата
    chatSlash: 191,  // / — Открытие чата с командой
    console: 119,    // F8 — Консоль разработчика / DevTools
    consoleAlt: 122, // F11 — Альтернативная клавиша консоли
    noclip: 115,     // F4 — Режим свободного админ-полёта (NoClip)
    tpm: 116,        // F5 — Быстрый телепорт по фиолетовой метке (WayPoint)
    voice: 66,       // B — Голосовой чат (Push-to-Talk)
    voiceTailMs: 400,// Задержка отпускания микрофона (мс), чтобы не обрывать слова
    engine: 50,      // 2 — Завести / заглушить двигатель транспорта
    lock: 76,        // L — Закрыть / открыть двери транспорта
    seatbelt: 66     // B в транспорте — Пристегнуть / отстегнуть ремень
};

// =============================================================================
// СОСТОЯНИЕ КЛИЕНТА
// =============================================================================
let inGame = false;
let currentServerName = 'RolePlay Server';
let chatView = null;
let chatTyping = false;
let consoleView = null;
let consoleStatsInterval = null;
let currentAdminLevel = 0;
let cursorDepth = 0;

// Голосовой чат
let isVoiceTalking = false;
let voiceReleaseTimeout = null;

// Состояние транспорта
let seatbeltOn = false;

// --- Менеджер вложенности курсора (Cursor Nesting Manager) ---
function pushCursor() {
    cursorDepth++;
    if (cursorDepth === 1) {
        try { alt.showCursor(true); } catch (e) { }
    }
}

function popCursor() {
    cursorDepth = Math.max(0, cursorDepth - 1);
    if (cursorDepth === 0) {
        try { alt.showCursor(false); } catch (e) { }
    }
}

// =============================================================================
// 1. СИСТЕМА СВОБОДНОГО ПОЛЁТА (NoClip)
// =============================================================================
let noClip = false;
let noClipPos = null;

export function toggleNoClip() {
    if (!inGame || chatTyping) return;
    if (currentAdminLevel < 1) {
        alt.log('[FloV:MP] Попытка вызова NoClip отклонена (нет прав администратора)');
        return;
    }
    const player = alt.Player.local;
    if (!player || !player.valid) return;
    if (player.vehicle) return; // Защита от рассинхрона в транспорте

    noClip = !noClip;

    if (noClip) {
        noClipPos = { ...player.pos };
        native.freezeEntityPosition(player.scriptID, true);
        native.setEntityCollision(player.scriptID, false, false);
        native.setEntityInvincible(player.scriptID, true);
        native.setEntityVisible(player.scriptID, false, 0);
        native.setEntityAlpha(player.scriptID, 0, false);
        alt.log('[FloV:MP] NoClip активирован (режим невидимости)');
    } else {
        native.freezeEntityPosition(player.scriptID, false);
        native.setEntityCollision(player.scriptID, true, true);
        native.setEntityInvincible(player.scriptID, false);
        native.setEntityVisible(player.scriptID, true, 0);
        native.resetEntityAlpha(player.scriptID);
        alt.log('[FloV:MP] NoClip деактивирован (видимый)');
    }
    try {
        alt.emitServer('starter:toggleNoClip', noClip);
        alt.emitServer('flovmp:admin:noclip', noClip);
    } catch (e) { }
}

// Периодическое отключение стандартных полицейских служб GTA V
function disableAmbientDispatch() {
    try {
        for (let i = 1; i <= 15; i++) {
            native.enableDispatchService(i, false);
        }
        native.setCreateRandomCops(false);
        native.setCreateRandomCopsNotOnScenarios(false);
        native.setCreateRandomCopsOnScenarios(false);
    } catch (e) { }
}
disableAmbientDispatch();
alt.setInterval(disableAmbientDispatch, 5000);

// Постоянный цикл рендера и управления (Every Tick)
alt.everyTick(() => {
    // Отключение трафика и случайных педов
    native.setPedDensityMultiplierThisFrame(0.0);
    native.setScenarioPedDensityMultiplierThisFrame(0.0, 0.0);
    native.setVehicleDensityMultiplierThisFrame(0.0);
    native.setRandomVehicleDensityMultiplierThisFrame(0.0);
    native.setParkedVehicleDensityMultiplierThisFrame(0.0);

    const player = alt.Player.local;
    if (player && player.valid) {
        native.setPlayerWantedLevel(player.scriptID, 0, false);
        native.setPlayerWantedLevelNow(player.scriptID, false);
    }

    // Блокировка колеса оружия
    native.disableControlAction(0, 37, true);
    for (let c = 157; c <= 165; c++) native.disableControlAction(0, c, true);

    // Скрытие стандартных элементов GTA V HUD
    native.hideHudComponentThisFrame(6);  // Vehicle Name
    native.hideHudComponentThisFrame(7);  // Area Name
    native.hideHudComponentThisFrame(8);  // Vehicle Class
    native.hideHudComponentThisFrame(9);  // Street Name
    native.hideHudComponentThisFrame(19); // Weapon Wheel
    native.hideHudComponentThisFrame(20); // Weapon Wheel Stats
    native.hideHudComponentThisFrame(22); // Weapons HUD

    // Блокировка Escape в меню паузы при открытом вводе или консоли
    if (chatTyping || consoleView) {
        native.disableControlAction(0, 199, true); // INPUT_FRONTEND_PAUSE
        native.disableControlAction(0, 200, true); // INPUT_FRONTEND_PAUSE_ALTERNATE
    }

    // Перемещение NoClip
    if (noClip && player && player.valid) {
        native.disableControlAction(0, 30, true);
        native.disableControlAction(0, 31, true);
        native.disableControlAction(0, 21, true);
        native.disableControlAction(0, 22, true);
        native.disableControlAction(0, 36, true);
        native.disableControlAction(0, 44, true);
        native.disableControlAction(0, 24, true);
        native.disableControlAction(0, 25, true);

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
        } else if (native.isControlPressed(0, 19) || native.isControlPressed(0, 25)) {
            speed = 0.18; // Alt / RMB (Slow / Precise)
        } else if (native.isControlPressed(0, 24)) {
            speed = 8.5; // LMB (Turbo)
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

        // Space / Ctrl (Вертикальный подъём / спуск)
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

// =============================================================================
// 2. ИГРОВОЙ ЧАТ (Obsidian Minimalist)
// =============================================================================
export function openChat() {
    if (chatView) return;
    chatView = new alt.WebView('http://resource/client/html/chat/index.html');
    chatView.on('flovmp:chat:say', (text) => {
        const s = String(text);
        alt.emitServer('flovmp:chat:say', s);
        alt.emitServer('chat:message', s);
    });
    chatView.on('flovmp:chat:done', () => {
        chatTyping = false;
        try { chatView.unfocus(); } catch (e) { }
        alt.toggleGameControls(true);
    });
}

export function closeChat() {
    if (!chatView) return;
    chatView.destroy();
    chatView = null;
    chatTyping = false;
}

export function startTyping(initialText = '') {
    if (!chatView || chatTyping || !inGame) return;
    chatTyping = true;
    chatView.focus();
    alt.toggleGameControls(false);
    chatView.emit('flovmp:chat:openinput', initialText);
}

// =============================================================================
// 3. КОНСОЛЬ РАЗРАБОТЧИКА (F8 / F11)
// =============================================================================
export function openDevConsole() {
    if (consoleView) return;
    consoleView = new alt.WebView('http://resource/client/html/console/index.html');
    consoleView.on('load', () => {
        try {
            consoleView.focus();
            consoleView.emit('flovmp:console:permissions', currentAdminLevel);
        } catch (e) { }
    });
    pushCursor();
    alt.toggleGameControls(false);

    consoleView.on('flovmp:console:cmd', (cmd) => {
        if (!cmd) return;
        const parts = cmd.trim().split(' ');
        const name = parts[0].toLowerCase();

        if (name === 'tpm') {
            triggerWaypointTeleport();
            return;
        }

        if (name === 'pos' || name === 'coords') {
            const p = alt.Player.local;
            if (p && p.valid) {
                const text = `${p.pos.x.toFixed(2)}, ${p.pos.y.toFixed(2)}, ${p.pos.z.toFixed(2)}, ${p.rot.z.toFixed(2)}`;
                try { alt.copyToClipboard(text); } catch (e) { }
                if (consoleView) consoleView.emit('flovmp:console:log', 'DEV', `Координаты скопированы в буфер: ${text}`);
            }
            return;
        }

        if (name === 'noclip') {
            toggleNoClip();
            if (consoleView) consoleView.emit('flovmp:console:log', 'DEV', `NoClip: ${noClip ? 'ВКЛЮЧЕН (инвиз)' : 'ВЫКЛЮЧЕН'}`);
            return;
        }

        // Отправка команды на сервер
        alt.emitServer('flovmp:chat:say', '/' + cmd);
        alt.emitServer('chat:message', '/' + cmd);
    });

    consoleView.on('flovmp:console:hotreload', () => {
        alt.log('[FloV:MP] NUI Hot-Reload requested via F8 console');
        if (chatView) chatView.reload(true);
        if (consoleView) consoleView.emit('flovmp:console:log', 'RELOAD', 'Все активные WebViews перезагружены.');
    });

    consoleView.on('flovmp:console:quit', () => {
        try {
            if (typeof alt.disconnect === 'function') alt.disconnect();
        } catch (e) { }
    });

    consoleView.on('flovmp:console:close', () => {
        closeDevConsole();
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

        try {
            const local = alt.Player.local;
            if (local && local.valid) {
                const pos = local.pos;
                const entities = [];
                for (const p of alt.Player.all) {
                    if (p && p.valid) {
                        const isLoc = p === local;
                        entities.push({
                            cell: `Cell [${Math.floor(p.pos.x / 64)}, ${Math.floor(p.pos.y / 64)}]`,
                            type: 'Player',
                            id: '#' + p.id + (isLoc ? ' (Local)' : ''),
                            dist: isLoc ? '0.0 m' : `${Math.round(p.pos.distanceTo(pos))} m`,
                            status: 'Visible'
                        });
                    }
                }
                for (const v of alt.Vehicle.all) {
                    if (v && v.valid) {
                        entities.push({
                            cell: `Cell [${Math.floor(v.pos.x / 64)}, ${Math.floor(v.pos.y / 64)}]`,
                            type: 'Vehicle',
                            id: '#' + v.id,
                            dist: `${Math.round(v.pos.distanceTo(pos))} m`,
                            status: 'Visible'
                        });
                    }
                }
                consoleView.emit('flovmp:console:entities_data', entities);
            }
        } catch (e) { }
    }, 500);
}

export function closeDevConsole() {
    if (consoleStatsInterval) {
        alt.clearInterval(consoleStatsInterval);
        consoleStatsInterval = null;
    }
    if (!consoleView) return;
    consoleView.destroy();
    consoleView = null;
    popCursor();
    alt.toggleGameControls(true);
}

export function toggleDevConsole() {
    if (consoleView) closeDevConsole();
    else openDevConsole();
}

// =============================================================================
// 4. ТЕЛЕПОРТАЦИЯ НА МЕТКУ (TPM / F5)
// =============================================================================
export function triggerWaypointTeleport() {
    if (currentAdminLevel < 1) {
        if (chatView) chatView.emit('flovmp:chat:msg', 'system', '', '{ef4444}[FloV:MP Security] Доступ запрещен (требуются права администратора).');
        return;
    }
    const blip = native.getFirstBlipInfoId(8);
    if (!native.doesBlipExist(blip)) {
        if (chatView) {
            chatView.emit('flovmp:chat:msg', 'system', '', '{f87171}[FloV:MP] Поставьте фиолетовую метку (waypoint) на карте перед использованием TPM!');
        }
        if (consoleView) {
            consoleView.emit('flovmp:console:log', 'WARN', 'Метка на карте (waypoint) не найдена.');
        }
        return;
    }
    const coords = native.getBlipInfoIdCoord(blip);
    const [found, groundZ] = native.getGroundZFor3dCoord(coords.x, coords.y, 800.0, 0, false);
    const z = found ? groundZ + 1.0 : coords.z + 1.0;
    alt.emitServer('starter:teleportWaypoint', coords.x, coords.y, z);
    if (consoleView) {
        consoleView.emit('flovmp:console:log', 'DEV', `Телепорт по метке: ${coords.x.toFixed(1)}, ${coords.y.toFixed(1)}, ${z.toFixed(1)}`);
    }
}

// =============================================================================
// 5. ПЛАВНАЯ ПРОГРУЗКА КОЛЛИЗИЙ И СПАВН (Защита от Shift+W багов)
// =============================================================================
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
            try { native.doScreenFadeIn(500); } catch (e) { }
            return;
        }

        native.requestCollisionAtCoord(targetPos.x, targetPos.y, targetPos.z);
        const [hasGround, groundZ] = native.getGroundZFor3dCoord(targetPos.x, targetPos.y, targetPos.z + 10.0, 0, false);
        const collisionLoaded = native.hasCollisionLoadedAroundEntity(player.scriptID);

        if ((hasGround && collisionLoaded) || attempts >= 40) {
            alt.clearInterval(interval);
            native.clearFocus();
            if (hasGround && Math.abs(groundZ - targetPos.z) < 25.0) {
                native.setEntityCoords(player.scriptID, targetPos.x, targetPos.y, groundZ, false, false, false, true);
            }
            native.setEntityVelocity(player.scriptID, 0, 0, 0);
            native.clearPedTasksImmediately(player.scriptID);
            native.setRunSprintMultiplierForPlayer(player.scriptID, 1.0);
            native.setPedCanRagdoll(player.scriptID, true);
            native.freezeEntityPosition(player.scriptID, false);
            alt.nextTick(() => {
                if (player && player.valid) {
                    native.setEntityVelocity(player.scriptID, 0, 0, 0);
                }
            });
            try { native.doScreenFadeIn(700); } catch (e) { }
            alt.log(`[FloV:MP] Спавн завершен. Коллизия загружена (попыток: ${attempts}, groundZ: ${hasGround ? groundZ.toFixed(2) : 'n/a'})`);
        }
    }, 100);
}

// =============================================================================
// 6. ГОЛОСОВОЙ ЧАТ PUSH-TO-TALK (B с задержкой отпускания 400 мс)
// =============================================================================
function handleVoiceKeyDown() {
    if (voiceReleaseTimeout) {
        alt.clearTimeout(voiceReleaseTimeout);
        voiceReleaseTimeout = null;
    }
    if (!isVoiceTalking) {
        isVoiceTalking = true;
        try {
            if (typeof alt.setMicGain === 'function') alt.setMicGain(1.0);
            alt.emit('flovmp:voice:active', true);
        } catch (e) { }
    }
}

function handleVoiceKeyUp() {
    if (!isVoiceTalking) return;
    // Задержка отпускания (hangover tail) предотвращает обрезание окончаний слов
    voiceReleaseTimeout = alt.setTimeout(() => {
        isVoiceTalking = false;
        voiceReleaseTimeout = null;
        try {
            if (typeof alt.setMicGain === 'function') alt.setMicGain(0.0);
            alt.emit('flovmp:voice:active', false);
        } catch (e) { }
    }, KEYBINDS.voiceTailMs);
}

// =============================================================================
// 7. ОБРАБОТЧИКИ КЛАВИАТУРЫ
// =============================================================================
alt.on('keydown', (key) => {
    if (key === KEYBINDS.voice) {
        const player = alt.Player.local;
        const inVehicle = player && player.valid && player.vehicle;
        // Если не в чате и не за рулём (где B = ремень)
        if (!chatTyping && !consoleView && !inVehicle) {
            handleVoiceKeyDown();
        }
    }
});

alt.on('keyup', (key) => {
    // F8 / F11 — Консоль
    if (key === KEYBINDS.console || key === KEYBINDS.consoleAlt) {
        toggleDevConsole();
        return;
    }
    if (consoleView) return;

    // F4 — NoClip
    if (key === KEYBINDS.noclip) {
        toggleNoClip();
        return;
    }

    // F5 — TPM (Быстрый телепорт на waypoint)
    if (key === KEYBINDS.tpm) {
        triggerWaypointTeleport();
        return;
    }

    // B — Микрофон (отпускание) или Ремень в авто
    if (key === KEYBINDS.voice) {
        const player = alt.Player.local;
        if (player && player.valid && player.vehicle) {
            // В авто: переключение ремня безопасности
            seatbeltOn = !seatbeltOn;
            try {
                native.setPedConfigFlag(player.scriptID, 32, !seatbeltOn);
            } catch (e) { }
            if (chatView) {
                const status = seatbeltOn ? 'пристёгнут' : 'отстёгнут';
                chatView.emit('flovmp:chat:msg', 'system', 'Транспорт', `Ремень безопасности ${status}.`);
            }
            return;
        } else {
            handleVoiceKeyUp();
            return;
        }
    }

    // 2 — Двигатель транспорта
    if (key === KEYBINDS.engine) {
        if (inGame && !chatTyping) {
            const player = alt.Player.local;
            if (player && player.valid && player.vehicle) {
                alt.emitServer('flovmp:chat:say', '/engine');
                return;
            }
        }
    }

    // L — Замок дверей транспорта
    if (key === KEYBINDS.lock) {
        if (inGame && !chatTyping) {
            const player = alt.Player.local;
            if (player && player.valid) {
                alt.emitServer('flovmp:chat:say', '/lock');
                return;
            }
        }
    }

    if (chatTyping) return;

    // T — Открыть чат
    if (key === KEYBINDS.chat) {
        startTyping();
    }
    // / — Открыть чат со слэшем
    else if (key === KEYBINDS.chatSlash) {
        startTyping('/');
    }
});

// =============================================================================
// 8. СЕРВЕРНЫЕ СОБЫТИЯ И ЖИЗНЕННЫЙ ЦИКЛ
// =============================================================================
alt.on('connectionComplete', () => {
    alt.log('[FloV:MP] Успешное прямое подключение к серверу');
    inGame = true;
    native.displayRadar(true);
    native.displayHud(true);
    openChat();
    alt.emitServer('flovmp:client:ready');

    const player = alt.Player.local;
    if (player && player.valid) {
        loadCollisionAndUnfreeze(player.pos);
    }
});

alt.on('disconnect', () => {
    if (noClip) toggleNoClip();
    closeChat();
    closeDevConsole();
    inGame = false;
    alt.log('[FloV:MP] Отключено от сервера');
});

// Инициализация чистого стартера
alt.onServer('starter:initClient', () => {
    alt.log('[FloV:MP] Игровой клиент FloV:MP активирован');
    inGame = true;
    native.displayRadar(true);
    native.displayHud(true);
    openChat();

    const player = alt.Player.local;
    if (player && player.valid) {
        loadCollisionAndUnfreeze(player.pos);
    }
});

alt.onServer('flovmp:console:setAdmin', (lvl) => {
    currentAdminLevel = Number(lvl) || 0;
    alt.log(`[FloV:MP] Уровень прав администратора: ${currentAdminLevel}`);
    if (consoleView) {
        try { consoleView.emit('flovmp:console:permissions', currentAdminLevel); } catch (e) { }
    }
});

// Реанимация (/revive)
alt.onServer('starter:revive', () => {
    const local = alt.Player.local;
    if (local && local.valid) {
        try {
            native.resurrectPed(local.scriptID);
            native.clearPedTasksImmediately(local.scriptID);
            native.setPedCanRagdoll(local.scriptID, true);
            native.freezeEntityPosition(local.scriptID, false);
            native.doScreenFadeIn(300);
        } catch (e) { }
    }
});

alt.onServer('starter:requestWaypointTp', () => {
    triggerWaypointTeleport();
});

alt.onServer('starter:copyCoords', (x, y, z, yaw) => {
    try {
        const text = `${x.toFixed(2)}, ${y.toFixed(2)}, ${z.toFixed(2)}, ${yaw.toFixed(2)}`;
        alt.copyToClipboard(text);
        if (chatView) {
            chatView.emit('flovmp:chat:msg', 'system', '', `{34d399}[FloV:MP] Координаты скопированы в буфер: ${text}`);
        }
    } catch (e) { }
});

alt.onServer('starter:setWeather', (weatherType) => {
    try {
        native.setWeatherTypeOverTimePersist(weatherType, 1.5);
    } catch (e) { }
});

alt.onServer('starter:setTime', (hour, minute) => {
    try {
        native.setClockTime(hour, minute, 0);
    } catch (e) { }
});

alt.onServer('starter:toggleNoClip', () => {
    toggleNoClip();
});

alt.onServer('starter:setGodMode', (enabled) => {
    const local = alt.Player.local;
    if (local && local.valid) {
        try {
            native.setEntityInvincible(local.scriptID, !!enabled);
            native.setPlayerInvincible(local.scriptID, !!enabled);
        } catch (e) { }
    }
});

alt.onServer('starter:setSpeed', (multiplier) => {
    const local = alt.Player.local;
    if (local && local.valid) {
        try {
            const mult = Math.max(1.0, Math.min(1.49, Number(multiplier) || 1.0));
            native.setRunSprintMultiplierForPlayer(local.scriptID, mult);
        } catch (e) { }
    }
});

alt.onServer('starter:setFrozen', (frozen) => {
    const local = alt.Player.local;
    if (local && local.valid) {
        try {
            native.freezeEntityPosition(local.scriptID, !!frozen);
        } catch (e) { }
    }
});

// Сообщения чата
alt.onServer('flovmp:chat:msg', (kind, author, text) => {
    if (chatView) chatView.emit('flovmp:chat:msg', kind, author, text);
    if (consoleView) {
        const prefix = author ? `[${author}] ` : '';
        consoleView.emit('flovmp:console:log', 'CHAT', `${prefix}${text}`);
    }
});

alt.onServer('chat:addMessage', (text) => {
    if (chatView) chatView.emit('flovmp:chat:msg', 'system', '', String(text));
    if (consoleView) consoleView.emit('flovmp:console:log', 'CHAT', String(text));
});

alt.onServer('chat:message', (author, text) => {
    if (chatView) chatView.emit('flovmp:chat:msg', 'player', author, text);
    if (consoleView) consoleView.emit('flovmp:console:log', 'CHAT', `[${author}] ${text}`);
});

alt.onServer('flovmp:chat:clear', () => {
    if (chatView) chatView.emit('flovmp:chat:clear');
});

// Защита FPS при массовых скоплениях на спавне (> 15 игроков в стриминге)
alt.setInterval(() => {
    const local = alt.Player.local;
    if (!local || !local.valid || !inGame) return;

    const nearbyPlayers = alt.Player.streamedIn;
    if (nearbyPlayers.length > 15) {
        native.setPedCanRagdoll(local.scriptID, false);
        for (let i = 0; i < nearbyPlayers.length; i++) {
            const remote = nearbyPlayers[i];
            if (remote && remote.valid && remote.scriptID) {
                native.setEntityNoCollisionEntity(local.scriptID, remote.scriptID, true);
            }
        }
    } else {
        native.setPedCanRagdoll(local.scriptID, true);
    }
}, 500);
