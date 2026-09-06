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

// ─── Лайки новостей — состояние на клиенте (localStorage), реально меняет
//     число под каждой новостью и в карточках ленты ───────────────────────
const LIKED_KEY = 'flovmp:likedNews';
let likedNews = new Set();
try {
  const raw = localStorage.getItem(LIKED_KEY);
  if (raw) likedNews = new Set(JSON.parse(raw));
} catch {}
function saveLiked() {
  try { localStorage.setItem(LIKED_KEY, JSON.stringify([...likedNews])); } catch {}
}
function isLiked(id) { return likedNews.has(id); }
// n.likes — «чужие» лайки (база); свой лайк добавляет +1 поверх.
function likeCount(n) { return (n.likes ?? 0) + (isLiked(n.id) ? 1 : 0); }

let currentNewsItem = null;

function syncLikeButton() {
  const btn = document.getElementById('news-modal-like');
  if (!btn || !currentNewsItem) return;
  const liked = isLiked(currentNewsItem.id);
  btn.classList.toggle('is-liked', liked);
  btn.setAttribute('aria-pressed', liked ? 'true' : 'false');
  document.getElementById('news-modal-likes').textContent = likeCount(currentNewsItem);
}

function toggleLike() {
  if (!currentNewsItem) return;
  const id = currentNewsItem.id;
  if (likedNews.has(id)) likedNews.delete(id);
  else {
    likedNews.add(id);
    const btn = document.getElementById('news-modal-like');
    btn?.classList.remove('pop');
    // рестарт анимации
    void btn?.offsetWidth;
    btn?.classList.add('pop');
  }
  saveLiked();
  syncLikeButton();
  // счётчики в ленте
  renderNewsShort();
  renderNewsFull();
}

function openNewsModal(newsItem) {
  currentNewsItem = newsItem;
  const modal = document.getElementById('news-modal-overlay');
  document.getElementById('news-modal-badge').textContent = newsItem.badge || 'НОВОСТЬ';
  document.getElementById('news-modal-title').textContent = newsItem.title;
  document.getElementById('news-modal-date').textContent = newsItem.date;
  document.getElementById('news-modal-text').innerHTML = newsItem.body;
  document.getElementById('news-modal-views').textContent = newsItem.views ?? 0;
  syncLikeButton();
  modal.classList.remove('hidden');
}

function closeNewsModal() {
  document.getElementById('news-modal-overlay').classList.add('hidden');
}
document.getElementById('news-modal-like')?.addEventListener('click', toggleLike);

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

// Категория новости → иконка-водяной знак на обложке (пока настоящих
// картинок нет — обложка это акцентный градиент + крупная иконка темы).
const NEWS_CAT_ICON = {
  'ОТКРЫТИЕ': 'sparkles',
  'КАРТА': 'mappin',
  'ОБНОВЛЕНИЕ': 'download',
  'ФРАКЦИИ': 'forum',
  'ИВЕНТ': 'gift',
};
function catIcon(badge) { return NEWS_CAT_ICON[badge] || 'newspaper'; }
function newsThumb(n) {
  return `<div class="news-thumb" data-badge="${n.badge}">
    <span class="news-thumb__wm icon icon-${catIcon(n.badge)}"></span>
  </div>`;
}

function renderNewsShort() {
  const short = document.getElementById('play-news-list');
  if (!short) return;
  short.innerHTML = NEWS.slice(0, 4).map((n) => `
    <button class="pnews-card" data-news-id="${n.id}" type="button">
      ${newsThumb(n).replace('news-thumb', 'pnews-thumb news-thumb')}
      <div class="pnews-body">
        <span class="pnews-tag">${n.badge}</span>
        <div class="pnews-t">${n.title}</div>
        <div class="pnews-meta">
          <span>${n.date}</span>
          <span class="news-count${isLiked(n.id) ? ' is-liked' : ''}">${countIcon('heart')} ${likeCount(n)}</span>
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

  const match = (n) => n.title.toLowerCase().includes(query) || (n.summary || '').toLowerCase().includes(query);
  let items = NEWS.filter((n) => !query || match(n));
  if (filterBadge !== 'all') items = items.filter((n) => n.badge === filterBadge);
  items = items.slice().sort((a, b) => {
    const diff = parseRuDate(a.date) - parseRuDate(b.date);
    return sortMode === 'old' ? diff : -diff;
  });

  if (!items.length) {
    full.innerHTML = `<div class="news-empty">
      <span class="icon icon-search"></span>
      <div>Ничего не найдено${query ? ` по запросу «${query}»` : ''}</div>
    </div>`;
    return;
  }

  full.innerHTML = items.map((n) => `
    <button class="news-full-card" data-news-id="${n.id}" type="button">
      ${newsThumb(n)}
      <div class="nfc-body">
        <span class="news-tag">${n.badge}</span>
        <div class="t">${n.title}</div>
        <div class="summary">${n.summary}</div>
        <div class="news-full-meta">
          <span class="d">${n.date}</span>
          <span class="news-count${isLiked(n.id) ? ' is-liked' : ''}">${countIcon('heart')} ${likeCount(n)}</span>
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
// nickname НЕ редактируется вручную: гость → «Игрок», после входа → логин
// аккаунта. Источник истины — settings.account ({ username, createdUtc } или
// null); nickname держим синхронно как производное для удобства сохранения.
let settings = {
  account: null,
  nickname: 'Игрок',
  gtaPath: '',
  serverHost: '127.0.0.1',
  serverPort: 7788,
  autoUpdate: true,
  updateChannel: 'stable',
  clientEdition: 'Legacy',
  accentColor: 'gold',
  accentCustom: '#8b5cf6',
  language: 'ru',
  animations: true,
  compactMode: false,
  rememberTab: true,
  uiScale: 100,
  uiSounds: false,
  trayOnClose: false,
  lastSettingsTab: 'general',
  autostart: false,
  minimizeOnPlay: true,
  region: 'auto',
  anonStats: false,
  procPriority: 'normal',
  launchArgs: '',
  gtaWindowMode: 'keep',
  graphicsPreset: 'untouched',
  fpsLimit: 0,
  disableAmbient: true,
  dlSpeed: 0,
  dlThreads: 4,
  verifyAfterDl: true,
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
  ['set-gtapath', 'gtaPath', 'value'],
  ['set-host', 'serverHost', 'value'],
  ['set-port', 'serverPort', 'value'],
  ['set-autoupdate', 'autoUpdate', 'checked'],
  ['set-update-channel', 'updateChannel', 'value'],
  ['set-language', 'language', 'value'],
  ['set-animations', 'animations', 'checked'],
  ['set-compact', 'compactMode', 'checked'],
  ['set-remember-tab', 'rememberTab', 'checked'],
  ['set-ui-scale', 'uiScale', 'value'],
  ['set-ui-sounds', 'uiSounds', 'checked'],
  ['set-tray-on-close', 'trayOnClose', 'checked'],
  ['set-minimizeonplay', 'minimizeOnPlay', 'checked'],
  ['set-region', 'region', 'value'],
  ['set-anon-stats', 'anonStats', 'checked'],
  ['set-proc-priority', 'procPriority', 'value'],
  ['set-launch-args', 'launchArgs', 'value'],
  ['set-gta-window-mode', 'gtaWindowMode', 'value'],
  ['set-graphics-preset', 'graphicsPreset', 'value'],
  ['set-fps-limit', 'fpsLimit', 'value'],
  ['set-disable-ambient', 'disableAmbient', 'checked'],
  ['set-dl-speed', 'dlSpeed', 'value'],
  ['set-dl-threads', 'dlThreads', 'value'],
  ['set-verify-after-dl', 'verifyAfterDl', 'checked'],
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

// ─── Акцентный цвет — пресеты + свой цвет. applyAccent меняет ТОЛЬКО
// --accent-color / --accent-color-rgb / --accent-ink; всё остальное в
// styles.css выведено из них через var(), поэтому перекрашивается сразу
// ВЕЗДЕ (кнопки, тумблеры, рамки, свечения, графики). ──────────────────────
const ACCENTS = [
  { id: 'gold',    name: 'Золотой',    accent: '#fdd015' },
  { id: 'amber',   name: 'Янтарный',   accent: '#ff9f43' },
  { id: 'red',     name: 'Красный',    accent: '#ff5d5d' },
  { id: 'pink',    name: 'Розовый',    accent: '#ff3d8a' },
  { id: 'purple',  name: 'Фиолетовый', accent: '#c084fc' },
  { id: 'blue',    name: 'Голубой',    accent: '#4ac3ff' },
  { id: 'teal',    name: 'Бирюзовый',  accent: '#2dd4bf' },
  { id: 'green',   name: 'Зелёный',    accent: '#3fd98a' },
];

function hexToRgbList(hex) {
  const n = parseInt(hex.slice(1), 16);
  return `${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}`;
}
// Контрастный «чернильный» цвет для текста НА акценте: тёмный вариант
// самого акцента, чтобы подпись на золотой кнопке была тёмно-золотой,
// а не чёрной. Считаем из яркости.
function inkFor(hex) {
  const n = parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255, g = (n >> 8) & 255, b = n & 255;
  const lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  if (lum > 0.6) return `#${[r, g, b].map((c) => Math.round(c * 0.16).toString(16).padStart(2, '0')).join('')}`;
  return '#ffffff';
}
function accentHex() {
  const c = settings.accentColor === 'custom'
    ? (settings.accentCustom || '#fdd015')
    : (ACCENTS.find((a) => a.id === settings.accentColor) || ACCENTS[0]).accent;
  return /^#[0-9a-fA-F]{6}$/.test(c) ? c : '#fdd015';
}
function accentName() {
  if (settings.accentColor === 'custom') return 'Свой цвет';
  return (ACCENTS.find((a) => a.id === settings.accentColor) || ACCENTS[0]).name;
}

function applyAccent() {
  const hex = accentHex();
  const root = document.documentElement.style;
  root.setProperty('--accent-color', hex);
  root.setProperty('--accent-color-rgb', hexToRgbList(hex));
  root.setProperty('--accent-ink', inkFor(hex));

  document.querySelectorAll('.accent-swatch').forEach((el) => {
    el.classList.toggle('selected', el.dataset.accent === settings.accentColor);
  });
  const indName = document.getElementById('accent-ind-name');
  const indHex = document.getElementById('accent-ind-hex');
  if (indName) indName.textContent = accentName();
  if (indHex) indHex.textContent = hex.toUpperCase();
}

function pickAccent(id, customHex) {
  settings.accentColor = id;
  if (id === 'custom' && customHex) settings.accentCustom = customHex;
  applyAccent();
  saveSettingsDebounced();
}

function renderAccentPicker() {
  const el = document.getElementById('accent-picker');
  if (!el) return;
  el.innerHTML = ACCENTS.map(
    (a) => `<button class="accent-swatch" data-accent="${a.id}" title="${a.name}" type="button"
      style="--sw:${a.accent}"><span class="icon icon-check"></span></button>`
  ).join('') + `
    <label class="accent-swatch accent-swatch--custom" title="Свой цвет" data-accent="custom"
      style="--sw:${settings.accentCustom || '#8b5cf6'}">
      <span class="accent-swatch__plus">+</span>
      <input type="color" id="accent-custom-input" value="${settings.accentCustom || '#8b5cf6'}">
    </label>`;

  el.querySelectorAll('.accent-swatch[data-accent]:not(.accent-swatch--custom)').forEach((btn) => {
    btn.addEventListener('click', () => pickAccent(btn.dataset.accent));
  });
  const custom = document.getElementById('accent-custom-input');
  if (custom) {
    custom.addEventListener('input', (e) => {
      const hex = e.target.value;
      e.target.closest('.accent-swatch').style.setProperty('--sw', hex);
      pickAccent('custom', hex);
    });
  }
}

function dlSpeedLabel(v) { return Number(v) === 0 ? 'Без ограничения' : `${v} МБ/с`; }
function voiceThrLabel(v) { v = Number(v); return v < 33 ? 'Низкий' : v < 66 ? 'Средний' : 'Высокий'; }

function applySettingsToUI() {
  SETTINGS_MAP.forEach(([id, key, prop]) => {
    const el = document.getElementById(id);
    if (el) el[prop] = settings[key];
  });
  setEditionToggle(settings.clientEdition);
  renderAccentPicker();
  applyAccent();
  applyUiScale();
  document.documentElement.classList.toggle('compact', !!settings.compactMode);
  document.documentElement.classList.toggle('no-anim', !settings.animations);

  const s = document.getElementById('dl-speed-val'); if (s) s.textContent = dlSpeedLabel(settings.dlSpeed);
  const t = document.getElementById('dl-threads-val'); if (t) t.textContent = String(settings.dlThreads);
  const vt = document.getElementById('voice-thr-val'); if (vt) vt.textContent = voiceThrLabel(settings.voiceThreshold);
  const us = document.getElementById('ui-scale-val'); if (us) us.textContent = `${settings.uiScale}%`;

  applyAccountUI();
}

// ─── Аккаунт → всё, что зависит от «вошёл / не вошёл» ────────────────────
// Ник в рельсе, буква аватара, шапка кабинета, вкладка «Профиль», класс
// на <html> для стилей. Один аккаунт для лаунчера и игры.
function isLoggedIn() { return !!(settings.account && settings.account.username); }

function applyAccountUI() {
  const nick = isLoggedIn() ? settings.account.username : 'Игрок';
  settings.nickname = nick;
  const initial = (nick || 'И')[0].toUpperCase();

  const railNick = document.getElementById('account-nick');
  if (railNick) railNick.textContent = nick;
  const railAvatar = document.getElementById('avatar-initial');
  if (railAvatar) railAvatar.textContent = initial;

  document.documentElement.classList.toggle('logged-in', isLoggedIn());

  const cabNick = document.getElementById('cabinet-nick');
  if (cabNick) cabNick.textContent = nick;
  const cabAvatar = document.getElementById('cabinet-avatar');
  if (cabAvatar) cabAvatar.textContent = initial;
  const cabSub = document.getElementById('cabinet-sub');
  if (cabSub) cabSub.textContent = isLoggedIn() ? 'Аккаунт Держава RP' : 'Гость — войдите в аккаунт';

  const cabProfileNick = document.getElementById('cab-nick');
  if (cabProfileNick) cabProfileNick.textContent = isLoggedIn() ? settings.account.username : '—';
  const cabCreated = document.getElementById('cab-created');
  if (cabCreated) cabCreated.textContent = isLoggedIn() ? formatDate(settings.account.createdUtc) : '—';
  const cabStatus = document.getElementById('cab-status');
  if (cabStatus) cabStatus.textContent = isLoggedIn() ? 'Вход выполнен' : 'Не выполнен вход';
}

// ─── Масштаб интерфейса (90–125%) — Chromium zoom на корне ───────────────
function applyUiScale() {
  const s = Math.max(80, Math.min(140, Number(settings.uiScale) || 100));
  document.documentElement.style.zoom = String(s / 100);
}

// ─── Звук в интерфейсе — короткий клик через WebAudio (без файлов) ───────
let _uiAudioCtx = null;
function uiClick() {
  if (!settings.uiSounds) return;
  try {
    _uiAudioCtx = _uiAudioCtx || new (window.AudioContext || window.webkitAudioContext)();
    const ctx = _uiAudioCtx;
    const o = ctx.createOscillator();
    const g = ctx.createGain();
    o.type = 'sine';
    o.frequency.setValueAtTime(660, ctx.currentTime);
    o.frequency.exponentialRampToValueAtTime(440, ctx.currentTime + 0.05);
    g.gain.setValueAtTime(0.05, ctx.currentTime);
    g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.08);
    o.connect(g); g.connect(ctx.destination);
    o.start(); o.stop(ctx.currentTime + 0.09);
  } catch {}
}
document.addEventListener('click', (e) => {
  if (e.target.closest('button, .rail-item, .cabinet-item, .link-row, .accent-swatch, .toggle-btn, .srv-play')) uiClick();
}, true);

// ─── Единая привязка всех контролов настроек к settings + сохранение ──────
SETTINGS_MAP.forEach(([id, key, prop]) => {
  if (id === 'set-gtapath') return; // у него свой обработчик ниже
  const el = document.getElementById(id);
  if (!el) return;
  el.addEventListener(prop === 'checked' ? 'change' : 'input', (e) => {
    let v = e.target[prop];
    if (prop === 'value' && el.type === 'number') v = Number(v);
    if (prop === 'value' && el.type === 'range') v = Number(v);
    settings[key] = v;
    if (id === 'set-compact') document.documentElement.classList.toggle('compact', v);
    if (id === 'set-animations') document.documentElement.classList.toggle('no-anim', !v);
    if (id === 'set-ui-scale') { applyUiScale(); document.getElementById('ui-scale-val').textContent = `${v}%`; }
    if (id === 'set-tray-on-close') window.floridaV.setTrayOnClose?.(v);
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

// «Скопировать сведения о системе» — для отправки в поддержку. Только то,
// что уже отдаёт deviceInfo + версия лаунчера, без личных данных.
document.getElementById('btn-copy-sysinfo')?.addEventListener('click', async (e) => {
  const info = await loadDeviceInfo();
  const lines = [
    `Держава RP Launcher — сведения о системе`,
    `Версия лаунчера: ${document.querySelector('.about-version')?.textContent || '—'}`,
    `ОС: ${info?.os || '—'} (${info?.osArch || '—'})`,
    `Устройство: ${info?.hostname || '—'}`,
    `Локальный IP: ${info?.localIp || '—'}`,
    `Аккаунт: ${isLoggedIn() ? settings.account.username : 'гость'}`,
    `Папка GTA V: ${settings.gtaPath || 'не указана'} · клиент ${settings.clientEdition}`,
    `Время: ${new Date().toISOString()}`,
  ].join('\n');
  try {
    await navigator.clipboard.writeText(lines);
    const lbl = e.currentTarget.lastChild;
    const orig = lbl.textContent;
    lbl.textContent = ' Скопировано в буфер обмена';
    setTimeout(() => { lbl.textContent = orig; }, 2000);
  } catch {}
});
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
  settings.account = null;
  settings.accountCreatedUtc = '';
  applyAccountUI();
  saveSettingsDebounced();
  try { window.floridaV.clearSession?.(); } catch {}
  document.getElementById('cabinet-overlay').classList.add('hidden');
  const al = document.getElementById('auth-login'); if (al) al.value = '';
  const ap = document.getElementById('auth-password'); if (ap) ap.value = '';
}
document.getElementById('cabinet-logout').addEventListener('click', doLogout);

// ─── Личный кабинет: модалка со вкладками (Профиль / Безопасность /
// Устройства / История входов), по образцу Majestic ──────────────────────
const cabinetOverlay = document.getElementById('cabinet-overlay');
function formatDate(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('ru-RU', { day: '2-digit', month: 'long', year: 'numeric' });
}
function formatDateTime(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('ru-RU', { day: '2-digit', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

let _deviceInfo = null;
async function loadDeviceInfo() {
  if (_deviceInfo) return _deviceInfo;
  try { _deviceInfo = await window.floridaV.deviceInfo?.(); } catch { _deviceInfo = null; }
  return _deviceInfo;
}
function fillDeviceUI(info) {
  const name = document.getElementById('dev-current-name');
  const sub = document.getElementById('dev-current-sub');
  const hName = document.getElementById('hist-current-name');
  const hSub = document.getElementById('hist-current-sub');
  const hTime = document.getElementById('hist-current-time');
  if (!info) {
    if (name) name.textContent = 'Это устройство';
    if (sub) sub.textContent = 'Данные системы недоступны';
    if (hName) hName.textContent = 'Текущий сеанс';
    if (hSub) hSub.textContent = '—';
    if (hTime) hTime.textContent = '—';
    return;
  }
  const os = (info.os || '').replace(/\s+\d+\.\d+\.\d+.*/, '').trim() || info.os || 'ОС неизвестна';
  const dev = `${info.hostname || 'ПК'} · ${os} (${info.osArch || '—'})`;
  const ip = info.localIp && info.localIp !== 'недоступен' ? `IP ${info.localIp}` : 'IP локальной сети недоступен';
  if (name) name.textContent = dev;
  if (sub) sub.textContent = `${ip} · пользователь ${info.userName || '—'}`;
  if (hName) hName.textContent = dev;
  if (hSub) hSub.textContent = ip;
  if (hTime) hTime.textContent = formatDateTime(info.nowUtc);
}

function selectCabinetTab(name) {
  document.querySelectorAll('.cabinet-item[data-tab]').forEach((b) => b.classList.toggle('active', b.dataset.tab === name));
  document.querySelectorAll('.cabinet-tab').forEach((t) => t.classList.toggle('active', t.dataset.tab === name));
  if (name === 'devices' || name === 'history') loadDeviceInfo().then(fillDeviceUI);
}

function openCabinet() {
  applyAccountUI();
  selectCabinetTab('profile');
  loadDeviceInfo().then(fillDeviceUI);
  cabinetOverlay.classList.remove('hidden');
}
function closeCabinet() { cabinetOverlay.classList.add('hidden'); }

// Клик по профилю в рельсе: вошёл → кабинет, не вошёл → окно входа/регистрации
document.getElementById('btn-open-cabinet').addEventListener('click', () => {
  if (isLoggedIn()) openCabinet();
  else openAuth();
});
document.getElementById('cabinet-close').addEventListener('click', closeCabinet);
cabinetOverlay.addEventListener('click', (e) => { if (e.target === cabinetOverlay) closeCabinet(); });
document.querySelectorAll('.cabinet-item[data-tab]').forEach((btn) => {
  btn.addEventListener('click', () => selectCabinetTab(btn.dataset.tab));
});
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && !cabinetOverlay.classList.contains('hidden')) {
    const sub = cabinetOverlay.querySelector('.cab-sub-overlay:not(.hidden)');
    if (sub) sub.classList.add('hidden');
    else closeCabinet();
  }
});

// ─── Безопасность: вложенные окна (пароль / почта / 2FA) ─────────────────
cabinetOverlay.querySelectorAll('[data-cab-sub-close]').forEach((btn) => {
  btn.addEventListener('click', () => btn.closest('.cab-sub-overlay').classList.add('hidden'));
});
cabinetOverlay.querySelectorAll('.cab-sub-overlay').forEach((ov) => {
  ov.addEventListener('click', (e) => { if (e.target === ov) ov.classList.add('hidden'); });
});
function openCabSub(id) {
  const ov = document.getElementById(id);
  ov.querySelectorAll('input').forEach((i) => { i.value = ''; });
  ov.querySelectorAll('.auth-error').forEach((e) => { e.textContent = ''; });
  ov.classList.remove('hidden');
}
document.getElementById('sec-btn-password').addEventListener('click', () => openCabSub('cab-sub-password'));
document.getElementById('sec-btn-email').addEventListener('click', () => openCabSub('cab-sub-email'));

async function secSubmit(mode, payload, errorEl, okMsg) {
  errorEl.textContent = '';
  try {
    const data = await window.floridaV.auth(mode, { username: settings.account?.username, ...payload });
    if (!data || !data.ok) { errorEl.textContent = (data && data.message) || 'Раздел безопасности подключается вместе с сервером — попробуйте позже'; return false; }
    errorEl.textContent = '';
    return true;
  } catch {
    errorEl.textContent = 'Сервер недоступен — запустите Держава RP и попробуйте снова';
    return false;
  }
}

document.getElementById('cab-pw-submit').addEventListener('click', async () => {
  const cur = document.getElementById('cab-pw-current').value;
  const nw = document.getElementById('cab-pw-new').value;
  const rep = document.getElementById('cab-pw-repeat').value;
  const err = document.getElementById('cab-pw-error');
  if (!cur || !nw) { err.textContent = 'Заполните все поля'; return; }
  if (nw.length < 6) { err.textContent = 'Новый пароль слишком короткий (минимум 6 символов)'; return; }
  if (nw !== rep) { err.textContent = 'Пароли не совпадают'; return; }
  if (await secSubmit('change-password', { password: cur, newPassword: nw }, err)) {
    document.getElementById('cab-sub-password').classList.add('hidden');
  }
});
document.getElementById('cab-email-submit').addEventListener('click', async () => {
  const pw = document.getElementById('cab-email-pw').value;
  const email = document.getElementById('cab-email-new').value.trim();
  const err = document.getElementById('cab-email-error');
  if (!pw || !email) { err.textContent = 'Заполните все поля'; return; }
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) { err.textContent = 'Неверный адрес почты'; return; }
  if (await secSubmit('change-email', { password: pw, email }, err)) {
    settings.account.email = email;
    document.getElementById('sec-email-value').textContent = email;
    document.getElementById('cab-sub-email').classList.add('hidden');
    saveSettingsDebounced();
  }
});

// 2FA — генерируем секрет и otpauth-URI на клиенте (стандарт TOTP), QR
// рисуем сами. Активация/проверка кода — на сервере (когда подключат);
// пока сервер не отвечает — окно честно об этом сообщает.
function base32Secret(len = 16) {
  const abc = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let out = '';
  const rnd = new Uint8Array(len);
  (crypto.getRandomValues ? crypto : window.msCrypto).getRandomValues(rnd);
  for (let i = 0; i < len; i++) out += abc[rnd[i] % 32];
  return out;
}
let _twoFaSecret = '';
document.getElementById('sec-btn-2fa').addEventListener('click', () => {
  const on = document.getElementById('sec-2fa-status').dataset.on === 'true';
  document.getElementById('cab-2fa-enable-body').classList.toggle('hidden', on);
  document.getElementById('cab-2fa-disable-body').classList.toggle('hidden', !on);
  document.getElementById('cab-2fa-title').textContent = on ? 'Отключение Google Authenticator' : 'Подключение Google Authenticator';
  document.getElementById('cab-2fa-desc').textContent = on
    ? 'Введите код из приложения или пароль, чтобы отключить двухфакторную защиту.'
    : 'Отсканируйте QR-код в приложении Google Authenticator и введите 6-значный код для подтверждения.';
  if (!on) {
    _twoFaSecret = base32Secret();
    document.getElementById('cab-2fa-secret').textContent = _twoFaSecret.replace(/(.{4})/g, '$1 ').trim();
    const label = encodeURIComponent(`Держава RP:${settings.account?.username || 'игрок'}`);
    const uri = `otpauth://totp/${label}?secret=${_twoFaSecret}&issuer=Derzhava%20RP&digits=6&period=30`;
    renderQr(document.getElementById('cab-2fa-qr'), uri);
  }
  openCabSub('cab-sub-2fa');
});
document.getElementById('cab-2fa-submit').addEventListener('click', async () => {
  const code = document.getElementById('cab-2fa-code').value.trim();
  const err = document.getElementById('cab-2fa-error');
  if (!/^\d{6}$/.test(code)) { err.textContent = 'Введите 6-значный код из приложения'; return; }
  if (await secSubmit('2fa-enable', { secret: _twoFaSecret, code }, err)) {
    set2faStatus(true);
    document.getElementById('cab-sub-2fa').classList.add('hidden');
  }
});
document.getElementById('cab-2fa-disable-submit').addEventListener('click', async () => {
  const code = document.getElementById('cab-2fa-disable-code').value.trim();
  const err = document.getElementById('cab-2fa-disable-error');
  if (!code) { err.textContent = 'Введите код или пароль'; return; }
  if (await secSubmit('2fa-disable', { code }, err)) {
    set2faStatus(false);
    document.getElementById('cab-sub-2fa').classList.add('hidden');
  }
});
function set2faStatus(on) {
  const el = document.getElementById('sec-2fa-status');
  el.dataset.on = String(on);
  el.textContent = on ? 'включена' : 'выключена';
  document.getElementById('sec-btn-2fa').textContent = on ? 'Отключить' : 'Подключить';
  if (settings.account) settings.account.twoFa = on;
  saveSettingsDebounced();
}

// QR рисуем через window.QRCode (vendored qrcode-generator, qr.js). Если по
// какой-то причине не загрузился — показываем ключ для ручного ввода в
// приложение (Google Authenticator принимает setup key без сканирования).
function renderQr(box, text) {
  box.innerHTML = '';
  try {
    const lib = window.qrcode || window.QRCode;
    if (!lib) throw new Error('no qr lib');
    const qr = lib(0, 'M');
    qr.addData(text);
    qr.make();
    const n = qr.getModuleCount();
    const cell = Math.max(3, Math.floor(176 / n));
    const cv = document.createElement('canvas');
    cv.width = cv.height = n * cell;
    const g = cv.getContext('2d');
    g.fillStyle = '#fff'; g.fillRect(0, 0, cv.width, cv.height);
    g.fillStyle = '#000';
    for (let y = 0; y < n; y++) for (let x = 0; x < n; x++) if (qr.isDark(y, x)) g.fillRect(x * cell, y * cell, cell, cell);
    box.appendChild(cv);
  } catch {
    const secret = text.match(/secret=([A-Z2-7]+)/)?.[1] || '';
    box.innerHTML = `<div class="cab-2fa-qr-fallback"><span>Добавьте ключ вручную в приложение:</span><code>${secret.replace(/(.{4})/g, '$1 ').trim()}</code></div>`;
  }
}

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

// ─── Авторизация — /api/auth/{register,login} на том же AuthService/
// accounts.json, что и в игре. Один аккаунт для лаунчера и игры (решено с
// владельцем). Запрос идёт через мост window.floridaV.auth(...) — в Electron
// это реальный HTTP к локальному ServerLauncher, в браузерном превью —
// заглушка из dev-shim. Вход в лаунчере необязателен: можно сразу играть и
// войти в самой игре — лаунчер подхватит аккаунт из session.json.
let isRegisterMode = false;
const authOverlay = document.getElementById('auth-overlay');

let auth2faStage = false; // сервер запросил код 2FA — второй submit шлёт code
function openAuth() {
  isRegisterMode = false;
  auth2faStage = false;
  document.getElementById('auth-error').textContent = '';
  document.getElementById('btn-auth-submit').textContent = 'ВОЙТИ';
  document.getElementById('btn-toggle-mode').textContent = 'У меня ещё нет аккаунта';
  document.getElementById('auth-2fa-field').classList.add('hidden');
  document.getElementById('btn-toggle-mode').classList.remove('hidden');
  const al = document.getElementById('auth-login');
  const ap = document.getElementById('auth-password');
  const ac = document.getElementById('auth-2fa-code');
  if (al) al.value = '';
  if (ap) ap.value = '';
  if (ac) ac.value = '';
  authOverlay.classList.remove('hidden');
  setTimeout(() => al && al.focus(), 60);
}
function closeAuth() { authOverlay.classList.add('hidden'); }

document.getElementById('auth-skip').addEventListener('click', closeAuth);
authOverlay.addEventListener('click', (e) => { if (e.target === authOverlay) closeAuth(); });
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && !authOverlay.classList.contains('hidden')) closeAuth();
});

document.getElementById('btn-toggle-mode').addEventListener('click', () => {
  isRegisterMode = !isRegisterMode;
  document.getElementById('auth-error').textContent = '';
  document.getElementById('btn-auth-submit').textContent = isRegisterMode ? 'ЗАРЕГИСТРИРОВАТЬСЯ' : 'ВОЙТИ';
  document.getElementById('btn-toggle-mode').textContent = isRegisterMode ? 'У меня уже есть аккаунт' : 'У меня ещё нет аккаунта';
});

function adoptAccount(data, fallbackLogin) {
  settings.account = {
    username: data.username || fallbackLogin,
    createdUtc: data.createdUtc || settings.account?.createdUtc || '',
    email: data.email || settings.account?.email || '',
    twoFa: !!(data.twoFa ?? settings.account?.twoFa),
  };
  settings.accountCreatedUtc = settings.account.createdUtc;
  applyAccountUI();
  const emailEl = document.getElementById('sec-email-value');
  if (emailEl) emailEl.textContent = settings.account.email || 'не указана';
  set2faStatusSilent(settings.account.twoFa);
  saveSettingsDebounced();
}
function set2faStatusSilent(on) {
  const el = document.getElementById('sec-2fa-status');
  if (!el) return;
  el.dataset.on = String(!!on);
  el.textContent = on ? 'включена' : 'выключена';
  document.getElementById('sec-btn-2fa').textContent = on ? 'Отключить' : 'Подключить';
}

document.getElementById('btn-auth-submit').addEventListener('click', async () => {
  const login = document.getElementById('auth-login').value.trim();
  const password = document.getElementById('auth-password').value;
  const errorEl = document.getElementById('auth-error');
  const submitBtn = document.getElementById('btn-auth-submit');
  errorEl.textContent = '';

  if (!login || !password) { errorEl.textContent = 'Введите логин и пароль'; return; }
  if (isRegisterMode && password.length < 6) {
    errorEl.textContent = 'Пароль слишком короткий (минимум 6 символов)';
    return;
  }

  const code2fa = document.getElementById('auth-2fa-code').value.trim();
  if (auth2faStage && !/^\d{6}$/.test(code2fa)) {
    errorEl.textContent = 'Введите 6-значный код из приложения';
    return;
  }

  submitBtn.disabled = true;
  submitBtn.classList.add('is-busy');
  const prevText = submitBtn.textContent;
  submitBtn.textContent = 'Проверка…';
  try {
    const payload = { username: login, password };
    if (auth2faStage) payload.code = code2fa;
    const data = await window.floridaV.auth(isRegisterMode ? 'register' : 'login', payload);

    // Аккаунт под 2FA: сервер просит код — показываем поле и ждём второй submit.
    if (data && !data.ok && data.twoFaRequired) {
      auth2faStage = true;
      document.getElementById('auth-2fa-field').classList.remove('hidden');
      document.getElementById('btn-toggle-mode').classList.add('hidden');
      errorEl.textContent = data.message || 'Введите код из приложения-аутентификатора';
      setTimeout(() => document.getElementById('auth-2fa-code').focus(), 40);
      return;
    }
    if (!data || !data.ok) {
      errorEl.textContent = (data && data.message) || 'Не удалось выполнить вход';
      return;
    }
    adoptAccount(data, login);
    closeAuth();
  } catch (e) {
    errorEl.textContent = 'Сервер сейчас недоступен — запусти Держава RP и попробуй снова';
  } finally {
    submitBtn.disabled = false;
    submitBtn.classList.remove('is-busy');
    submitBtn.textContent = prevText;
  }
});

// ─── Хэндофф из игры: если игрок вошёл в самой игре, сервер пишет
// session.json в общую папку (%LOCALAPPDATA%\FloridaV\), лаунчер его
// подхватывает и тоже становится авторизованным под тем же аккаунтом.
async function pollSession() {
  if (isLoggedIn()) return;
  let s = null;
  try { s = await window.floridaV.readSession?.(); } catch { s = null; }
  if (s && s.username) {
    adoptAccount({ username: s.username, createdUtc: s.createdUtc, email: s.email, twoFa: s.twoFa }, s.username);
  }
}

// ─── Инициализация ──────────────────────────────────────────────────────────
(async function init() {
  renderAccentPicker();
  const loaded = await window.floridaV.getSettings().catch(() => null);
  if (loaded) settings = { ...settings, ...loaded };
  settings.autostart = await window.floridaV.getAutostart().catch(() => settings.autostart);

  // Ник — производное от аккаунта, не из файла: чинит ситуацию, когда в
  // settings.json остался старый/битый nickname без account.
  applySettingsToUI();
  if (isLoggedIn()) {
    const emailEl = document.getElementById('sec-email-value');
    if (emailEl) emailEl.textContent = settings.account.email || 'не указана';
    set2faStatusSilent(settings.account.twoFa);
  }

  if (!settings.gtaPath) detectGta();

  window.floridaV.setTrayOnClose?.(settings.trayOnClose);

  pollServerStatus();
  setInterval(pollServerStatus, 10000);

  pollSession();
  setInterval(pollSession, 5000);
  window.addEventListener('focus', pollSession);
})();
