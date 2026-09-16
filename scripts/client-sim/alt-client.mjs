// Заглушка модуля 'alt-client' для симуляции входа.
//
// Моделирует ровно то, от чего зависит процесс захода в игру: окна
// (WebView), события сервера и клиента, курсор, блокировку управления и
// таймеры. Таймеры виртуальные — сценарий двигает время сам через
// __advance, поэтому тест на 30-секундный таймаут идёт мгновенно.

let clientHandlers = {};
let serverHandlers = {};
let views = [];
let toServer = [];
let cursorShows = 0;
let controlsEnabled = true;
let now = 0;
let timers = [];
let timerSeq = 1;

function reset() {
    // Окна и таймеры прошлого сценария гасятся, но обработчики событий,
    // зарегистрированные скриптом при загрузке, остаются: скрипт грузится
    // один раз, как и в игре.
    views = [];
    toServer = [];
    cursorShows = 0;
    controlsEnabled = true;
    now = 0;
    timers = [];
}

export function __reset() {
    reset();
    // Сброс внутреннего состояния скрипта — через его же событие отключения.
    for (const fn of clientHandlers['disconnect'] || []) {
        try { fn(); } catch (e) { /* заглушка */ }
    }
    reset();
}

export function __fire(name, ...args) {
    for (const fn of clientHandlers[name] || []) fn(...args);
}

export function __server(name, ...args) {
    for (const fn of serverHandlers[name] || []) fn(...args);
}

export function __views() { return views.filter(v => !v.__destroyed); }
export function __toServer() { return toServer; }
export function __cursor() { return cursorShows > 0; }
export function __cursorDepthSane() { return cursorShows <= 1; }
export function __controls() { return controlsEnabled; }

export function __advance(ms) {
    const target = now + ms;
    // Обрабатываем таймеры по порядку наступления, включая интервалы и
    // таймеры, созданные внутри других таймеров.
    for (;;) {
        const due = timers
            .filter(t => !t.cancelled && t.at <= target)
            .sort((a, b) => a.at - b.at)[0];
        if (!due) break;
        now = due.at;
        if (due.interval) {
            due.at += due.interval;
        } else {
            due.cancelled = true;
        }
        due.fn();
    }
    now = target;
}

// --- API alt-client ---------------------------------------------------------

export function on(name, fn) {
    (clientHandlers[name] ||= []).push(fn);
}

export function onServer(name, fn) {
    (serverHandlers[name] ||= []).push(fn);
}

export function off() {}
export function offServer() {}

export function emitServer(name, ...args) {
    toServer.push([name, ...args]);
}

export function emit() {}

export function log() {}
export function logError() {}
export function logWarning() {}

export function showCursor(state) {
    cursorShows += state ? 1 : -1;
    if (cursorShows < 0) cursorShows = 0;
}

export function toggleGameControls(state) {
    controlsEnabled = !!state;
}

export function setTimeout(fn, ms) {
    const id = timerSeq++;
    timers.push({ id, fn, at: now + (ms || 0), cancelled: false });
    return id;
}

export function clearTimeout(id) {
    const t = timers.find(x => x.id === id);
    if (t) t.cancelled = true;
}

export function setInterval(fn, ms) {
    const id = timerSeq++;
    timers.push({ id, fn, at: now + (ms || 1), interval: ms || 1, cancelled: false });
    return id;
}

export function clearInterval(id) { clearTimeout(id); }

export function everyTick() { return 0; }
export function clearEveryTick() {}
export function nextTick(fn) { setTimeout(fn, 0); }

export function copyToClipboard() {}
export function isConsoleOpen() { return false; }
export function getLocale() { return 'ru'; }
export function hash(s) { return String(s).length; }
export function loadModel() {}
export function requestIpl() {}
export function removeIpl() {}

export class Vector3 {
    constructor(x = 0, y = 0, z = 0) { this.x = x; this.y = y; this.z = z; }
}

export class WebView {
    constructor(url) {
        this.url = url;
        this.__handlers = {};
        this.__emitted = [];
        this.__destroyed = false;
        this.focused = false;
        views.push(this);
    }
    on(name, fn) { (this.__handlers[name] ||= []).push(fn); }
    off() {}
    emit(name, ...args) { this.__emitted.push([name, ...args]); }
    focus() { this.focused = true; }
    unfocus() { this.focused = false; }
    destroy() { this.__destroyed = true; }
    __fire(name, ...args) {
        for (const fn of this.__handlers[name] || []) fn(...args);
    }
}

const localPlayer = {
    valid: true,
    scriptID: 1,
    pos: new Vector3(0, 0, 0),
    vehicle: null,
    health: 200,
};

export const Player = {
    local: localPlayer,
    all: [],
    getByID: () => null,
};

export const Vehicle = { all: [] };

export const Utils = {
    requestModel: async () => {},
    wait: async () => {},
};

export default {
    on, onServer, off, offServer, emitServer, emit, log, logError, logWarning,
    showCursor, toggleGameControls, setTimeout, clearTimeout, setInterval,
    clearInterval, everyTick, clearEveryTick, nextTick, copyToClipboard,
    isConsoleOpen, getLocale, hash, loadModel, requestIpl, removeIpl,
    Vector3, WebView, Player, Vehicle, Utils,
};
