'use strict';
// Заглушка window.floridaV для просмотра лаунчера в обычном браузере
// (без Electron/нативного помощника) — чтобы владелец мог кликать по
// всему интерфейсу и видеть правки сразу, не пересобирая .exe каждый раз.
// В реальном Electron-приложении preload.js подставляет настоящий мост
// ДО загрузки этого файла, поэтому здесь всегда проверяем и не перетираем.
if (!window.floridaV) {
  console.warn('[dev-shim] window.floridaV не найден — работаем в режиме браузерного превью (без нативного помощника и без реального сервера).');

  const STORE_KEY = 'flovmp_dev_settings';
  function loadStored() {
    try { return JSON.parse(localStorage.getItem(STORE_KEY) || '{}'); } catch { return {}; }
  }
  function saveStored(data) {
    try { localStorage.setItem(STORE_KEY, JSON.stringify(data)); } catch {}
  }

  let fakeMaximized = false;

  window.floridaV = {
    minimize: async () => console.log('[dev-shim] minimize() — недоступно в браузере'),
    toggleMaximize: async () => { fakeMaximized = !fakeMaximized; console.log('[dev-shim] toggleMaximize()'); },
    close: async () => console.log('[dev-shim] close() — недоступно в браузере (закрой вкладку)'),
    onWindowState: () => {},

    getSettings: async () => loadStored(),
    saveSettings: async (data) => { saveStored(data); return true; },

    detectGta: async () => { console.log('[dev-shim] detectGta() — эмуляция, ничего не найдено'); return null; },
    validateGta: async (gtaPath) => !!gtaPath,
    browseFolder: async () => { console.log('[dev-shim] browseFolder() — недоступно в браузере'); return null; },

    serverStatus: async () => ({ online: false, players: 0 }),
    play: async () => { console.log('[dev-shim] play() — недоступно в браузере, это только визуальное превью'); return { success: false, error: 'Превью в браузере: запуск игры недоступен' }; },

    setAutostart: async (enabled) => { console.log('[dev-shim] setAutostart()', enabled); return enabled; },
    getAutostart: async () => false,
  };
}
