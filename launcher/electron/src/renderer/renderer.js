'use strict';

// ─── Титлбар: управление окном ─────────────────────────────────────────────
document.getElementById('btn-min').addEventListener('click', () => window.floridaV.minimize());
document.getElementById('btn-max').addEventListener('click', () => window.floridaV.toggleMaximize());
document.getElementById('btn-close').addEventListener('click', () => window.floridaV.close());

// ─── Навигация по страницам ─────────────────────────────────────────────────
document.querySelectorAll('.rail-item[data-page]').forEach((btn) => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.rail-item[data-page]').forEach((b) => b.classList.remove('active'));
    btn.classList.add('active');
    const target = btn.dataset.page;
    document.querySelectorAll('.page').forEach((p) => p.classList.toggle('active', p.id === `page-${target}`));
  });
});

const openUrl = (url) => window.open(url, '_blank');
document.getElementById('btn-discord').addEventListener('click', () => openUrl('https://discord.gg/derzhavarp'));
document.getElementById('btn-forum').addEventListener('click', () => openUrl('https://forum.derzhava-rp.ru'));
document.getElementById('btn-donate').addEventListener('click', () => openUrl('https://donate.derzhava-rp.ru'));

// ─── Новости (пока статичные — как и было в WPF-версии) ────────────────────
const NEWS = [
  { title: 'Открытие Держава RP — добро пожаловать!', date: '30.08.2026' },
  { title: 'Новая карта: реальные улицы Москвы', date: '29.08.2026' },
  { title: 'Обновление FloV:MP 1.0 — стабильный запуск', date: '28.08.2026' },
  { title: 'Первые RP-фракции открыты для вступления', date: '27.08.2026' },
];

function renderNews() {
  const short = document.getElementById('news-list-short');
  const full = document.getElementById('news-list-full');
  short.innerHTML = NEWS.map(
    (n) => `<div class="news-card"><div class="t">${n.title}</div><div class="d">${n.date}</div></div>`
  ).join('');
  full.innerHTML = NEWS.map(
    (n) => `<div class="news-full-card"><div class="t">${n.title}</div><div class="d">${n.date}</div></div>`
  ).join('');
}
renderNews();

// ─── Силуэт города (декоративный) ───────────────────────────────────────────
(function buildSkyline() {
  const el = document.getElementById('skyline');
  const n = 34;
  for (let i = 0; i < n; i++) {
    const b = document.createElement('i');
    const h = 20 + Math.abs(Math.sin(i * 0.7)) * 70 + (i % 5 === 0 ? 30 : 0);
    b.style.height = h + '%';
    b.style.width = 100 / n + '%';
    el.appendChild(b);
  }
})();

// ─── Состояние настроек ─────────────────────────────────────────────────────
let settings = {
  nickname: 'Игрок',
  gtaPath: '',
  serverHost: '127.0.0.1',
  serverPort: 7788,
  autoUpdate: true,
  clientEdition: 'Legacy',
};

function applySettingsToUI() {
  document.getElementById('set-nickname').value = settings.nickname;
  document.getElementById('set-gtapath').value = settings.gtaPath;
  document.getElementById('set-host').value = settings.serverHost;
  document.getElementById('set-port').value = settings.serverPort;
  document.getElementById('set-autoupdate').checked = settings.autoUpdate;
  setEditionToggle(settings.clientEdition);

  document.getElementById('account-nick').textContent = settings.nickname;
  document.getElementById('avatar-initial').textContent = (settings.nickname || 'И')[0].toUpperCase();
}

function setEditionToggle(edition) {
  document.getElementById('toggle-legacy').classList.toggle('on', edition === 'Legacy');
  document.getElementById('toggle-enhanced').classList.toggle('on', edition === 'Enhanced');
}

let saveTimer = null;
function saveSettingsDebounced() {
  clearTimeout(saveTimer);
  saveTimer = setTimeout(async () => {
    await window.floridaV.saveSettings(settings);
  }, 300);
}

document.getElementById('set-nickname').addEventListener('input', (e) => {
  settings.nickname = e.target.value;
  document.getElementById('account-nick').textContent = settings.nickname;
  document.getElementById('avatar-initial').textContent = (settings.nickname || 'И')[0].toUpperCase();
  saveSettingsDebounced();
});
document.getElementById('set-host').addEventListener('input', (e) => {
  settings.serverHost = e.target.value;
  saveSettingsDebounced();
});
document.getElementById('set-port').addEventListener('input', (e) => {
  settings.serverPort = parseInt(e.target.value, 10) || 7788;
  saveSettingsDebounced();
});
document.getElementById('set-autoupdate').addEventListener('change', (e) => {
  settings.autoUpdate = e.target.checked;
  saveSettingsDebounced();
});

document.getElementById('toggle-legacy').addEventListener('click', () => setEdition('Legacy'));
document.getElementById('toggle-enhanced').addEventListener('click', () => setEdition('Enhanced'));
function setEdition(edition) {
  settings.clientEdition = edition;
  setEditionToggle(edition);
  document.getElementById('settings-status').textContent = `Профиль клиента: ${edition}`;
  saveSettingsDebounced();
}

document.getElementById('btn-browse').addEventListener('click', async () => {
  const path = await window.floridaV.browseFolder();
  if (!path) return;
  const check = await window.floridaV.validateGta(path);
  if (!check || !check.valid) {
    document.getElementById('settings-status').textContent = 'В выбранной папке не найден GTA5.exe / GTA5_Enhanced.exe.';
    return;
  }
  settings.gtaPath = path;
  settings.clientEdition = check.edition === 'Enhanced' ? 'Enhanced' : 'Legacy';
  document.getElementById('set-gtapath').value = path;
  setEditionToggle(settings.clientEdition);
  document.getElementById('settings-status').textContent = `GTA V указана вручную — ${check.edition}`;
  saveSettingsDebounced();
});

document.getElementById('btn-detect').addEventListener('click', () => detectGta());

async function detectGta() {
  const found = await window.floridaV.detectGta();
  if (!found) {
    document.getElementById('settings-status').textContent = 'GTA V не найдена автоматически. Укажи путь вручную.';
    return;
  }
  settings.gtaPath = found.path;
  settings.clientEdition = found.edition === 'Enhanced' ? 'Enhanced' : 'Legacy';
  document.getElementById('set-gtapath').value = found.path;
  setEditionToggle(settings.clientEdition);
  document.getElementById('settings-status').textContent = `GTA V найдена: ${found.source} — ${found.edition}`;
  saveSettingsDebounced();
}

// ─── Кнопка ИГРАТЬ ───────────────────────────────────────────────────────
document.getElementById('btn-play').addEventListener('click', async () => {
  const btn = document.getElementById('btn-play');
  const status = document.getElementById('play-status');
  if (!settings.gtaPath) {
    status.textContent = 'Папка GTA V не найдена. Укажи путь в настройках.';
    status.classList.add('error');
    return;
  }
  btn.disabled = true;
  btn.textContent = 'ЗАПУСК...';
  status.classList.remove('error');
  status.textContent = 'Запуск игры...';

  const result = await window.floridaV.play(settings.gtaPath, settings.serverHost, settings.serverPort, settings.nickname);

  btn.disabled = false;
  btn.textContent = 'ИГРАТЬ';
  if (result && result.success) {
    status.textContent = 'Игра запущена! Хорошей игры!';
  } else {
    status.textContent = (result && result.error) || 'Неизвестная ошибка запуска.';
    status.classList.add('error');
  }
});

// ─── Статус сервера (опрос раз в 10с) ──────────────────────────────────────
async function pollServerStatus() {
  const result = await window.floridaV.serverStatus(settings.serverHost, settings.serverPort).catch(() => null);
  const dot = document.getElementById('status-dot');
  const text = document.getElementById('status-text');
  const online = document.getElementById('status-online');
  if (result && result.online) {
    dot.classList.add('online');
    text.textContent = 'Онлайн';
    online.textContent = `${result.players} игроков`;
  } else {
    dot.classList.remove('online');
    text.textContent = 'Офлайн';
    online.textContent = 'нет данных';
  }
}

// ─── Авторизация (локальная заглушка — как и было в WPF-версии) ───────────
let isRegisterMode = false;
const authOverlay = document.getElementById('auth-overlay');

document.getElementById('btn-toggle-mode').addEventListener('click', () => {
  isRegisterMode = !isRegisterMode;
  document.getElementById('auth-error').textContent = '';
  document.getElementById('btn-auth-submit').textContent = isRegisterMode ? 'ЗАРЕГИСТРИРОВАТЬСЯ' : 'ВОЙТИ';
  document.getElementById('btn-toggle-mode').textContent = isRegisterMode ? 'У меня уже есть аккаунт' : 'У меня ещё нет аккаунта';
});

document.getElementById('btn-auth-submit').addEventListener('click', async () => {
  const login = document.getElementById('auth-login').value.trim();
  const password = document.getElementById('auth-password').value;
  const errorEl = document.getElementById('auth-error');
  errorEl.textContent = '';

  if (!login || !password) {
    errorEl.textContent = 'Введите логин и пароль';
    return;
  }
  if (isRegisterMode && password.length < 6) {
    errorEl.textContent = 'Пароль слишком короткий (минимум 6 символов)';
    return;
  }

  settings.nickname = login;
  applySettingsToUI();
  await window.floridaV.saveSettings(settings);
  authOverlay.classList.add('hidden');
});

// ─── Инициализация ──────────────────────────────────────────────────────────
(async function init() {
  const loaded = await window.floridaV.getSettings().catch(() => null);
  if (loaded) settings = { ...settings, ...loaded };
  applySettingsToUI();

  if (settings.nickname && settings.nickname !== 'Игрок') {
    authOverlay.classList.add('hidden');
  }

  if (!settings.gtaPath) {
    detectGta();
  }

  pollServerStatus();
  setInterval(pollServerStatus, 10000);
})();
