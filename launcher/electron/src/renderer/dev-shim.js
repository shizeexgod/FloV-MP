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

    getSettings: async () => {
      const s = loadStored();
      if (!s.gtaPath) s.gtaPath = 'C:\\Games\\GTA V (демо-превью)';
      return s;
    },
    saveSettings: async (data) => { saveStored(data); return true; },

    detectGta: async () => { console.log('[dev-shim] detectGta() — эмуляция, ничего не найдено'); return null; },
    validateGta: async (gtaPath) => !!gtaPath,
    browseFolder: async () => { console.log('[dev-shim] browseFolder() — недоступно в браузере'); return null; },

    serverStatus: async () => ({ online: false, players: 0 }),

    // Демо-прогресс для модалки запуска (в браузере игру не запустить).
    _progressCb: null,
    _demoTimer: null,
    onDownloadProgress(cb) { this._progressCb = cb; },
    cancelPlay() { clearInterval(window.floridaV._demoTimer); },
    play: async () => {
      const cb = window.floridaV._progressCb;
      if (cb) {
        const total = 4.2 * 1073741824;
        let p = 0;
        const phases = ['Проверка файлов…', 'Загрузка файлов игры', 'Серверные файлы', 'Подключение к серверу'];
        clearInterval(window.floridaV._demoTimer);
        window.floridaV._demoTimer = setInterval(() => {
          p = Math.min(100, p + 4 + Math.random() * 6);
          cb({
            percent: p,
            downloaded: (p / 100) * total,
            total,
            speed: (55 + Math.random() * 20) * 1048576,
            phase: phases[Math.min(phases.length - 1, Math.floor(p / 26))],
            done: p >= 100,
          });
          if (p >= 100) clearInterval(window.floridaV._demoTimer);
        }, 400);
      }
      await new Promise((r) => setTimeout(r, 4600));
      return { success: true };
    },

    setAutostart: async (enabled) => { console.log('[dev-shim] setAutostart()', enabled); return enabled; },
    getAutostart: async () => false,
  };
}
