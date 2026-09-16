// Проверка безопасности авторизации лаунчера.
//
// main.js — главный процесс Electron, в обычном Node он не запускается. Здесь
// модуль 'electron' и NativeBridge подменяются заглушками, настоящий main.js
// загружается как есть, и вызывается его реальный обработчик native:auth.
//
// Сеть подменяется через global.fetch: так проверяется и то, КУДА лаунчер
// пытается отправить пароль, и то, что он отвечает, когда сервер недоступен.
//
// Запуск:  node launcher/electron/tests/auth-security.test.cjs

'use strict';

const path = require('node:path');
const fs = require('node:fs');
const os = require('node:os');
const Module = require('node:module');

let passes = 0;
let failures = 0;
function check(ok, name, detail) {
  if (ok) { passes++; console.log(`  [PASS] ${name}`); }
  else { failures++; console.log(`  [FAIL] ${name}${detail ? ' — ' + detail : ''}`); }
}
function section(t) { console.log(`\n=== ${t} ===`); }

// --- заглушки ---------------------------------------------------------------
const handlers = {};
const electronStub = {
  app: {
    commandLine: { appendSwitch() {} },
    requestSingleInstanceLock: () => true,
    on() {}, whenReady: () => new Promise(() => {}), quit() {},
    getPath: () => os.tmpdir(), setLoginItemSettings() {}, getLoginItemSettings: () => ({}),
    isPackaged: false,
  },
  BrowserWindow: function () {},
  ipcMain: { handle: (name, fn) => { handlers[name] = fn; }, on() {} },
  Tray: function () {},
  Menu: { buildFromTemplate: () => ({}) },
  nativeImage: { createFromPath: () => ({}) },
};

class NativeBridgeStub {
  call() { return Promise.resolve(null); }
  on() {}
  stop() {}
}

// Отдельная папка для session.json, чтобы тест не трогал настоящий профиль.
const sandbox = fs.mkdtempSync(path.join(os.tmpdir(), 'flovmp-launcher-test-'));
process.env.LOCALAPPDATA = sandbox;

const origLoad = Module._load;
Module._load = function (request, parent, isMain) {
  if (request === 'electron') return electronStub;
  if (request === './native-bridge' || request.endsWith('native-bridge')) {
    return { NativeBridge: NativeBridgeStub };
  }
  return origLoad.apply(this, arguments);
};

// --- сеть -------------------------------------------------------------------
let requested = [];
let networkMode = 'down'; // 'down' | 'up'

global.fetch = async (url, opts) => {
  requested.push({ url, body: opts && opts.body });
  if (networkMode === 'down') throw new Error('ECONNREFUSED');
  return { text: async () => JSON.stringify({ ok: true, username: 'Ivan', message: 'ок' }) };
};

// --- загрузка настоящего main.js -------------------------------------------
require(path.join(__dirname, '..', 'src', 'main.js'));
const auth = handlers['native:auth'];

async function run() {
  section('Сервер авторизации недоступен');

  check(typeof auth === 'function', 'обработчик native:auth зарегистрирован');

  requested = [];
  networkMode = 'down';
  const sessionFile = path.join(sandbox, 'FloridaV', 'session.json');
  try { fs.unlinkSync(sessionFile); } catch {}

  const res = await auth({}, 'login', {
    username: 'Victim', password: 'ЛюбойНеверныйПароль', serverHost: '203.0.113.10',
  });

  check(res && res.ok === false, 'неверный пароль НЕ принимается при недоступном сервере',
        'раньше лаунчер отвечал «Вход выполнен» для любого пароля');
  check(!fs.existsSync(sessionFile), 'поддельная сессия не записывается',
        'иначе лаунчер считал бы игрока вошедшим под чужим ником');
  check(res && typeof res.message === 'string' && res.message.includes('без входа'),
        'игроку объясняется, что играть можно и без входа');

  check(!requested.some(r => r.url.includes('127.0.0.1') || r.url.includes('localhost')),
        'пароль НЕ уходит на localhost при удалённом сервере',
        'раньше уходил на 127.0.0.1:7799 — любому приложению на машине игрока');
  check(requested.every(r => r.url.includes('203.0.113.10')),
        'пароль уходит только на тот сервер, в который игрок входит');

  section('Смена пароля при недоступном сервере');
  requested = [];
  const cp = await auth({}, 'change-password', {
    username: 'Victim', password: 'old', newPassword: 'new', serverHost: '203.0.113.10',
  });
  check(cp && cp.ok === false, 'смена пароля НЕ сообщает об успехе, когда ничего не сохранено',
        'раньше игроку говорили «пароль изменён», хотя сервер был недоступен');
  check(cp && cp.message && cp.message.includes('не сохранены'), 'сообщение говорит, что изменения не сохранены');

  section('Локальный сервер (разработка)');
  requested = [];
  await auth({}, 'login', { username: 'Dev', password: 'x', serverHost: '127.0.0.1' });
  check(requested.length > 0 && requested.every(r => r.url.includes('127.0.0.1:7799')),
        'для локального сервера используется localhost — так и задумано');

  section('Сервер доступен');
  requested = [];
  networkMode = 'up';
  const ok = await auth({}, 'login', { username: 'Ivan', password: 'right', serverHost: '203.0.113.10' });
  check(ok && ok.ok === true, 'при доступном сервере ответ сервера проходит как есть');
  check(requested.length === 1, 'после первого успешного ответа других адресов не пробуем',
        `запросов: ${requested.length}`);

  section('Конфиг сборки');
  check(typeof handlers['native:buildConfig'] === 'function', 'конфиг сборки доступен интерфейсу');
  const cfg = await handlers['native:buildConfig']();
  check(cfg && typeof cfg === 'object', 'без launcher.config.json возвращается пустой конфиг, не ошибка');

  console.log(`\n${'='.repeat(62)}`);
  console.log(`Пройдено: ${passes}, провалов: ${failures}`);

  try { fs.rmSync(sandbox, { recursive: true, force: true }); } catch {}
  process.exit(failures > 0 ? 1 : 0);
}

run().catch((e) => {
  console.error('Тест упал:', e);
  process.exit(1);
});
