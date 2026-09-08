'use strict';

const { contextBridge, ipcRenderer } = require('electron');

const api = {
  // Управление окном
  minimize: () => ipcRenderer.invoke('window:minimize'),
  toggleMaximize: () => ipcRenderer.invoke('window:toggleMaximize'),
  close: () => ipcRenderer.invoke('window:close'),
  onWindowState: (cb) => ipcRenderer.on('window:state', (_e, state) => cb(state)),
  setTrayOnClose: (enabled) => ipcRenderer.invoke('window:setTrayOnClose', enabled),

  // Настройки / данные лаунчера (через нативный C#-помощник)
  getSettings: () => ipcRenderer.invoke('native:getSettings'),
  saveSettings: (data) => ipcRenderer.invoke('native:saveSettings', data),
  detectGta: () => ipcRenderer.invoke('native:detectGta'),
  validateGta: (gtaPath) => ipcRenderer.invoke('native:validateGta', gtaPath),
  browseFolder: () => ipcRenderer.invoke('native:browseFolder'),
  serverStatus: (host, port) => ipcRenderer.invoke('native:serverStatus', host, port),
  play: (gtaPath, host, port, nickname) => ipcRenderer.invoke('native:play', gtaPath, host, port, nickname),
  cancelPlay: () => ipcRenderer.invoke('native:cancelPlay'),
  onDownloadProgress: (cb) => ipcRenderer.on('download:progress', (_e, data) => cb(data)),

  // Движок клиента FloV:MP — статус локальной установки и загрузка с CDN
  // (%LOCALAPPDATA%\FloridaV\engine\). Прогресс идёт тем же каналом
  // download:progress, что и загрузка файлов игры.
  engineStatus: (cdnBase) => ipcRenderer.invoke('native:engineStatus', cdnBase),
  downloadEngine: (cdnBase) => ipcRenderer.invoke('native:downloadEngine', cdnBase),

  // Реальные данные о текущем устройстве (для «Личного кабинета» → Устройства,
  // История входов). Возвращает { deviceId, hostname, userName, os, osArch,
  // localIp, bootTimeUtc, nowUtc } — только то, что система отдаёт локально.
  deviceInfo: () => ipcRenderer.invoke('native:deviceInfo'),

  // Аппаратный инспектор видеокарты и профили масштабирования (DLSS 5 / FSR 3)
  detectGpu: () => ipcRenderer.invoke('native:detectGpu'),
  deployUpscaler: (gtaPath) => ipcRenderer.invoke('native:deployUpscaler', gtaPath),
  cleanupUpscaler: (gtaPath) => ipcRenderer.invoke('native:cleanupUpscaler', gtaPath),

  // Авторизация / безопасность — один аккаунт для лаунчера и игры. mode:
  // 'login' | 'register' | 'change-password' | 'change-email' | '2fa-enable' |
  // '2fa-disable'. Возвращает { ok, message, username?, createdUtc?, ... }.
  auth: (mode, payload) => ipcRenderer.invoke('native:auth', mode, payload),

  // Хэндофф аккаунта из игры: session.json в %LOCALAPPDATA%\FloridaV\.
  readSession: () => ipcRenderer.invoke('native:readSession'),
  clearSession: () => ipcRenderer.invoke('native:clearSession'),

  // Автозапуск с Windows — настоящая системная настройка через Electron,
  // не просто галочка в settings.json.
  setAutostart: (enabled) => ipcRenderer.invoke('native:setAutostart', enabled),
  getAutostart: () => ipcRenderer.invoke('native:getAutostart'),
};

contextBridge.exposeInMainWorld('floridaV', api);
contextBridge.exposeInMainWorld('flovmp', api);
