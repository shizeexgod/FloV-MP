'use strict';
// Заглушка window.floridaV для просмотра лаунчера в обычном браузере
// (без Electron/нативного помощника) — чтобы владелец мог кликать по
// всему интерфейсу и видеть правки сразу, не пересобирая .exe каждый раз.
// В реальном Electron-приложении preload.js подставляет настоящий мост
// ДО загрузки этого файла, поэтому здесь всегда проверяем и не перетираем.
if (!window.floridaV) {
  console.warn('[dev-shim] window.floridaV не найден — работаем в режиме браузерного превью (без нативного помощника и без реального сервера).');

  const STORE_KEY = 'flovmp_dev_settings';
  const SESSION_KEY = 'flovmp_dev_session';
  function loadStored() {
    try { return JSON.parse(localStorage.getItem(STORE_KEY) || '{}'); } catch { return {}; }
  }
  function saveStored(data) {
    try { localStorage.setItem(STORE_KEY, JSON.stringify(data)); } catch {}
  }

  // Грубое определение ОС по userAgent — только для браузерного превью,
  // в Electron это делает настоящий DeviceInfoService на C#.
  function guessOs() {
    const ua = navigator.userAgent;
    if (/Windows NT 10/.test(ua)) return 'Windows 11';
    if (/Windows/.test(ua)) return 'Windows';
    if (/Mac OS X/.test(ua)) return 'macOS';
    if (/Linux/.test(ua)) return 'Linux';
    return 'ОС неизвестна';
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

    // Устройство — правдоподобные данные из браузера (без реального IP).
    deviceInfo: async () => ({
      deviceId: 'devpreview01',
      hostname: (navigator.userAgentData && navigator.userAgentData.platform) || 'BROWSER-PREVIEW',
      userName: 'preview',
      os: guessOs(),
      osArch: /x64|Win64|WOW64/.test(navigator.userAgent) ? 'X64' : 'X86',
      localIp: 'недоступен',
      bootTimeUtc: new Date(Date.now() - 3 * 3600e3).toISOString(),
      nowUtc: new Date().toISOString(),
    }),

    // Авторизация/безопасность — в браузере настоящего сервера нет, поэтому
    // заглушка: login/register принимает любые непустые данные и «создаёт»
    // аккаунт; смены пароля/почты/2FA — успех. Аккаунт кладём в localStorage,
    // чтобы «запоминание входа» тоже можно было проверить.
    auth: async (mode, payload) => {
      const p = payload || {};
      if (mode === 'login' || mode === 'register') {
        if (!p.username || !p.password) return { ok: false, message: 'Введите логин и пароль' };
        if (mode === 'register' && p.password.length < 6) return { ok: false, message: 'Пароль слишком короткий' };
        const acc = { username: p.username, createdUtc: new Date().toISOString(), email: '', twoFa: false };
        try { localStorage.setItem(SESSION_KEY, JSON.stringify(acc)); } catch {}
        return { ok: true, message: 'ok', ...acc };
      }
      if (mode === 'change-password') return { ok: true, message: 'Пароль изменён (демо)' };
      if (mode === 'change-email') return { ok: true, message: 'Почта сохранена (демо)' };
      if (mode === '2fa-enable') return { ok: true, message: '2FA включена (демо)' };
      if (mode === '2fa-disable') return { ok: true, message: '2FA выключена (демо)' };
      return { ok: false, message: 'неизвестная операция' };
    },
    readSession: async () => {
      try { return JSON.parse(localStorage.getItem(SESSION_KEY) || 'null'); } catch { return null; }
    },
    clearSession: async () => { try { localStorage.removeItem(SESSION_KEY); } catch {} return true; },
  };
}
