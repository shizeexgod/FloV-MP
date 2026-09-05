'use strict';

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('floridaV', {
  // Управление окном
  minimize: () => ipcRenderer.invoke('window:minimize'),
  toggleMaximize: () => ipcRenderer.invoke('window:toggleMaximize'),
  close: () => ipcRenderer.invoke('window:close'),
  onWindowState: (cb) => ipcRenderer.on('window:state', (_e, state) => cb(state)),

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

  // Автозапуск с Windows — настоящая системная настройка через Electron,
  // не просто галочка в settings.json.
  setAutostart: (enabled) => ipcRenderer.invoke('native:setAutostart', enabled),
  getAutostart: () => ipcRenderer.invoke('native:getAutostart'),
});
