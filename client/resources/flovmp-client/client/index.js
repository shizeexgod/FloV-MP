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

alt.onServer('flovmp:auth:show', openAuth);
alt.onServer('flovmp:auth:hide', () => {
    closeAuth();
    openHud();
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
});

alt.on('disconnect', () => {
    closeAuth();
    closeHud();
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
