'use strict';

const { app, BrowserWindow, ipcMain, Tray, Menu, nativeImage } = require('electron');
const path = require('node:path');
const fs = require('node:fs');
const os = require('node:os');
const { NativeBridge } = require('./native-bridge');

let tray = null;
let trayOnClose = false;   // из настроек: крестик → в трей вместо закрытия
let quitting = false;      // true во время реального выхода (before-quit)

// Общая с игрой папка (та же, что и settings.json). session.json пишет
// сервер при входе в игре — лаунчер его читает для хэндоффа аккаунта.
const SHARED_DIR = path.join(process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local'), 'FloridaV');
const SESSION_FILE = path.join(SHARED_DIR, 'session.json');
const AUTH_API = 'http://127.0.0.1:7799/api/auth';

// mode лаунчера → путь эндпоинта ServerLauncher. Пока сервер знает только
// register/login; остальные вернут не-ok, и UI честно это покажет.
const AUTH_ROUTES = {
  login: 'login',
  register: 'register',
  'change-password': 'change-password',
  'change-email': 'change-email',
  '2fa-enable': '2fa/enable',
  '2fa-disable': '2fa/disable',
};

const native = new NativeBridge();
let mainWindow = null;
let splashWindow = null;

// Показываем сразу после запуска .exe, пока грузится нативный помощник и
// первый рендер главного окна — как заставка "Проверка обновлений" у
// Majestic, только честная: время показа = реальное время загрузки
// (плюс небольшой минимум, чтобы не мигало на быстрых машинах), а не
// искусственная задержка.
const SPLASH_MIN_MS = process.env.FLOVMP_SPLASH_MIN ? parseInt(process.env.FLOVMP_SPLASH_MIN, 10) : 700;
function createSplash() {
  splashWindow = new BrowserWindow({
    width: 380,
    height: 280,
    frame: false,
    transparent: true,
    backgroundColor: '#00000000',
    resizable: false,
    center: true,
    show: true,
    skipTaskbar: true,
    webPreferences: { sandbox: true },
  });
  splashWindow.removeMenu();
  splashWindow.loadFile(path.join(__dirname, 'renderer', 'splash.html'));
}

function createWindow() {
  const splashShownAt = Date.now();
  mainWindow = new BrowserWindow({
    width: 1180,
    height: 720,
    minWidth: 960,
    minHeight: 600,
    center: true,
    frame: false,
    transparent: true,
    backgroundColor: '#00000000',
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  mainWindow.webContents.on('console-message', (_e, _level, message) => {
    console.log('[renderer]', message);
  });

  mainWindow.removeMenu();
  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));

  mainWindow.once('ready-to-show', () => {
    const elapsed = Date.now() - splashShownAt;
    const wait = Math.max(0, SPLASH_MIN_MS - elapsed);
    setTimeout(() => {
      if (splashWindow && !splashWindow.isDestroyed()) splashWindow.close();
      splashWindow = null;
      mainWindow.show();
      mainWindow.focus();
    }, wait);
  });

  mainWindow.on('maximize', () => mainWindow.webContents.send('window:state', 'maximized'));
  mainWindow.on('unmaximize', () => mainWindow.webContents.send('window:state', 'normal'));

  // Крестик → в трей, если так настроено (и это не реальный выход).
  mainWindow.on('close', (e) => {
    if (trayOnClose && !quitting) {
      e.preventDefault();
      mainWindow.hide();
      ensureTray();
    }
  });
}

function ensureTray() {
  if (tray) return;
  let img;
  try {
    img = nativeImage.createFromPath(path.join(__dirname, 'renderer', 'assets', 'logo.png'));
    if (!img.isEmpty()) img = img.resize({ width: 16, height: 16 });
  } catch { img = nativeImage.createEmpty(); }
  tray = new Tray(img);
  tray.setToolTip('Держава RP Launcher');
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Открыть', click: () => { mainWindow?.show(); mainWindow?.focus(); } },
    { type: 'separator' },
    { label: 'Выход', click: () => { quitting = true; app.quit(); } },
  ]));
  tray.on('click', () => { mainWindow?.show(); mainWindow?.focus(); });
}

// ─── Window chrome (кастомный титлбар в renderer) ─────────────────────────
ipcMain.handle('window:minimize', () => mainWindow?.minimize());
ipcMain.handle('window:toggleMaximize', () => {
  if (!mainWindow) return;
  if (mainWindow.isMaximized()) mainWindow.unmaximize();
  else mainWindow.maximize();
});
ipcMain.handle('window:close', () => mainWindow?.close());
ipcMain.handle('window:setTrayOnClose', (_e, enabled) => {
  trayOnClose = !!enabled;
  if (!trayOnClose && tray) { tray.destroy(); tray = null; }
  return trayOnClose;
});

// ─── Мост к нативной логике (реестр / запуск игры) ─────────────────────────
ipcMain.handle('native:getSettings', () => native.call('getSettings'));
ipcMain.handle('native:saveSettings', (_e, data) => native.call('saveSettings', { data }));
ipcMain.handle('native:detectGta', () => native.call('detectGta'));
ipcMain.handle('native:validateGta', (_e, gtaPath) => native.call('validateGta', { path: gtaPath }));
ipcMain.handle('native:browseFolder', () => native.call('browseFolder'));
ipcMain.handle('native:serverStatus', (_e, host, port) => native.call('serverStatus', { host, port }));
ipcMain.handle('native:play', (_e, gtaPath, host, port, nickname) => {
  const numericPort = parseInt(port, 10) || 7788;
  return native.call('play', { gtaPath, host, port: numericPort, nickname });
});
ipcMain.handle('native:cancelPlay', () => native.call('cancelPlay').catch(() => null));
ipcMain.handle('native:deviceInfo', () => native.call('deviceInfo').catch(() => null));
ipcMain.handle('native:detectGpu', () => native.call('detectGpu').catch(() => null));
ipcMain.handle('native:deployUpscaler', (_e, gtaPath) => native.call('deployUpscaler', { gtaPath }).catch(() => null));
ipcMain.handle('native:cleanupUpscaler', (_e, gtaPath) => native.call('cleanupUpscaler', { gtaPath }).catch(() => null));

// Авторизация / безопасность — HTTP к локальному ServerLauncher (тот же
// accounts.json, что и в игре). fetch есть в Node начиная с 18 — отдельный
// http-клиент не нужен.
ipcMain.handle('native:auth', async (_e, mode, payload) => {
  const route = AUTH_ROUTES[mode];
  if (!route) return { ok: false, message: 'неизвестная операция' };
  const host = (payload && payload.serverHost) || process.env.FLOVMP_SERVER_HOST || '188.127.229.224';
  const apiBase = (host === '127.0.0.1' || host === 'localhost')
    ? 'http://127.0.0.1:7799/api/auth'
    : `http://${host}/api/auth`;
  try {
    const res = await fetch(`${apiBase}/${route}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload || {}),
      signal: AbortSignal.timeout(8000),
    });
    const text = await res.text();
    try { return JSON.parse(text); }
    catch { return { ok: false, message: res.ok ? 'некорректный ответ сервера' : `ошибка сервера (${res.status})` }; }
  } catch {
    return { ok: false, message: 'сервер недоступен' };
  }
});

ipcMain.handle('native:readSession', () => {
  try {
    const raw = fs.readFileSync(SESSION_FILE, 'utf8');
    const s = JSON.parse(raw);
    return s && typeof s.username === 'string' && s.username ? s : null;
  } catch { return null; }
});
ipcMain.handle('native:clearSession', () => {
  try { fs.unlinkSync(SESSION_FILE); } catch {}
  return true;
});

// Нативный слой (FloVMP.Connect через C#-помощник) шлёт события прогресса
// загрузки строкой NDJSON {"event":"download","downloaded":..,"total":..,
// "speed":..,"percent":..,"phase":".."}. NativeBridge эмитит их как 'event'.
native.on?.('download', (data) => {
  mainWindow?.webContents.send('download:progress', data);
});

// ─── Движок клиента FloV:MP (Фаза 2 CDN-раздача) ──────────────────────────
// Лаунчер тонкий: сам движок (~430 МБ: libce2/CEF/flovmp-client) не вшит в
// инсталлятор, а качается один раз с CDN сервера в
// %LOCALAPPDATA%\FloridaV\engine\ и проверяется по sha256 из манифеста.
// FloVMP.Connect подхватывает его оттуда (см. PlayService.FindClientDir).
const ENGINE_DIR = path.join(SHARED_DIR, 'engine');
const ENGINE_MARKER = path.join(ENGINE_DIR, '.flovmp-engine.json');
const DEFAULT_CDN = process.env.FLOVMP_CDN || 'http://188.127.229.224/cdn';

function readEngineMarker() {
  try { return JSON.parse(fs.readFileSync(ENGINE_MARKER, 'utf8')); } catch { return null; }
}
ipcMain.handle('native:engineStatus', async (_e, cdnBase) => {
  const marker = readEngineMarker();
  const entry = marker?.entry || 'flovmp.exe';
  const entryOk = fs.existsSync(path.join(ENGINE_DIR, entry))
    || fs.existsSync(path.join(ENGINE_DIR, 'flovmp.exe'))
    || fs.existsSync(path.join(ENGINE_DIR, 'altv.exe'));
  let latest = null;
  try {
    const base = (cdnBase || DEFAULT_CDN).replace(/\/+$/, '');
    const res = await fetch(`${base}/engine/engine-manifest.json`, { signal: AbortSignal.timeout(6000) });
    if (res.ok) latest = await res.json();
  } catch {}
  return {
    installed: !!marker && entryOk,
    version: marker?.version || null,
    latestVersion: latest?.version || null,
    upToDate: !!marker && entryOk && (!latest || marker.version === latest.version),
    sizeBytes: latest?.archive?.size || latest?.totalBytes || 0,
  };
});

ipcMain.handle('native:downloadEngine', async (_e, cdnBase) => {
  const base = (cdnBase || DEFAULT_CDN).replace(/\/+$/, '');
  const send = (d) => mainWindow?.webContents.send('download:progress', d);
  try {
    send({ phase: 'Проверка версии движка…', percent: 0, downloaded: 0, total: 0, speed: 0 });
    const man = await fetch(`${base}/engine/engine-manifest.json`, { signal: AbortSignal.timeout(10000) })
      .then((r) => { if (!r.ok) throw new Error(`манифест ${r.status}`); return r.json(); });

    const marker = readEngineMarker();
    if (marker && marker.version === man.version
        && fs.existsSync(path.join(ENGINE_DIR, man.entry || 'flovmp.exe'))) {
      send({ phase: 'Движок актуален', percent: 100, done: true });
      return { ok: true, upToDate: true, version: man.version };
    }

    const arc = man.archive || {};
    const total = arc.size || man.totalBytes || 0;
    const tmp = path.join(SHARED_DIR, 'engine-download.tgz');
    fs.mkdirSync(SHARED_DIR, { recursive: true });

    send({ phase: 'Загрузка движка', percent: 0, downloaded: 0, total, speed: 0 });
    const res = await fetch(`${base}/engine/${arc.name || 'engine.tgz'}`);
    if (!res.ok || !res.body) throw new Error(`архив ${res.status}`);

    const out = fs.createWriteStream(tmp);
    const hash = require('node:crypto').createHash('sha256');
    let got = 0; const started = Date.now(); let lastTick = started;
    for await (const chunk of res.body) {
      out.write(chunk); hash.update(chunk); got += chunk.length;
      const now = Date.now();
      if (now - lastTick > 250) {
        lastTick = now;
        send({
          phase: 'Загрузка движка',
          percent: total ? (got / total) * 90 : 0,
          downloaded: got, total,
          speed: got / Math.max(0.001, (now - started) / 1000),
        });
      }
    }
    await new Promise((r) => out.end(r));

    const digest = hash.digest('hex');
    if (arc.sha256 && digest !== arc.sha256) {
      fs.unlinkSync(tmp);
      throw new Error('битый архив (sha256 не совпал)');
    }

    send({ phase: 'Распаковка движка…', percent: 93, downloaded: got, total });
    fs.rmSync(ENGINE_DIR, { recursive: true, force: true });
    fs.mkdirSync(ENGINE_DIR, { recursive: true });
    // bsdtar есть в Windows 10 1803+ / 11 (C:\Windows\System32\tar.exe)
    await new Promise((resolve, reject) => {
      const { execFile } = require('node:child_process');
      execFile('tar', ['-xzf', tmp, '-C', ENGINE_DIR], (err) => err ? reject(err) : resolve());
    });
    fs.unlinkSync(tmp);

    fs.writeFileSync(ENGINE_MARKER, JSON.stringify({
      version: man.version, entry: man.entry || 'flovmp.exe',
      installedUtc: new Date().toISOString(), sha256: digest,
    }, null, 1));

    send({ phase: 'Движок установлен', percent: 100, downloaded: total || got, total: total || got, done: true });
    return { ok: true, version: man.version };
  } catch (err) {
    send({ phase: `Ошибка загрузки движка: ${err.message}`, percent: 0, error: String(err.message), done: true });
    return { ok: false, error: String(err.message) };
  }
});

// ─── Автозапуск с Windows — настоящая системная настройка (не просто чекбокс),
// через встроенный Electron API поверх реестра Run/Startup, ничего своего
// в реестр не пишем напрямую (та же дисциплина, что и для BattlEye — не
// трогать системные вещи руками там, где есть официальный API).
ipcMain.handle('native:setAutostart', (_e, enabled) => {
  if (!app.isPackaged) {
    return !!enabled;
  }
  app.setLoginItemSettings({ openAtLogin: !!enabled, path: process.execPath });
  return app.getLoginItemSettings().openAtLogin;
});
ipcMain.handle('native:getAutostart', () => app.getLoginItemSettings().openAtLogin);

app.whenReady().then(() => {
  createSplash();
  try {
    native.start();
  } catch (err) {
    console.error('Не удалось запустить нативный помощник:', err.message);
  }
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  native.stop();
  if (process.platform !== 'darwin') app.quit();
});

app.on('before-quit', () => { quitting = true; native.stop(); });
