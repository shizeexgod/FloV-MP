/**
 * FloV:MP — клиентская точка входа (js-module / alt-client).
 *
 * Фаза 3 (каркас): экран авторизации.
 *   - сервер шлёт flovmp:auth:show → показываем NUI логина/регистрации,
 *     курсор, блок управления, статичная камера;
 *   - NUI шлёт flovmp:auth:submit {mode,user,pass} → пересылаем на сервер;
 *   - сервер шлёт flovmp:auth:result {ok,msg} → отдаём в NUI;
 *   - сервер шлёт flovmp:auth:hide → убираем NUI, возвращаем управление.
 *
 * До входа сервер игрока не спавнит — экран чёрный, поверх него NUI.
 */

import * as alt from 'alt-client';
import * as native from 'natives';

alt.log('[FloV:MP] client: ресурс flovmp-client загружен');

let authView = null;
let authCamera = null;
let hudView = null;
let invView = null;
let lastInvSync = null;
let inGame = false;
let chatView = null;
let chatTyping = false;

function openAuth() {
    if (authView) return;

    // статичная камера над Los Santos, чтобы не смотреть в никуда
    try {
        authCamera = native.createCamWithParams(
            'DEFAULT_SCRIPTED_CAMERA',
            -365.0, -130.0, 120.0, -10.0, 0.0, 240.0, 55.0, false, 2);
        native.setCamActive(authCamera, true);
        native.renderScriptCams(true, false, 0, true, false, 0);
    } catch (err) {
        alt.log('[FloV:MP] client: камера логина недоступна: ' + err);
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
    try {
        alt.showCursor(false);
    } catch (e) { /* ignore */ }
    alt.toggleGameControls(true);

    try {
        native.renderScriptCams(false, false, 0, true, false, 0);
        if (authCamera) {
            native.destroyCam(authCamera, false);
            authCamera = null;
        }
    } catch (err) {
        alt.log('[FloV:MP] client: сброс камеры: ' + err);
    }
}

function openHud() {
    if (hudView) return;
    hudView = new alt.WebView('http://resource/client/html/hud/index.html');
}

function closeHud() {
    if (hudView) {
        hudView.destroy();
        hudView = null;
    }
}

function openInventory() {
    if (invView || !inGame) return;
    invView = new alt.WebView('http://resource/client/html/inventory/index.html');
    invView.focus();
    alt.showCursor(true);
    alt.toggleGameControls(false);

    if (lastInvSync) invView.emit('flovmp:inv:sync', lastInvSync);

    invView.on('flovmp:inv:move', (from, to) => alt.emitServer('flovmp:inv:move', from | 0, to | 0));
    invView.on('flovmp:inv:use', (slot) => alt.emitServer('flovmp:inv:use', slot | 0));
    invView.on('flovmp:inv:drop', (slot, qty) => alt.emitServer('flovmp:inv:drop', slot | 0, qty | 0));
}

function closeInventory() {
    if (!invView) return;
    invView.destroy();
    invView = null;
    try { alt.showCursor(false); } catch (e) { /* ignore */ }
    alt.toggleGameControls(true);
}

function toggleInventory() {
    if (invView) closeInventory();
    else openInventory();
}

function openChat() {
    if (chatView) return;
    chatView = new alt.WebView('http://resource/client/html/chat/index.html');
    chatView.on('flovmp:chat:say', (text) => alt.emitServer('flovmp:chat:say', String(text)));
    chatView.on('flovmp:chat:done', () => {
        chatTyping = false;
        try { chatView.unfocus(); } catch (e) { /* ignore */ }
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
    if (!chatView || chatTyping || !inGame || invView) return;
    chatTyping = true;
    chatView.focus();
    alt.toggleGameControls(false);
    chatView.emit('flovmp:chat:openinput');
}

alt.onServer('flovmp:auth:show', openAuth);
alt.onServer('flovmp:auth:hide', () => {
    closeAuth();
    openHud();
    openChat();
    inGame = true;
});

alt.onServer('flovmp:chat:msg', (kind, author, text) => {
    if (chatView) chatView.emit('flovmp:chat:msg', kind, author, text);
});

alt.onServer('flovmp:inv:sync', (json) => {
    lastInvSync = json;
    if (invView) invView.emit('flovmp:inv:sync', json);
});

alt.onServer('flovmp:inv:notice', (text) => {
    if (invView) invView.emit('flovmp:inv:notice', text);
});

// I — инвентарь, T — чат
alt.on('keyup', (key) => {
    if (chatTyping) return;
    if (key === 73) toggleInventory();
    else if (key === 84) startTyping();
});

alt.onServer('flovmp:auth:result', (ok, message) => {
    if (authView) authView.emit('flovmp:auth:result', ok, message);
});

alt.onServer('flovmp:hud:init', (serverName) => {
    if (hudView) hudView.emit('flovmp:hud:init', serverName);
});

alt.onServer('flovmp:hud:tick', (hp, armor, cash, online, hour, minute) => {
    if (hudView) hudView.emit('flovmp:hud:tick', hp, armor, cash, online, hour, minute);
});

alt.on('connectionComplete', () => {
    alt.log('[FloV:MP] client: connectionComplete — вошли на сервер');
    // сообщаем серверу, что все обработчики навешены и можно показывать логин
    alt.emitServer('flovmp:client:ready');
});

alt.on('disconnect', () => {
    closeAuth();
    closeHud();
    closeInventory();
    closeChat();
    inGame = false;
    alt.log('[FloV:MP] client: disconnect');
});

alt.onServer('flovmp:client:welcome', (name, index) => {
    alt.log(`[FloV:MP] client: welcome "${name}", точка спавна #${index}`);
    try {
        native.beginTextCommandThefeedPost('STRING');
        native.addTextComponentSubstringPlayerName(`FloV:MP — добро пожаловать, ${name}`);
        native.endTextCommandThefeedPostTicker(false, true);
    } catch (err) {
        alt.log(`[FloV:MP] client: нативное уведомление недоступно: ${err}`);
    }
});
