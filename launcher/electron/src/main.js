'use strict';

const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('node:path');
const { NativeBridge } = require('./native-bridge');

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
}

// ─── Window chrome (кастомный титлбар в renderer) ─────────────────────────
ipcMain.handle('window:minimize', () => mainWindow?.minimize());
ipcMain.handle('window:toggleMaximize', () => {
  if (!mainWindow) return;
  if (mainWindow.isMaximized()) mainWindow.unmaximize();
  else mainWindow.maximize();
});
ipcMain.handle('window:close', () => mainWindow?.close());

// ─── Мост к нативной логике (реестр / запуск игры) ─────────────────────────
ipcMain.handle('native:getSettings', () => native.call('getSettings'));
ipcMain.handle('native:saveSettings', (_e, data) => native.call('saveSettings', { data }));
ipcMain.handle('native:detectGta', () => native.call('detectGta'));
ipcMain.handle('native:validateGta', (_e, gtaPath) => native.call('validateGta', { path: gtaPath }));
ipcMain.handle('native:browseFolder', () => native.call('browseFolder'));
ipcMain.handle('native:serverStatus', (_e, host, port) => native.call('serverStatus', { host, port }));
ipcMain.handle('native:play', (_e, gtaPath, host, port, nickname) =>
  native.call('play', { gtaPath, host, port, nickname }));
ipcMain.handle('native:cancelPlay', () => native.call('cancelPlay').catch(() => null));

// Нативный слой (FloVMP.Connect через C#-помощник) шлёт события прогресса
// загрузки строкой NDJSON {"event":"download","downloaded":..,"total":..,
// "speed":..,"percent":..,"phase":".."}. NativeBridge эмитит их как 'event'.
native.on?.('download', (data) => {
  mainWindow?.webContents.send('download:progress', data);
});

// ─── Автозапуск с Windows — настоящая системная настройка (не просто чекбокс),
// через встроенный Electron API поверх реестра Run/Startup, ничего своего
// в реестр не пишем напрямую (та же дисциплина, что и для BattlEye — не
// трогать системные вещи руками там, где есть официальный API).
ipcMain.handle('native:setAutostart', (_e, enabled) => {
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

app.on('before-quit', () => native.stop());
