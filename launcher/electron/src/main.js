'use strict';

const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('node:path');
const { NativeBridge } = require('./native-bridge');

const native = new NativeBridge();
let mainWindow = null;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1180,
    height: 720,
    minWidth: 960,
    minHeight: 600,
    center: true,
    frame: false,
    transparent: true,
    backgroundColor: '#00000000',
    show: true,
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
  mainWindow.show();
  mainWindow.focus();

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
