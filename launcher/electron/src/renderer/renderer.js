'use strict';

// ─── Титлбар: управление окном ─────────────────────────────────────────────
document.getElementById('btn-min').addEventListener('click', () => window.floridaV.minimize());
document.getElementById('btn-max').addEventListener('click', () => window.floridaV.toggleMaximize());
document.getElementById('btn-close').addEventListener('click', () => window.floridaV.close());

// ─── Навигация по страницам ─────────────────────────────────────────────────
function navigateTo(target) {
  document.querySelectorAll('[data-page]').forEach((b) => {
    if (b.classList.contains('rail-item')) b.classList.toggle('active', b.dataset.page === target);
  });
  document.querySelectorAll('.page').forEach((p) => p.classList.toggle('active', p.id === `page-${target}`));
}
document.querySelectorAll('[data-page]').forEach((btn) => {
  btn.addEventListener('click', () => navigateTo(btn.dataset.page));
});

const openUrl = (url) => window.open(url, '_blank');

// ─── Ресурсы: сайт/форум/донат + соцсети, флайаут по одной иконке ──────────
const RESOURCE_URLS = {
  site: 'https://derzhava-rp.ru',
  forum: 'https://forum.derzhava-rp.ru',
  donate: 'https://donate.derzhava-rp.ru',
  discord: 'https://discord.gg/derzhavarp',
  telegram: 'https://t.me/derzhavarp',
  youtube: 'https://youtube.com/@derzhavarp',
};
(function initResources() {
  const btn = document.getElementById('btn-resources');
  const fly = document.getElementById('res-flyout');
  if (!btn || !fly) return;
  const setOpen = (open) => {
    fly.classList.toggle('hidden', !open);
    btn.classList.toggle('open', open);
    btn.setAttribute('aria-expanded', String(open));
  };
  btn.addEventListener('click', (e) => {
    e.stopPropagation();
    setOpen(fly.classList.contains('hidden'));
  });
  fly.querySelectorAll('.res-link').forEach((link) => {
    link.addEventListener('click', () => {
      const url = RESOURCE_URLS[link.dataset.res];
      if (url) openUrl(url);
      setOpen(false);
    });
  });
  document.addEventListener('click', (e) => {
    if (!fly.classList.contains('hidden') && !e.target.closest('.rail-resources')) setOpen(false);
  });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') setOpen(false); });
})();

// ─── Новости и модальное окно статьи ─────────────────────────────────────
const NEWS = [
  {
    id: 1,
    badge: 'ОТКРЫТИЕ',
    title: 'Открытие Держава RP — добро пожаловать!',
    date: '30.08.2026',
    likes: 214,
    views: 3180,
    summary: 'Долгожданный запуск сервера на независимом движке FloV:MP.',
    body: `<p>Мы рады приветствовать всех первопроходцев проекта <b>Держава RP</b>! Это масштабный мир на базе собственного высокопроизводительного мультиплеера <b>FloV:MP</b>, свободного от ограничений старых платформ.</p>
    <p>Что вас ждёт на старте:</p>
    <ul>
      <li>Уникальная экономическая система с реальными профессиями и бизнесами;</li>
      <li>Проработанная физика транспорта и кастомные модели отечественного автопрома;</li>
      <li>Полная свобода от стандартных GTA-ботов и назойливого амбиента — мир заполняют только живые игроки;</li>
      <li>Интуитивный инвентарь нового поколения и голосовая связь высокой четкости.</li>
    </ul>
    <p>Каждый новый игрок получает стартовый пакет для комфортного старта в городе. Увидимся на сервере!</p>`
  },
  {
    id: 2,
    badge: 'КАРТА',
    title: 'Новая карта: реальные улицы Москвы',
    date: '29.08.2026',
    likes: 176,
    views: 2540,
    summary: 'Кремль, Арбат, Сити и спальные районы прямо в GTA V.',
    body: `<p>Наши левел-дизайнеры завершили интеграцию уникального городского массива. Вы сможете прокатиться по Садовому кольцу, прогуляться по историческому центру или устроить гонки на широких проспектах.</p>
    <p>Особенности локации:</p>
    <ul>
      <li>Высокая детализация фасадов и узнаваемые ориентиры;</li>
      <li>Оптимизированный стриминг текстур без просадки FPS;</li>
      <li>Специальные зоны для государственных служб и бандформирований.</li>
    </ul>
    <p>Карта продолжит расширяться с каждым сезонным патчем.</p>`
  },
  {
    id: 3,
    badge: 'ОБНОВЛЕНИЕ',
    title: 'Обновление FloV:MP 1.0 — стабильный запуск',
    date: '28.08.2026',
    likes: 98,
    views: 1710,
    summary: 'Автономный сетевой стек, быстрый кэш и защита соединения.',
    body: `<p>Ядро мультиплеера переведено на версию <b>FloV:MP 1.0</b>. Мы полностью избавились от внешних зависимостей и построили автономную серверную архитектуру.</p>
    <p>Ключевые изменения:</p>
    <ul>
      <li>Мгновенный вход без очередей через наш оптимизированный коннектор;</li>
      <li>Атомарная проверка ресурсов и защита от рассинхронизации;</li>
      <li>Поддержка современных версий GTA V и гладкая интерполяция движения;</li>
      <li>Интеграция нативного лаунчера с автоопределением установленной игры.</li>
    </ul>`
  },
  {
    id: 4,
    badge: 'ФРАКЦИИ',
    title: 'Первые RP-фракции открыты для вступления',
    date: '27.08.2026',
    likes: 132,
    views: 1980,
    summary: 'Полиция, МЧС, Правительство и криминальные группировки ждут лидеров.',
    body: `<p>Начался набор лидеров и активных участников в ключевые государственные и нелегальные структуры штата.</p>
    <p>Доступные направления:</p>
    <ul>
      <li><b>Министерство Внутренних Дел</b> — контроль правопорядка, погони и патрулирование;</li>
      <li><b>Скорая Медицинская Помощь</b> — спасение жизней и полевая медицина;</li>
      <li><b>Городская Мэрия</b> — управление налогами, лицензиями и городскими проектами;</li>
      <li><b>Особые группировки</b> — контроль районов и нелегальный оборот.</li>
    </ul>
    <p>Подавайте заявки на нашем официальном форуме или в игре!</p>`
  },
];

function openNewsModal(newsItem) {
  const modal = document.getElementById('news-modal-overlay');
  document.getElementById('news-modal-badge').textContent = newsItem.badge || 'НОВОСТЬ';
  document.getElementById('news-modal-title').textContent = newsItem.title;
  document.getElementById('news-modal-date').textContent = newsItem.date;
  document.getElementById('news-modal-text').innerHTML = newsItem.body;
  document.getElementById('news-modal-likes').textContent = newsItem.likes ?? 0;
  document.getElementById('news-modal-views').textContent = newsItem.views ?? 0;
  modal.classList.remove('hidden');
}

function closeNewsModal() {
  document.getElementById('news-modal-overlay').classList.add('hidden');
}

document.getElementById('news-modal-close').addEventListener('click', closeNewsModal);
document.getElementById('news-modal-overlay').addEventListener('click', (e) => {
  if (e.target === e.currentTarget) closeNewsModal();
});
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') closeNewsModal();
});

function parseRuDate(d) {
  const [day, month, year] = d.split('.').map(Number);
  return new Date(year, month - 1, day).getTime();
}

function countIcon(name) {
  return `<span class="icon icon-${name}"></span>`;
}

function renderNewsShort() {
  const short = document.getElementById('play-news-list');
  if (!short) return;
  short.innerHTML = NEWS.slice(0, 4).map((n) => `
    <button class="pnews-card" data-news-id="${n.id}" type="button">
      <div class="pnews-thumb"></div>
      <div class="pnews-body">
        <span class="pnews-tag">${n.badge}</span>
        <div class="pnews-t">${n.title}</div>
        <div class="pnews-meta">
          <span>${n.date}</span>
          <span class="news-count">${countIcon('heart')} ${n.likes ?? 0}</span>
          <span class="news-count">${countIcon('eye')} ${n.views ?? 0}</span>
        </div>
      </div>
    </button>
  `).join('');
}

function renderNewsFull() {
  const full = document.getElementById('news-list-full');
  const query = (document.getElementById('news-search')?.value || '').trim().toLowerCase();
  const sortMode = document.getElementById('news-sort')?.value || 'new';
  const filterBadge = document.getElementById('news-filter')?.value || 'all';

  let items = NEWS.filter((n) => !query || n.title.toLowerCase().includes(query));
  if (filterBadge !== 'all') items = items.filter((n) => n.badge === filterBadge);
  items = items.slice().sort((a, b) => {
    const diff = parseRuDate(a.date) - parseRuDate(b.date);
    return sortMode === 'old' ? diff : -diff;
  });

  if (!items.length) {
    full.innerHTML = `<div class="hint" style="padding:20px 0">Ничего не найдено по запросу.</div>`;
    return;
  }

  full.innerHTML = items.map((n) => `
    <button class="news-full-card" data-news-id="${n.id}" type="button">
      <div class="news-thumb"></div>
      <div class="nfc-body">
        <span class="news-tag">${n.badge}</span>
        <div class="t">${n.title}</div>
        <div class="summary">${n.summary}</div>
        <div class="news-full-meta">
          <span class="d">${n.date}</span>
          <span class="news-count">${countIcon('heart')} ${n.likes ?? 0}</span>
          <span class="news-count">${countIcon('eye')} ${n.views ?? 0}</span>
        </div>
      </div>
    </button>
  `).join('');
}

function renderNews() {
  renderNewsShort();
  renderNewsFull();

  document.body.addEventListener('click', (e) => {
    const el = e.target.closest('[data-news-id]');
    if (!el) return;
    const id = parseInt(el.dataset.newsId, 10);
    const item = NEWS.find((n) => n.id === id);
    if (item) openNewsModal(item);
  });

  const badges = Array.from(new Set(NEWS.map((n) => n.badge)));
  const filterSelect = document.getElementById('news-filter');
  if (filterSelect) {
    filterSelect.innerHTML = '<option value="all">Все категории</option>'
      + badges.map((b) => `<option value="${b}">${b}</option>`).join('');
  }
  ['news-search', 'news-sort', 'news-filter'].forEach((id) => {
    const el = document.getElementById(id);
    if (el) el.addEventListener(id === 'news-search' ? 'input' : 'change', renderNewsFull);
  });
}
renderNews();

// ─── Силуэт города (декоративный) ───────────────────────────────────────────
(function buildSkyline() {
  const el = document.getElementById('skyline');
  if (!el) return;
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
  updateChannel: 'stable',
  clientEdition: 'Legacy',
  accentColor: 'gold',
  language: 'ru',
  animations: true,
  compactMode: false,
  rememberTab: true,
  lastSettingsTab: 'general',
  autostart: false,
  minimizeOnPlay: true,
  region: 'auto',
  anonStats: false,
  procPriority: 'normal',
  launchArgs: '',
  graphicsPreset: 'untouched',
  fpsLimit: 0,
  disableAmbient: true,
  dlSpeed: 0,
  dlThreads: 4,
  cacheDir: '',
  voiceInput: '',
  voiceOutput: '',
  voiceMode: 'ptt',
  voiceThreshold: 50,
  notifNews: true,
  notifStatus: true,
  notifEvents: true,
  notifSound: false,
  accountCreatedUtc: '',
};

// id → [ключ настройки, свойство элемента]
const SETTINGS_MAP = [
  ['set-nickname', 'nickname', 'value'],
  ['set-gtapath', 'gtaPath', 'value'],
  ['set-host', 'serverHost', 'value'],
  ['set-port', 'serverPort', 'value'],
  ['set-autoupdate', 'autoUpdate', 'checked'],
  ['set-update-channel', 'updateChannel', 'value'],
  ['set-language', 'language', 'value'],
  ['set-animations', 'animations', 'checked'],
  ['set-compact', 'compactMode', 'checked'],
  ['set-remember-tab', 'rememberTab', 'checked'],
  ['set-minimizeonplay', 'minimizeOnPlay', 'checked'],
  ['set-region', 'region', 'value'],
  ['set-anon-stats', 'anonStats', 'checked'],
  ['set-proc-priority', 'procPriority', 'value'],
  ['set-launch-args', 'launchArgs', 'value'],
  ['set-graphics-preset', 'graphicsPreset', 'value'],
  ['set-fps-limit', 'fpsLimit', 'value'],
  ['set-disable-ambient', 'disableAmbient', 'checked'],
  ['set-dl-speed', 'dlSpeed', 'value'],
  ['set-dl-threads', 'dlThreads', 'value'],
  ['set-cache-dir', 'cacheDir', 'value'],
  ['set-voice-input', 'voiceInput', 'value'],
  ['set-voice-output', 'voiceOutput', 'value'],
  ['set-voice-mode', 'voiceMode', 'value'],
  ['set-voice-threshold', 'voiceThreshold', 'value'],
  ['set-notif-news', 'notifNews', 'checked'],
  ['set-notif-status', 'notifStatus', 'checked'],
  ['set-notif-events', 'notifEvents', 'checked'],
  ['set-notif-sound', 'notifSound', 'checked'],
];

// ─── Акцентный цвет — пресеты. Меняем ТОЛЬКО --accent-color и
// --accent-color-rgb; всё остальное в styles.css выведено из них. ──────────
const ACCENTS = [
  { id: 'gold',   name: 'Золотой (по умолчанию)', accent: '#fdd015', ink: '#1a1206' },
  { id: 'pink',   name: 'Розовый',                accent: '#ff3d8a', ink: '#1a0410' },
  { id: 'blue',   name: 'Голубой',                accent: '#4ac3ff', ink: '#031420' },
  { id: 'green',  name: 'Зелёный',                accent: '#3fd98a', ink: '#031b10' },
  { id: 'purple', name: 'Фиолетовый',             accent: '#c084fc', ink: '#1a0f26' },
  { id: 'red',    name: 'Красный',                accent: '#ff5d5d', ink: '#210404' },
];

function hexToRgbList(hex) {
  const n = parseInt(hex.slice(1), 16);
  return `${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}`;
}

function applyAccent(id) {
  const preset = ACCENTS.find((a) => a.id === id) || ACCENTS[0];
  const root = document.documentElement.style;
  root.setProperty('--accent-color', preset.accent);
  root.setProperty('--accent-color-rgb', hexToRgbList(preset.accent));
  root.setProperty('--accent-ink', preset.ink);

  document.querySelectorAll('.accent-swatch').forEach((el) => {
    el.classList.toggle('selected', el.dataset.accent === preset.id);
  });
}

function renderAccentPicker() {
  const el = document.getElementById('accent-picker');
  el.innerHTML = ACCENTS.map(
    (a) => `<button class="accent-swatch" data-accent="${a.id}" title="${a.name}"
      style="background:${a.accent}">
      <span class="icon icon-check"></span></button>`
  ).join('');
  el.querySelectorAll('.accent-swatch').forEach((btn) => {
    btn.addEventListener('click', () => {
      settings.accentColor = btn.dataset.accent;
      applyAccent(settings.accentColor);
      saveSettingsDebounced();
    });
  });
}

function dlSpeedLabel(v) { return Number(v) === 0 ? 'Без ограничения' : `${v} МБ/с`; }
function voiceThrLabel(v) { v = Number(v); return v < 33 ? 'Низкий' : v < 66 ? 'Средний' : 'Высокий'; }

function applySettingsToUI() {
  SETTINGS_MAP.forEach(([id, key, prop]) => {
    const el = document.getElementById(id);
    if (el) el[prop] = settings[key];
  });
  setEditionToggle(settings.clientEdition);
  applyAccent(settings.accentColor);
  document.documentElement.classList.toggle('compact', !!settings.compactMode);
  document.documentElement.classList.toggle('no-anim', !settings.animations);

  const s = document.getElementById('dl-speed-val'); if (s) s.textContent = dlSpeedLabel(settings.dlSpeed);
  const t = document.getElementById('dl-threads-val'); if (t) t.textContent = String(settings.dlThreads);
  const vt = document.getElementById('voice-thr-val'); if (vt) vt.textContent = voiceThrLabel(settings.voiceThreshold);

  document.getElementById('account-nick').textContent = settings.nickname;
  document.getElementById('avatar-initial').textContent = (settings.nickname || 'И')[0].toUpperCase();
}

// ─── Единая привязка всех контролов настроек к settings + сохранение ──────
SETTINGS_MAP.forEach(([id, key, prop]) => {
  if (id === 'set-nickname' || id === 'set-gtapath') return; // у них свои обработчики ниже
  const el = document.getElementById(id);
  if (!el) return;
  el.addEventListener(prop === 'checked' ? 'change' : 'input', (e) => {
    let v = e.target[prop];
    if (prop === 'value' && el.type === 'number') v = Number(v);
    if (prop === 'value' && el.type === 'range') v = Number(v);
    settings[key] = v;
    if (id === 'set-compact') document.documentElement.classList.toggle('compact', v);
    if (id === 'set-animations') document.documentElement.classList.toggle('no-anim', !v);
    if (id === 'set-dl-speed') document.getElementById('dl-speed-val').textContent = dlSpeedLabel(v);
    if (id === 'set-dl-threads') document.getElementById('dl-threads-val').textContent = String(v);
    if (id === 'set-voice-threshold') document.getElementById('voice-thr-val').textContent = voiceThrLabel(v);
    saveSettingsDebounced();
  });
});

// ─── Аудиоустройства для вкладки «Голос» (реальные, из системы) ───────────
async function fillAudioDevices() {
  if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) return;
  try {
    const devs = await navigator.mediaDevices.enumerateDevices();
    const fill = (selId, kind) => {
      const sel = document.getElementById(selId);
      if (!sel) return;
      const cur = settings[selId === 'set-voice-input' ? 'voiceInput' : 'voiceOutput'];
      devs.filter((d) => d.kind === kind).forEach((d) => {
        const o = document.createElement('option');
        o.value = d.deviceId; o.textContent = d.label || (kind === 'audioinput' ? 'Микрофон' : 'Динамики');
        sel.appendChild(o);
      });
      if (cur) sel.value = cur;
    };
    fill('set-voice-input', 'audioinput');
    fill('set-voice-output', 'audiooutput');
  } catch {}
}
let _audioDevicesFilled = false;

// ─── Проверка микрофона — реальный уровень громкости ─────────────────────
document.getElementById('btn-mic-test')?.addEventListener('click', async () => {
  const meter = document.getElementById('mic-meter-fill');
  if (!meter || !navigator.mediaDevices) return;
  try {
    const stream = await navigator.mediaDevices.getUserMedia({
      audio: settings.voiceInput ? { deviceId: { exact: settings.voiceInput } } : true,
    });
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    const src = ctx.createMediaStreamSource(stream);
    const analyser = ctx.createAnalyser();
    analyser.fftSize = 512;
    src.connect(analyser);
    const data = new Uint8Array(analyser.frequencyBinCount);
    const started = Date.now();
    (function loop() {
      analyser.getByteFrequencyData(data);
      const avg = data.reduce((a, b) => a + b, 0) / data.length;
      meter.style.width = Math.min(100, avg * 1.6) + '%';
      if (Date.now() - started < 5000) requestAnimationFrame(loop);
      else { stream.getTracks().forEach((t) => t.stop()); ctx.close(); meter.style.width = '0%'; }
    })();
  } catch {
    meter.style.width = '0%';
  }
});

// ─── Служебные ссылки в настройках ──────────────────────────────────────
document.getElementById('btn-clear-cache')?.addEventListener('click', () => {
  window.floridaV.clearCache?.();
  try { localStorage.removeItem('flovmp_dev_settings'); } catch {}
});
document.getElementById('btn-verify-files')?.addEventListener('click', () => window.floridaV.verifyFiles?.());
document.getElementById('btn-open-logs')?.addEventListener('click', () => window.floridaV.openLogs?.());
document.getElementById('btn-open-changelog')?.addEventListener('click', () => openUrl('https://derzhava-rp.ru/changelog'));
document.getElementById('btn-cache-browse')?.addEventListener('click', async () => {
  const dir = await window.floridaV.browseFolder();
  if (dir) { settings.cacheDir = dir; document.getElementById('set-cache-dir').value = dir; saveSettingsDebounced(); }
});

// Автозапуск — настоящая системная настройка (реестр Run через Electron),
// не просто галочка в settings.json. Источник истины — сама ОС, не файл.
document.getElementById('set-autostart').addEventListener('change', async (e) => {
  const actual = await window.floridaV.setAutostart(e.target.checked);
  settings.autostart = actual;
  e.target.checked = actual;
  saveSettingsDebounced();
});

function doLogout() {
  settings.nickname = 'Игрок';
  settings.accountCreatedUtc = '';
  applySettingsToUI();
  saveSettingsDebounced();
  document.getElementById('cabinet-overlay').classList.add('hidden');
  document.getElementById('auth-overlay').classList.remove('hidden');
  document.getElementById('auth-login').value = '';
  document.getElementById('auth-password').value = '';
}
document.getElementById('btn-logout').addEventListener('click', doLogout);
document.getElementById('cabinet-logout').addEventListener('click', doLogout);

// ─── Личный кабинет: модалка со вкладками (профиль/баланс) ─────────────────
const cabinetOverlay = document.getElementById('cabinet-overlay');
function formatDate(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('ru-RU', { day: '2-digit', month: 'long', year: 'numeric' });
}
function openCabinet() {
  document.getElementById('cabinet-nick').textContent = settings.nickname;
  document.getElementById('cabinet-avatar').textContent = (settings.nickname || 'И')[0].toUpperCase();
  document.getElementById('cab-nick').textContent = settings.nickname;
  document.getElementById('cab-created').textContent = formatDate(settings.accountCreatedUtc);
  cabinetOverlay.classList.remove('hidden');
}
document.getElementById('btn-open-cabinet').addEventListener('click', openCabinet);
document.getElementById('cabinet-close').addEventListener('click', () => cabinetOverlay.classList.add('hidden'));
cabinetOverlay.addEventListener('click', (e) => { if (e.target === cabinetOverlay) cabinetOverlay.classList.add('hidden'); });
document.querySelectorAll('.cabinet-item[data-tab]').forEach((btn) => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.cabinet-item[data-tab]').forEach((b) => b.classList.remove('active'));
    document.querySelectorAll('.cabinet-tab').forEach((t) => t.classList.remove('active'));
    btn.classList.add('active');
    document.querySelector(`.cabinet-tab[data-tab="${btn.dataset.tab}"]`).classList.add('active');
  });
});

// ─── Настройки: всплывающее окно (как у Majestic), не отдельная страница —
// открывается поверх текущего экрана (Играть/Новости), тот остаётся видимым
// и блюрится позади, закрывается — крестиком, кликом мимо или Esc.
const settingsOverlay = document.getElementById('settings-overlay');
function selectSettingsTab(name) {
  const btn = document.querySelector(`.settings-subnav [data-subtab="${name}"]`);
  if (!btn) return;
  document.querySelectorAll('.settings-subnav [data-subtab]').forEach((b) => b.classList.remove('active'));
  document.querySelectorAll('.settings-tab').forEach((t) => t.classList.remove('active'));
  btn.classList.add('active');
  document.querySelector(`.settings-tab[data-subtab="${name}"]`).classList.add('active');
  if (name === 'voice' && !_audioDevicesFilled) {
    _audioDevicesFilled = true;
    fillAudioDevices();
  }
}
document.getElementById('btn-open-settings').addEventListener('click', () => {
  if (settings.rememberTab && settings.lastSettingsTab) selectSettingsTab(settings.lastSettingsTab);
  settingsOverlay.classList.remove('hidden');
});
document.getElementById('settings-close').addEventListener('click', () => settingsOverlay.classList.add('hidden'));
settingsOverlay.addEventListener('click', (e) => { if (e.target === settingsOverlay) settingsOverlay.classList.add('hidden'); });
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && !settingsOverlay.classList.contains('hidden')) settingsOverlay.classList.add('hidden');
});

// ─── Настройки: категории слева ──────────────────────────────────────────
document.querySelectorAll('.settings-subnav [data-subtab]').forEach((btn) => {
  btn.addEventListener('click', () => {
    selectSettingsTab(btn.dataset.subtab);
    settings.lastSettingsTab = btn.dataset.subtab;
    if (settings.rememberTab) saveSettingsDebounced();
  });
});

// Честно: реального сервера обновлений (CDN-раздачи манифестов) пока нет,
// поэтому кнопка не притворяется, что что-то проверила — просто говорит,
// что проверять пока нечего, и возвращает исходный текст через паузу.
document.getElementById('btn-check-update').addEventListener('click', (e) => {
  const label = e.currentTarget.lastChild;
  const original = label.textContent;
  label.textContent = ' Сервер обновлений ещё не подключён';
  setTimeout(() => { label.textContent = original; }, 2500);
});
document.getElementById('btn-open-site').addEventListener('click', () => openUrl('https://derzhava-rp.ru'));
document.getElementById('btn-open-support').addEventListener('click', () => openUrl('https://discord.gg/derzhavarp'));

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

// ─── Модалка запуска / загрузки файлов ────────────────────────────────────
const launchOverlay = document.getElementById('launch-overlay');
function openLaunchModal(serverName) {
  document.getElementById('launch-title').textContent = (serverName || 'Басманный').toUpperCase();
  updateLaunchProgress({ phase: 'Подготовка…', percent: 0, downloaded: 0, total: 0, speed: 0 });
  launchOverlay.classList.remove('hidden');
}
function closeLaunchModal() { launchOverlay.classList.add('hidden'); }
function fmtGb(bytes) { return (Number(bytes || 0) / 1073741824).toFixed(1); }
function updateLaunchProgress(d) {
  d = d || {};
  const pct = Math.max(0, Math.min(100, Number(d.percent) || 0));
  document.getElementById('launch-bar-fill').style.width = pct + '%';
  if (d.phase) document.getElementById('launch-phase').textContent = d.phase;
  const dl = typeof d.downloaded === 'number' ? fmtGb(d.downloaded) : (d.downloaded ?? '0');
  const tot = typeof d.total === 'number' ? fmtGb(d.total) : (d.total ?? '0');
  document.getElementById('launch-stat-size').textContent = `${dl} ГБ из ${tot} ГБ`;
  const spd = typeof d.speed === 'number' ? Math.round(d.speed / 1048576) : (d.speed ?? '0');
  document.getElementById('launch-stat-speed').textContent = `${spd} MB/s`;
}
document.getElementById('launch-cancel').addEventListener('click', () => {
  window.floridaV.cancelPlay?.();
  closeLaunchModal();
});
// Реальные данные загрузки от нативного слоя (main → preload → сюда).
window.floridaV.onDownloadProgress?.((data) => {
  // data: { downloaded, total, speed, percent, phase, done, error }
  if (!launchOverlay.classList.contains('hidden')) updateLaunchProgress(data);
  if (data && data.done) setTimeout(closeLaunchModal, 700);
});

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
  btn.textContent = 'ЗАПУСК…';
  status.classList.remove('error');
  status.textContent = '';
  openLaunchModal('Басманный');

  const result = await window.floridaV.play(settings.gtaPath, settings.serverHost, settings.serverPort, settings.nickname);

  btn.disabled = false;
  btn.textContent = 'ИГРАТЬ';
  if (result && result.success) {
    updateLaunchProgress({ phase: 'Игра запущена', percent: 100 });
    setTimeout(closeLaunchModal, 900);
    if (settings.minimizeOnPlay) setTimeout(() => window.floridaV.minimize(), 1500);
  } else {
    closeLaunchModal();
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

// ─── Авторизация — реальный запрос к /api/auth/{register,login} на том же
// AuthService/accounts.json, что и в игре (см. FloVMP.ServerLauncher/Program.cs).
// Требует запущенного alt:V-сервера (владелец это принял осознанно, 2026-09-05:
// один аккаунт для лаунчера и игры важнее, чем офлайн-логин без сервера).
const AUTH_API = 'http://127.0.0.1:7799/api/auth';
let isRegisterMode = false;
const authOverlay = document.getElementById('auth-overlay');

// Вход в лаунчере — по желанию, не обязательный шаг: можно пропустить и
// войти уже в самой игре (тот же аккаунт, экран авторизации там свой).
document.getElementById('auth-skip').addEventListener('click', () => {
  authOverlay.classList.add('hidden');
});

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
  const submitBtn = document.getElementById('btn-auth-submit');
  errorEl.textContent = '';

  if (!login || !password) {
    errorEl.textContent = 'Введите логин и пароль';
    return;
  }
  if (isRegisterMode && password.length < 6) {
    errorEl.textContent = 'Пароль слишком короткий (минимум 6 символов)';
    return;
  }

  submitBtn.disabled = true;
  const prevText = submitBtn.textContent;
  submitBtn.textContent = 'Проверка...';
  try {
    const res = await fetch(`${AUTH_API}/${isRegisterMode ? 'register' : 'login'}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: login, password }),
    });
    const data = await res.json();
    if (!data.ok) {
      errorEl.textContent = data.message || 'Не удалось выполнить вход';
      return;
    }
    settings.nickname = data.username || login;
    settings.accountCreatedUtc = data.createdUtc || settings.accountCreatedUtc || '';
    applySettingsToUI();
    await window.floridaV.saveSettings(settings);
    authOverlay.classList.add('hidden');
  } catch (e) {
    errorEl.textContent = 'Сервер сейчас недоступен — запусти FloV:MP и попробуй снова';
  } finally {
    submitBtn.disabled = false;
    submitBtn.textContent = prevText;
  }
});

// ─── Инициализация ──────────────────────────────────────────────────────────
(async function init() {
  renderAccentPicker();
  const loaded = await window.floridaV.getSettings().catch(() => null);
  if (loaded) settings = { ...settings, ...loaded };
  settings.autostart = await window.floridaV.getAutostart().catch(() => settings.autostart);
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
