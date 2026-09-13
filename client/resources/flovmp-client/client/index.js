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
    help: 112,       // F1 — Меню помощи / консоль и быстрый выход
    chat: 84,        // T — Открытие чата
    chatSlash: 191,  // / — Открытие чата с командой
    console: 119,    // F8 — Консоль разработчика / DevTools
    consoleAlt: 122, // F11 — Альтернативная клавиша консоли
    noclip: 115,     // F4 — Режим свободного админ-полёта (NoClip)
    tpm: 116,        // F5 — Быстрый телепорт по фиолетовой метке (WayPoint)
    esp: 114,        // F3 — Режим админского видения (ESP Wallhack)
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
let consoleOpen = false;
let consoleStatsInterval = null;
let currentAdminLevel = 0;
let godMode = false;
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
// 1. СИСТЕМА СВОБОДНОГО ПОЛЁТА (NoClip) — Архитектура Sayonara RP
// =============================================================================
let noClip = false;
let noClipPos = null;
let flyF = 2.0;
let flyL = 2.0;
let flyH = 2.0;

const FLY_SPEED = {
    precise: 0.18, // Alt / RMB — Точный осмотр интерьеров / расстановка объектов
    normal: 0.85,  // WASD — Стандартная крейсерская скорость
    sprint: 3.50,  // Shift — Быстрый патруль
    turbo: 7.50    // LMB (ЛКМ) — Турбо-полёт через всю карту
};

function setPlayerToGround() {
    const player = alt.Player.local;
    if (!player || !player.valid) return false;
    const pPos = player.pos;

    let highestSurfaceZ = null;

    // 1. Луч вниз (ShapeTest Raycast): проверяет крыши домов, мосты, платформы, дороги, объекты
    try {
        const probe = native.startExpensiveSynchronousShapeTestLosProbe(
            pPos.x, pPos.y, pPos.z,
            pPos.x, pPos.y, pPos.z - 25.0,
            1 | 2 | 16, // Map, Vehicles, Objects/Buildings
            player.scriptID,
            7
        );
        const [retval, hit, endCoords, surfaceNormal, entityHit] = native.getShapeTestResult(probe);
        if (hit && endCoords && Number.isFinite(endCoords.z)) {
            highestSurfaceZ = endCoords.z;
        }
    } catch (_) { }

    // 2. Высота рельефа земли под игроком (ландшафт, горы, холмы)
    try {
        const [found, groundZ] = native.getGroundZFor3dCoord(pPos.x, pPos.y, pPos.z, 0, false);
        if (found && Number.isFinite(groundZ)) {
            if (highestSurfaceZ === null || groundZ > highestSurfaceZ) {
                highestSurfaceZ = groundZ;
            }
        }
    } catch (_) { }

    // 3. Приземление ТОЛЬКО если расстояние до высшей точки поверхности под ногами <= 20 метров
    if (highestSurfaceZ !== null) {
        const heightDiff = pPos.z - highestSurfaceZ;
        if (heightDiff >= -0.5 && heightDiff <= 20.0) {
            native.setEntityCoordsNoOffset(player.scriptID, pPos.x, pPos.y, highestSurfaceZ + 1.0, false, false, false);
            return true;
        }
    }

    // Если выше 20 метров от любой поверхности — остаёмся на текущей высоте в воздухе
    return false;
}

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
        noClipPos = { x: player.pos.x, y: player.pos.y, z: player.pos.z };
        flyF = 2.0;
        flyL = 2.0;
        flyH = 2.0;
        native.freezeEntityPosition(player.scriptID, true);
        native.setEntityCollision(player.scriptID, false, false);
        native.setEntityInvincible(player.scriptID, true);
        native.setEntityVisible(player.scriptID, false, 0);
        native.setEntityAlpha(player.scriptID, 0, false);
        alt.log('[FloV:MP] NoClip активирован (инвиз)');
        if (chatView) {
            chatView.emit('flovmp:chat:msg', 'system', '', '{38bdf8}[FloV:MP] NoClip {34d399}ВКЛЮЧЕН{ffffff} (Shift: бег, ПКМ/Alt: точность, ЛКМ: турбо)');
        }
    } else {
        // Если Пробел не зажат — мягко опускаем персонажа на землю, предотвращая падение
        if (!native.isControlPressed(0, 22) && !native.isDisabledControlPressed(0, 22)) {
            setPlayerToGround();
        }
        native.setEntityVelocity(player.scriptID, 0.0, 0.0, 0.0);
        native.freezeEntityPosition(player.scriptID, false);
        native.setEntityCollision(player.scriptID, true, true);
        native.setEntityInvincible(player.scriptID, currentAdminLevel >= 1 && godMode);
        native.setEntityVisible(player.scriptID, true, 0);
        native.resetEntityAlpha(player.scriptID);
        alt.log('[FloV:MP] NoClip деактивирован');
        if (chatView) {
            chatView.emit('flovmp:chat:msg', 'system', '', '{38bdf8}[FloV:MP] NoClip {f87171}ВЫКЛЮЧЕН');
        }
    }
    try {
        alt.emitServer('flovmp:admin:noclip', noClip);
    } catch (e) { }
}

// =============================================================================
// 1.1 СИСТЕМА АДМИН-ВИДЕНИЯ (ESP / Wallhack) — Архитектура Sayonara RP
// =============================================================================
let espMode = 0; // 0: ВЫКЛ, 1: Игроки, 2: Транспорт, 3: Все (Игроки + ТС)

const ESP_MODE_NAMES = ['ВЫКЛ', 'Игроки', 'Транспорт', 'Все (Игроки + ТС)'];

const WEAPON_NAMES = {
    0xA2719263: 'Кулаки',
    0x1B06D571: 'Pistol',
    0x5EF9FEC4: 'Combat Pistol',
    0x22D8FE39: 'AP Pistol',
    0x99AEEB3B: 'Pistol .50',
    0x13532244: 'Micro SMG',
    0x2BE6766B: 'SMG',
    0xEFE7E2DF: 'Assault SMG',
    0x0A3D4D34: 'Combat PDW',
    0xBFEFFF6D: 'Assault Rifle',
    0x83BF0278: 'Carbine Rifle',
    0xAF113F99: 'Advanced Rifle',
    0xC0A3098D: 'Special Carbine',
    0x7F229F94: 'Bullpup Rifle',
    0x624488B9: 'Compact Rifle',
    0x9D079177: 'Heavy Rifle',
    0x1D073A89: 'Pump Shotgun',
    0x7846A318: 'Sawed-Off Shotgun',
    0xE284C533: 'Bullpup Shotgun',
    0x9D61E50F: 'Heavy Shotgun',
    0x05FC3C11: 'Sniper Rifle',
    0x0C472FE2: 'Heavy Sniper',
    0x63AB0442: 'Homing Launcher',
    0xB1CA77B1: 'RPG',
    0x687652CE: 'Minigun',
    0x42BF8A85: 'Grenade',
    0x787F0BB: 'Smoke Grenade',
    0x61560C7: 'Molotov',
    0x47757124: 'Flare',
    0x3656C8C1: 'Stun Gun'
};

function getPedWeaponLabel(pedScriptId) {
    try {
        const wh = native.getSelectedPedWeapon(pedScriptId);
        // Если кулаки или пусто в руках — возвращаем пустую строку (в ESP ничего не пишется)
        if (!wh || wh === 0 || (wh | 0) === (0xA2719263 | 0)) return '';
        if (WEAPON_NAMES[wh]) return WEAPON_NAMES[wh];
        const dn = native.getDisplayNameFromWeaponHash(wh);
        if (dn && dn !== 'NULL') {
            const label = native.getLabelText(dn);
            if (label && label !== 'NULL' && label !== dn) return label;
            return dn;
        }
    } catch (_) { }
    return '';
}

function getVehModelLabel(veh) {
    try {
        const model = veh.model;
        const dn = native.getDisplayNameFromVehicleModel(model);
        if (dn && dn !== 'NULL') {
            const label = native.getLabelText(dn);
            if (label && label !== 'NULL' && label !== dn) return label;
            return dn;
        }
    } catch (_) { }
    return 'Транспорт';
}

function fadeAlpha(baseA, dist, maxDist = 250) {
    if (dist <= 35) return baseA;
    if (dist >= maxDist) return Math.max(45, Math.floor(baseA * 0.28));
    const t = (dist - 35) / (maxDist - 35);
    return Math.max(45, Math.floor(baseA * (1.0 - t * 0.65)));
}

function drawEspText(text, sx, sy, color, scale = 0.28, center = true, bold = false) {
    try {
        native.setTextFont(4);
        native.setTextScale(scale, scale);
        native.setTextProportional(true);
        native.setTextColour(color[0], color[1], color[2], color[3] !== undefined ? color[3] : 230);
        native.setTextOutline();
        native.setTextCentre(center);
        native.beginTextCommandDisplayText('STRING');
        native.addTextComponentSubstringPlayerName(String(text));
        if (bold) {
            native.endTextCommandDisplayText(sx - 0.0005, sy);
            native.beginTextCommandDisplayText('STRING');
            native.addTextComponentSubstringPlayerName(String(text));
        }
        native.endTextCommandDisplayText(sx, sy);
    } catch (_) { }
}

export function toggleEsp(targetMode = null) {
    if (currentAdminLevel < 1) {
        alt.log('[FloV:MP] Доступ к ESP отклонен (нет прав администратора)');
        return;
    }
    if (targetMode !== null && typeof targetMode === 'number') {
        espMode = Math.max(0, Math.min(3, targetMode));
    } else {
        espMode = (espMode + 1) % 4;
    }
    const currentName = ESP_MODE_NAMES[espMode];
    if (chatView) {
        chatView.emit('flovmp:chat:msg', 'system', '', `{38bdf8}[FloV:MP ESP] Режим: {34d399}${currentName}`);
    }
    if (consoleView) {
        consoleView.emit('flovmp:console:log', 'ADMIN', `ESP режим: ${currentName}`);
    }
    try {
        native.playSoundFrontend(-1, "NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET", true);
    } catch (_) { }
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

    // Перемещение NoClip (Sayonara RP: точная сферическая математика + ускорение)
    if (noClip && player && player.valid) {
        native.disableControlAction(0, 30, true);  // MOVE_LR
        native.disableControlAction(0, 31, true);  // MOVE_UD
        native.disableControlAction(0, 21, true);  // SPRINT
        native.disableControlAction(0, 22, true);  // JUMP
        native.disableControlAction(0, 36, true);  // DUCK
        native.disableControlAction(0, 44, true);  // COVER
        native.disableControlAction(0, 24, true);  // ATTACK (LMB)
        native.disableControlAction(0, 25, true);  // AIM (RMB)
        native.disableControlAction(0, 140, true); // MELEE_LIGHT
        native.disableControlAction(0, 141, true); // MELEE_HEAVY
        native.disableControlAction(0, 142, true); // MELEE_ALTERNATE
        native.disableControlAction(0, 257, true); // ATTACK2

        const camRot = native.getGameplayCamRot(2);
        const heading = camRot.z * (Math.PI / 180.0);
        const pitch = camRot.x * (Math.PI / 180.0);
        const cosPitch = Math.cos(pitch);

        // Вектор взгляда камеры (сферические координаты Sayonara RP)
        const dirX = -Math.sin(heading) * cosPitch;
        const dirY = Math.cos(heading) * cosPitch;
        const dirZ = Math.sin(pitch);

        // Определение скорости (Gears)
        let speed = FLY_SPEED.normal;
        if (native.isControlPressed(0, 24) || native.isDisabledControlPressed(0, 24)) {
            speed = FLY_SPEED.turbo; // LMB — Турбо (7.5)
        } else if (native.isControlPressed(0, 25) || native.isDisabledControlPressed(0, 25) || native.isControlPressed(0, 19)) {
            speed = FLY_SPEED.precise; // RMB / Alt — Точный/медленный (0.18)
        } else if (native.isControlPressed(0, 21) || native.isDisabledControlPressed(0, 21)) {
            speed = FLY_SPEED.sprint; // Shift — Быстрый (3.5)
        }

        // W / S (Вперед / Назад по взгляду камеры)
        if (native.isControlPressed(0, 32) || native.isDisabledControlPressed(0, 32)) {
            flyF = Math.min(8.0, flyF * 1.025);
            noClipPos.x += dirX * flyF * speed;
            noClipPos.y += dirY * flyF * speed;
            noClipPos.z += dirZ * flyF * speed;
        } else if (native.isControlPressed(0, 33) || native.isDisabledControlPressed(0, 33)) {
            flyF = Math.min(8.0, flyF * 1.025);
            noClipPos.x -= dirX * flyF * speed;
            noClipPos.y -= dirY * flyF * speed;
            noClipPos.z -= dirZ * flyF * speed;
        } else {
            flyF = 2.0;
        }

        // A / D (Стрейф влево / вправо строго перпендикулярно рысканию камеры)
        if (native.isControlPressed(0, 34) || native.isDisabledControlPressed(0, 34)) {
            flyL = Math.min(8.0, flyL * 1.025);
            noClipPos.x += (-dirY) * flyL * speed;
            noClipPos.y += dirX * flyL * speed;
        } else if (native.isControlPressed(0, 35) || native.isDisabledControlPressed(0, 35)) {
            flyL = Math.min(8.0, flyL * 1.05);
            noClipPos.x -= (-dirY) * flyL * speed;
            noClipPos.y -= dirX * flyL * speed;
        } else {
            flyL = 2.0;
        }

        // Space / Ctrl (Чистый вертикальный подъём / спуск)
        if (native.isControlPressed(0, 22) || native.isDisabledControlPressed(0, 22)) {
            flyH = Math.min(8.0, flyH * 1.025);
            noClipPos.z += flyH * speed;
        } else if (native.isControlPressed(0, 36) || native.isDisabledControlPressed(0, 36)) {
            flyH = Math.min(8.0, flyH * 1.05);
            noClipPos.z -= flyH * speed;
        } else {
            flyH = 2.0;
        }

        native.setEntityCoordsNoOffset(player.scriptID, noClipPos.x, noClipPos.y, noClipPos.z, false, false, false);
        native.setEntityHeading(player.scriptID, camRot.z);
        native.freezeEntityPosition(player.scriptID, true);
        native.setEntityCollision(player.scriptID, false, false);
        native.setEntityInvincible(player.scriptID, true);
        native.setEntityVisible(player.scriptID, false, 0);
        native.setEntityAlpha(player.scriptID, 0, false);
    }

    // Отрисовка ESP (Sayonara RP: аппаратный рендеринг native text, дистанционное масштабирование)
    if (espMode > 0 && currentAdminLevel >= 1 && inGame) {
        const camPos = native.getGameplayCamCoord();
        const showPlayers = (espMode === 1 || espMode === 3);
        const showVehicles = (espMode === 2 || espMode === 3);

        if (showPlayers) {
            const streamedPlayers = alt.Player.streamedIn;
            const allToRender = [player, ...streamedPlayers];
            for (let i = 0; i < allToRender.length; i++) {
                const p = allToRender[i];
                if (!p || !p.valid) continue;

                const isSelf = p === player;
                const pedId = p.scriptID;
                if (!pedId || pedId === 0) continue;

                const pPos = p.pos;
                const dx = pPos.x - camPos.x;
                const dy = pPos.y - camPos.y;
                const dz = pPos.z - camPos.z;
                const dist = Math.round(Math.sqrt(dx * dx + dy * dy + dz * dz));
                if (!isSelf && dist > 250) continue;

                // Позиция якоря: при NoClip у себя отключаем расчет костей (устраняет дрожание)
                let ax = pPos.x;
                let ay = pPos.y;
                let az = pPos.z + 1.05;

                if (isSelf && noClip && noClipPos) {
                    ax = noClipPos.x;
                    ay = noClipPos.y;
                    az = noClipPos.z + 0.35;
                } else {
                    try {
                        const boneIdx = native.getPedBoneIndex(pedId, 12844);
                        if (boneIdx !== -1) {
                            const bc = native.getPedBoneCoords(pedId, 12844, 0.0, 0.0, 0.28);
                            if (Number.isFinite(bc.z)) {
                                ax = bc.x;
                                ay = bc.y;
                                az = bc.z;
                            }
                        }
                    } catch (_) { }
                }

                const [onScreen, sx, sy] = native.getScreenCoordFromWorldCoord(ax, ay, az);
                if (!onScreen) continue;

                let pAdmin = 0;
                try {
                    pAdmin = p.getStreamSyncedMetaData('adminLevel') || 0;
                } catch (_) { }
                if (isSelf) pAdmin = currentAdminLevel;

                // Иерархия: обычный администратор (< 8) не видит Основателя (ур. 8) в ESP
                if (!isSelf && currentAdminLevel < 8 && pAdmin >= 8) continue;

                let color;
                let adminBadge = '';

                if (isSelf) {
                    color = [192, 132, 252, fadeAlpha(240, dist)]; // Фиолетовый
                    adminBadge = pAdmin >= 8 ? ' (Вы, Основатель)' : (pAdmin > 0 ? ' (Вы, Админ)' : ' (Вы)');
                } else if (pAdmin >= 8) {
                    color = [255, 199, 64, fadeAlpha(240, dist)];  // Золотой (Главный / Основатель)
                    adminBadge = ' (Основатель)';
                } else if (pAdmin > 0) {
                    color = [248, 113, 113, fadeAlpha(240, dist)]; // Кораллово-красный (Администрация)
                    adminBadge = ' (Админ)';
                } else {
                    color = [244, 244, 246, fadeAlpha(230, dist)]; // Чистый белый (Игроки)
                    adminBadge = '';
                }

                const fontScale = Math.max(0.20, Math.min(0.35, 0.35 * (1.0 - dist / 320.0)));
                const lineGap = 0.020 * (fontScale / 0.30);

                const line1 = `[${p.id}] ${p.name}${adminBadge}`;

                let hp = 100;
                let armor = 0;
                try {
                    const rawHp = native.getEntityHealth(pedId);
                    hp = Math.max(0, Math.min(100, rawHp > 100 ? rawHp - 100 : rawHp));
                    armor = Math.max(0, Math.min(100, native.getPedArmour(pedId)));
                } catch (_) { }

                const distPart = isSelf ? 'SELF' : `${dist}m`;
                const line2 = `${hp} HP  ${armor} AR  ${distPart}`;

                const lines = [line1, line2];
                // Блок оружия: отображается ТОЛЬКО если у игрока в руках реальное оружие (не кулаки)
                const weaponName = getPedWeaponLabel(pedId);
                if (weaponName) {
                    lines.push(`[${weaponName}]`);
                }

                const blockTop = sy - ((lines.length - 1) * lineGap);
                for (let li = 0; li < lines.length; li++) {
                    drawEspText(lines[li], sx, blockTop + li * lineGap, color, fontScale, true, true);
                }
            }
        }

        if (showVehicles) {
            const streamedVehicles = alt.Vehicle.streamedIn;
            for (let i = 0; i < streamedVehicles.length; i++) {
                const veh = streamedVehicles[i];
                if (!veh || !veh.valid) continue;
                if (player.vehicle && player.vehicle === veh) continue;

                const vPos = veh.pos;
                const dx = vPos.x - camPos.x;
                const dy = vPos.y - camPos.y;
                const dz = vPos.z - camPos.z;
                const dist = Math.round(Math.sqrt(dx * dx + dy * dy + dz * dz));
                if (dist > 180) continue;

                const [onScreen, sx, sy] = native.getScreenCoordFromWorldCoord(vPos.x, vPos.y, vPos.z + 0.6);
                if (!onScreen) continue;

                const vehModel = getVehModelLabel(veh);
                let plate = '';
                let engineHp = 1000;
                let speedKmh = 0;
                try {
                    const vid = veh.scriptID;
                    if (vid) {
                        plate = (native.getVehicleNumberPlateText(vid) || '').trim();
                        engineHp = Math.round(native.getVehicleEngineHealth(vid) || 0);
                        speedKmh = Math.round((native.getEntitySpeed(vid) || 0) * 3.6);
                    }
                } catch (_) { }

                const fontScale = Math.max(0.18, Math.min(0.30, 0.30 * (1.0 - dist / 240.0)));
                const lineGap = 0.018 * (fontScale / 0.26);
                const vehColor = [125, 211, 252, fadeAlpha(210, dist, 180)];

                const line1 = `${vehModel} #${veh.id}`;
                const line2 = `${plate ? plate + ' · ' : ''}${engineHp} HP · ${dist}m`;
                const line3 = `${speedKmh} km/h`;

                const vLines = [line1, line2, line3];
                const vTop = sy - ((vLines.length - 1) * lineGap);
                for (let li = 0; li < vLines.length; li++) {
                    drawEspText(vLines[li], sx, vTop + li * lineGap, vehColor, fontScale, true, false);
                }
            }
        }
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
    });
    chatView.on('flovmp:chat:quit', () => {
        quitGame();
    });
    chatView.on('flovmp:chat:done', () => {
        if (chatTyping) {
            chatTyping = false;
            popCursor();
            try { chatView.unfocus(); } catch (e) { }
            if (!consoleOpen) {
                alt.toggleGameControls(true);
            }
        }
    });
}

export function closeChat() {
    if (!chatView) return;
    if (chatTyping) {
        chatTyping = false;
        popCursor();
        if (!consoleOpen) {
            alt.toggleGameControls(true);
        }
    }
    chatView.destroy();
    chatView = null;
}

export function startTyping(initialText = '') {
    if (!chatView || chatTyping || !inGame || consoleOpen) return;
    if (isVoiceTalking) {
        isVoiceTalking = false;
        if (voiceReleaseTimeout) {
            alt.clearTimeout(voiceReleaseTimeout);
            voiceReleaseTimeout = null;
        }
        try {
            if (typeof alt.setMicGain === 'function') alt.setMicGain(0.0);
            alt.emit('flovmp:voice:active', false);
        } catch (e) { }
    }
    chatTyping = true;
    chatView.focus();
    pushCursor();
    alt.toggleGameControls(false);
    chatView.emit('flovmp:chat:openinput', initialText);
}

// =============================================================================
// 3. ВЫХОД ИЗ ИГРЫ И КОНСОЛЬ РАЗРАБОТЧИКА (F1 / F8 / F11)
// =============================================================================
export function quitGame() {
    alt.log('[FloV:MP] Завершение игрового процесса...');
    try {
        if (typeof alt.emit === 'function') alt.emit('exit');
        if (typeof alt.disconnect === 'function') alt.disconnect();
    } catch (e) { }
    try {
        native.restartGame();
    } catch (e) { }
}

export function getOrCreateDevConsole() {
    if (!consoleView) {
        consoleView = new alt.WebView('http://resource/client/html/console/index.html');
        consoleView.on('load', () => {
            try {
                consoleView.emit('flovmp:console:permissions', currentAdminLevel);
            } catch (e) { }
        });

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

            if (name === 'esp') {
                const mode = parts.length > 1 ? parseInt(parts[1], 10) : null;
                toggleEsp(mode);
                return;
            }

            if (name === 'quit' || name === 'exit' || name === 'q') {
                quitGame();
                return;
            }

            // Отправка команды на сервер
            alt.emitServer('flovmp:chat:say', '/' + cmd);
        });

        consoleView.on('flovmp:console:hotreload', () => {
            alt.log('[FloV:MP] NUI Hot-Reload requested via F8 console');
            if (chatView) chatView.reload(true);
            if (consoleView) {
                consoleView.reload(true);
                consoleView.emit('flovmp:console:log', 'RELOAD', 'Все активные WebViews перезагружены.');
            }
        });

        consoleView.on('flovmp:console:quit', () => {
            quitGame();
        });

        consoleView.on('flovmp:console:close', () => {
            closeDevConsole();
        });
    }
    return consoleView;
}

function startConsoleStats() {
    if (consoleStatsInterval) alt.clearInterval(consoleStatsInterval);
    consoleStatsInterval = alt.setInterval(() => {
        if (!consoleView || !consoleOpen) return;
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

export function openDevConsole() {
    if (consoleOpen) return;
    if (isVoiceTalking) {
        isVoiceTalking = false;
        if (voiceReleaseTimeout) {
            alt.clearTimeout(voiceReleaseTimeout);
            voiceReleaseTimeout = null;
        }
        try {
            if (typeof alt.setMicGain === 'function') alt.setMicGain(0.0);
            alt.emit('flovmp:voice:active', false);
        } catch (e) { }
    }
    if (chatTyping && chatView) {
        chatView.emit('flovmp:chat:closeinput');
        chatTyping = false;
        popCursor();
        try { chatView.unfocus(); } catch (e) { }
    }
    const cv = getOrCreateDevConsole();
    consoleOpen = true;
    cv.emit('flovmp:console:open');
    cv.emit('flovmp:console:permissions', currentAdminLevel);
    cv.focus();
    pushCursor();
    alt.toggleGameControls(false);
    startConsoleStats();
}

export function closeDevConsole() {
    if (!consoleOpen) return;
    consoleOpen = false;
    if (consoleStatsInterval) {
        alt.clearInterval(consoleStatsInterval);
        consoleStatsInterval = null;
    }
    if (consoleView) {
        consoleView.emit('flovmp:console:close');
        try { consoleView.unfocus(); } catch (e) { }
    }
    popCursor();
    if (!chatTyping) {
        alt.toggleGameControls(true);
    }
}

export function toggleDevConsole() {
    if (consoleOpen) closeDevConsole();
    else openDevConsole();
}

export function destroyDevConsole() {
    closeDevConsole();
    if (consoleView) {
        consoleView.destroy();
        consoleView = null;
    }
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
    loadCollisionAndTeleport(coords.x, coords.y);
}

function loadCollisionAndTeleport(x, y) {
    const player = alt.Player.local;
    if (!player || !player.valid) return;

    try { native.doScreenFadeOut(100); } catch (e) { }

    native.freezeEntityPosition(player.scriptID, true);
    native.loadScene(x, y, 100.0);
    native.requestCollisionAtCoord(x, y, 100.0);
    native.setFocusPosAndVel(x, y, 100.0, 0, 0, 0);

    let attempts = 0;
    const interval = alt.setInterval(() => {
        attempts++;
        if (!player || !player.valid) {
            alt.clearInterval(interval);
            native.clearFocus();
            try { native.doScreenFadeIn(300); } catch (e) { }
            return;
        }

        native.requestCollisionAtCoord(x, y, 100.0);
        const [found, groundZ] = native.getGroundZFor3dCoord(x, y, 800.0, 0, false);

        if ((found && Number.isFinite(groundZ)) || attempts >= 25) {
            alt.clearInterval(interval);
            native.clearFocus();
            const finalZ = (found && Number.isFinite(groundZ)) ? groundZ + 1.0 : 50.0;
            alt.emitServer('starter:teleportWaypoint', x, y, finalZ);
            native.setEntityCoordsNoOffset(player.scriptID, x, y, finalZ, false, false, false);
            native.freezeEntityPosition(player.scriptID, false);
            native.setEntityVelocity(player.scriptID, 0, 0, 0);
            try { native.doScreenFadeIn(300); } catch (e) { }
            if (consoleView) {
                consoleView.emit('flovmp:console:log', 'DEV', `Телепорт по метке: ${x.toFixed(1)}, ${y.toFixed(1)}, ${finalZ.toFixed(1)}`);
            }
        }
    }, 100);
}

// =============================================================================
// 5. ПЛАВНАЯ ПРОГРУЗКА КОЛЛИЗИЙ И СПАВН (Защита от Shift+W багов)
// =============================================================================
let activeSpawnInterval = null;
let lastSpawnTime = 0;
let lastSpawnCoord = null;

function loadCollisionAndUnfreeze(targetPos) {
    const player = alt.Player.local;
    if (!player || !player.valid) return;

    const now = Date.now();
    // Идемпотентность: если за последние 3 секунды спавн уже запущен в этой же точке (<= 5м), игнорируем дубликат
    if (lastSpawnCoord && (now - lastSpawnTime < 3000)) {
        const dx = targetPos.x - lastSpawnCoord.x;
        const dy = targetPos.y - lastSpawnCoord.y;
        const dz = targetPos.z - lastSpawnCoord.z;
        if ((dx * dx + dy * dy + dz * dz) < 25.0) {
            alt.log('[FloV:MP] Пропуск повторного вызова loadCollisionAndUnfreeze (спавн уже выполняется)');
            return;
        }
    }

    lastSpawnTime = now;
    lastSpawnCoord = { x: targetPos.x, y: targetPos.y, z: targetPos.z };

    if (activeSpawnInterval) {
        alt.clearInterval(activeSpawnInterval);
        activeSpawnInterval = null;
    }

    try { native.doScreenFadeOut(0); } catch (e) { }

    native.freezeEntityPosition(player.scriptID, true);
    native.loadScene(targetPos.x, targetPos.y, targetPos.z);
    native.requestCollisionAtCoord(targetPos.x, targetPos.y, targetPos.z);
    native.setFocusPosAndVel(targetPos.x, targetPos.y, targetPos.z, 0, 0, 0);

    let attempts = 0;
    activeSpawnInterval = alt.setInterval(() => {
        attempts++;
        if (!player || !player.valid) {
            if (activeSpawnInterval) {
                alt.clearInterval(activeSpawnInterval);
                activeSpawnInterval = null;
            }
            native.clearFocus();
            try { native.doScreenFadeIn(500); } catch (e) { }
            return;
        }

        native.requestCollisionAtCoord(targetPos.x, targetPos.y, targetPos.z);
        const [hasGround, groundZ] = native.getGroundZFor3dCoord(targetPos.x, targetPos.y, targetPos.z + 15.0, 0, false);
        const collisionLoaded = native.hasCollisionLoadedAroundEntity(player.scriptID);

        if ((hasGround && collisionLoaded) || attempts >= 35) {
            if (activeSpawnInterval) {
                alt.clearInterval(activeSpawnInterval);
                activeSpawnInterval = null;
            }
            native.clearFocus();
            const spawnZ = (hasGround && Number.isFinite(groundZ) && Math.abs(groundZ - targetPos.z) < 30.0)
                ? groundZ + 1.0
                : targetPos.z;
            native.setEntityCoordsNoOffset(player.scriptID, targetPos.x, targetPos.y, spawnZ, false, false, false);
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
            try { native.doScreenFadeIn(600); } catch (e) { }
            alt.log(`[FloV:MP] Спавн завершен. Коллизия загружена (попыток: ${attempts}, spawnZ: ${spawnZ.toFixed(2)})`);
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
        if (!chatTyping && !consoleOpen && !inVehicle) {
            handleVoiceKeyDown();
        }
    }
});

alt.on('keyup', (key) => {
    // F1 / F8 / F11 — Консоль / Меню управления и выхода
    if (key === KEYBINDS.help || key === KEYBINDS.console || key === KEYBINDS.consoleAlt) {
        toggleDevConsole();
        return;
    }
    if (key === 27) { // Escape
        if (consoleOpen) {
            closeDevConsole();
            return;
        }
    }
    if (consoleOpen) return;

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

    // F3 — ESP (Wallhack / Информационные теги)
    if (key === KEYBINDS.esp) {
        toggleEsp();
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
    getOrCreateDevConsole(); // Прогрев WebView консоли для мгновенного отклика (0мс)
    alt.emitServer('flovmp:client:ready');
});

alt.on('disconnect', () => {
    if (activeSpawnInterval) {
        alt.clearInterval(activeSpawnInterval);
        activeSpawnInterval = null;
    }
    if (voiceReleaseTimeout) {
        alt.clearTimeout(voiceReleaseTimeout);
        voiceReleaseTimeout = null;
    }
    isVoiceTalking = false;
    if (noClip) toggleNoClip();
    espMode = 0;
    cursorDepth = 0;
    try { alt.showCursor(false); } catch (e) { }
    try { alt.toggleGameControls(true); } catch (e) { }
    closeChat();
    destroyDevConsole();
    inGame = false;
    alt.log('[FloV:MP] Отключено от сервера');
});

// Инициализация чистого стартера
alt.onServer('starter:initClient', (x, y, z) => {
    alt.log('[FloV:MP] Игровой клиент FloV:MP активирован');
    inGame = true;
    native.displayRadar(true);
    native.displayHud(true);
    openChat();
    getOrCreateDevConsole();

    if (x !== undefined && y !== undefined && z !== undefined && Number.isFinite(Number(x))) {
        loadCollisionAndUnfreeze(new alt.Vector3(Number(x), Number(y), Number(z)));
    } else {
        const player = alt.Player.local;
        if (player && player.valid) {
            loadCollisionAndUnfreeze(player.pos);
        }
    }
});

alt.onServer('flovmp:client:welcome', (name, index, x, y, z) => {
    alt.log(`[FloV:MP] Добро пожаловать, ${name}!`);
    inGame = true;
    if (x !== undefined && y !== undefined && z !== undefined && Number.isFinite(Number(x))) {
        loadCollisionAndUnfreeze(new alt.Vector3(Number(x), Number(y), Number(z)));
    } else {
        const player = alt.Player.local;
        if (player && player.valid) {
            loadCollisionAndUnfreeze(player.pos);
        }
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

alt.onServer('flovmp:admin:toggleEsp', (targetMode) => {
    toggleEsp(targetMode !== undefined ? targetMode : null);
});

alt.onServer('starter:setGodMode', (enabled) => {
    godMode = !!enabled;
    const local = alt.Player.local;
    if (local && local.valid) {
        try {
            native.setEntityInvincible(local.scriptID, godMode);
            native.setPlayerInvincible(local.scriptID, godMode);
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
