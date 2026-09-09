'use strict';
console.log('[renderer.js] STARTING INITIALIZATION');

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
    image: 'assets/news/cover-opening.svg',
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
  const banner = document.getElementById('news-modal-banner');
  if (banner) {
    banner.classList.toggle('news-modal-banner--img', !!newsItem.image);
    banner.style.backgroundImage = newsItem.image ? `url('${newsItem.image}')` : '';
  }
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
  if (n.image) {
    return `<div class="news-thumb news-thumb--img" data-badge="${n.badge}"
      style="background-image:url('${n.image}')"></div>`;
  }
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
  serverHost: '188.127.229.224',
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
  upscalerMode: 'none',
  upscalerQuality: 'quality',
  upscalerSharpness: 50,
  upscalerFrameGen: true,
  upscalerNuiProtection: true,
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
  ['set-upscaler-mode', 'upscalerMode', 'value'],
  ['set-upscaler-quality', 'upscalerQuality', 'value'],
  ['set-upscaler-sharpness', 'upscalerSharpness', 'value'],
  ['set-upscaler-framegen', 'upscalerFrameGen', 'checked'],
  ['set-upscaler-nui-protection', 'upscalerNuiProtection', 'checked'],
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
    <button class="accent-swatch accent-swatch--custom" title="Свой цвет" data-accent="custom" type="button"
      style="--sw:${settings.accentCustom || '#8b5cf6'}"><span class="accent-swatch__plus">+</span></button>`;

  el.querySelectorAll('.accent-swatch[data-accent]:not(.accent-swatch--custom)').forEach((btn) => {
    btn.addEventListener('click', () => pickAccent(btn.dataset.accent));
  });
  const customBtn = el.querySelector('.accent-swatch--custom');
  if (customBtn) {
    customBtn.addEventListener('click', () => {
      openColorPicker(customBtn, settings.accentCustom || accentHex(), (hex) => {
        customBtn.style.setProperty('--sw', hex);
        pickAccent('custom', hex);
      });
    });
  }
}

// ─── Кастомный HSV-пикер цвета (свой акцент) ────────────────────────────
let _cpick = null;
function hsvToRgb(h, s, v) {
  h /= 360;
  const i = Math.floor(h * 6), f = h * 6 - i;
  const p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
  const [r, g, b] = [
    [v, t, p], [q, v, p], [p, v, t], [p, q, v], [t, p, v], [v, p, q],
  ][i % 6];
  return [Math.round(r * 255), Math.round(g * 255), Math.round(b * 255)];
}
function rgbToHsv(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min;
  let h = 0;
  if (d) {
    if (max === r) h = ((g - b) / d) % 6;
    else if (max === g) h = (b - r) / d + 2;
    else h = (r - g) / d + 4;
    h *= 60; if (h < 0) h += 360;
  }
  return [h, max ? d / max : 0, max];
}
function hexToRgbArr(hex) {
  const n = parseInt((hex || '').replace('#', ''), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}
function rgbToHex(r, g, b) {
  return '#' + [r, g, b].map((c) => Math.max(0, Math.min(255, c | 0)).toString(16).padStart(2, '0')).join('');
}

function openColorPicker(anchor, initialHex, onChange) {
  closeColorPicker();
  let [h, s, v] = rgbToHsv(...hexToRgbArr(/^#[0-9a-f]{6}$/i.test(initialHex) ? initialHex : '#8b5cf6'));

  const box = document.createElement('div');
  box.className = 'cpick';
  box.innerHTML = `
    <div class="cpick__sv"><div class="cpick__knob"></div></div>
    <div class="cpick__hue"><div class="cpick__hue-knob"></div></div>
    <div class="cpick__row">
      <span class="cpick__prev"></span>
      <input class="cpick__hex" type="text" maxlength="7" spellcheck="false">
    </div>`;
  (document.getElementById('app') || document.body).appendChild(box);
  _cpick = box;

  const svEl = box.querySelector('.cpick__sv');
  const knob = box.querySelector('.cpick__knob');
  const hueEl = box.querySelector('.cpick__hue');
  const hueKnob = box.querySelector('.cpick__hue-knob');
  const prev = box.querySelector('.cpick__prev');
  const hexInput = box.querySelector('.cpick__hex');

  const render = (fireChange = true) => {
    const [r, g, bl] = hsvToRgb(h, s, v);
    const hex = rgbToHex(r, g, bl);
    svEl.style.setProperty('--cp-hue', `hsl(${h}, 100%, 50%)`);
    knob.style.left = `${s * 100}%`;
    knob.style.top = `${(1 - v) * 100}%`;
    knob.style.background = hex;
    hueKnob.style.left = `${(h / 360) * 100}%`;
    prev.style.background = hex;
    if (document.activeElement !== hexInput) hexInput.value = hex.toUpperCase();
    if (fireChange) onChange(hex);
  };

  const dragSV = (e) => {
    const r = svEl.getBoundingClientRect();
    s = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width));
    v = Math.max(0, Math.min(1, 1 - (e.clientY - r.top) / r.height));
    render();
  };
  const dragHue = (e) => {
    const r = hueEl.getBoundingClientRect();
    h = Math.max(0, Math.min(359.9, ((e.clientX - r.left) / r.width) * 360));
    render();
  };
  const bindDrag = (el, mover) => {
    el.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      mover(e);
      const mv = (ev) => mover(ev);
      const up = () => { document.removeEventListener('pointermove', mv); document.removeEventListener('pointerup', up); };
      document.addEventListener('pointermove', mv);
      document.addEventListener('pointerup', up);
    });
  };
  bindDrag(svEl, dragSV);
  bindDrag(hueEl, dragHue);
  hexInput.addEventListener('input', () => {
    let val = hexInput.value.trim();
    if (!val.startsWith('#')) val = '#' + val;
    if (/^#[0-9a-f]{6}$/i.test(val)) {
      [h, s, v] = rgbToHsv(...hexToRgbArr(val));
      render();
    }
  });

  // позиционируем рядом с якорем, в пределах окна
  const ar = anchor.getBoundingClientRect();
  box.style.left = `${Math.min(ar.left, window.innerWidth - 250)}px`;
  const below = ar.bottom + 8;
  box.style.top = (below + 210 > window.innerHeight ? Math.max(8, ar.top - 218) : below) + 'px';

  render(false);
  // форсируем reflow и включаем анимацию открытия синхронно (rAF в фоновой
  // вкладке может не сработать — тогда попап «молча» не появлялся)
  void box.offsetWidth;
  box.classList.add('open');

  setTimeout(() => document.addEventListener('pointerdown', outsideClose, true), 0);
  function outsideClose(e) {
    if (!box.contains(e.target) && e.target !== anchor) closeColorPicker();
  }
  box._outsideClose = outsideClose;
}
function closeColorPicker() {
  if (!_cpick) return;
  const box = _cpick; _cpick = null;
  document.removeEventListener('pointerdown', box._outsideClose, true);
  box.classList.remove('open');
  setTimeout(() => box.remove(), 200);
}
document.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeColorPicker(); });

function dlSpeedLabel(v) { return Number(v) === 0 ? 'Без ограничения' : `${v} МБ/с`; }
function voiceThrLabel(v) { v = Number(v); return v < 33 ? 'Низкий' : v < 66 ? 'Средний' : 'Высокий'; }

const UPSCALER_DESCRIPTIONS = {
  none: 'Оригинальный рендеринг без изменений',
  fsr3_framegen: 'AMD FidelityFX Super Resolution 3.1 + Генерация кадров (для любых GPU)',
  dlss_framegen: 'NVIDIA Deep Learning Super Sampling 3.7 + Frame Generation (RTX)',
  dlss5_neural: '✨ Экспериментальный Neural Reconstruction («DLSS 5»)',
};

function updateUpscalerUI() {
  const mode = settings.upscalerMode || 'none';
  const isOff = mode === 'none';

  const descEl = document.getElementById('upscaler-desc');
  if (descEl) descEl.textContent = UPSCALER_DESCRIPTIONS[mode] || UPSCALER_DESCRIPTIONS.none;

  const qRow = document.getElementById('row-upscaler-quality');
  const sRow = document.getElementById('row-upscaler-sharpness');
  const fgRow = document.getElementById('row-upscaler-framegen');
  const nuiRow = document.getElementById('row-upscaler-nui-protection');

  if (qRow) qRow.style.display = isOff ? 'none' : '';
  if (sRow) sRow.style.display = isOff ? 'none' : '';
  if (fgRow) fgRow.style.display = isOff ? 'none' : '';
  if (nuiRow) nuiRow.style.display = isOff ? 'none' : '';

  const shVal = document.getElementById('upscaler-sharpness-val');
  if (shVal) shVal.textContent = `${settings.upscalerSharpness ?? 50}%`;
}

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

  updateUpscalerUI();

  syncAllXSelects();
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

// ─── Масштаб интерфейса — нативный setZoomFactor через Chromium webFrame.
// Полностью исключает артефакты CSS zoom и сбои hit-testing Windows.
function applyUiScale() {
  const s = Math.max(80, Math.min(140, Number(settings.uiScale) || 100));
  try {
    if (window.floridaV?.setZoom) {
      window.floridaV.setZoom(s / 100);
    }
  } catch {}
}

// ─── Звук в интерфейсе ──────────────────────────────────────────────────
// Архитектура по образцу Majestic: именованные звуки, общий master-gain,
// decodeAudioData. Файлы кладутся в assets/sounds/<name>.ogg (или .mp3).
// Пока файлов нет — короткий синтез через осциллятор, чтобы фича работала
// сразу и было слышно, куда встанут настоящие сэмплы.
const SND = (() => {
  const DEFS = {
    hover:   { vol: 0.30, tone: [520, 480, 0.035] },
    click:   { vol: 0.55, tone: [660, 440, 0.07] },
    select:  { vol: 0.5,  tone: [700, 900, 0.09] },
    modal:   { vol: 0.55, tone: [300, 520, 0.12] },
    modalOut:{ vol: 0.45, tone: [520, 300, 0.10] },
    toggle:  { vol: 0.4,  tone: [600, 720, 0.05] },
    error:   { vol: 0.6,  tone: [300, 180, 0.18] },
  };
  let ctx = null, master = null;
  const buffers = new Map();

  function ensure() {
    if (ctx) return true;
    try {
      ctx = new (window.AudioContext || window.webkitAudioContext)();
      master = ctx.createGain();
      master.gain.value = 0.9;
      master.connect(ctx.destination);
      ['pointerdown', 'keydown'].forEach((ev) =>
        window.addEventListener(ev, () => ctx && ctx.resume(), { once: true }));
    } catch { return false; }
    return true;
  }
  async function preload(name) {
    if (buffers.has(name) || !ensure()) return;
    buffers.set(name, 'loading');
    for (const ext of ['ogg', 'mp3']) {
      try {
        const res = await fetch(`assets/sounds/${name}.${ext}`);
        if (!res.ok) continue;
        const buf = await ctx.decodeAudioData(await res.arrayBuffer());
        buffers.set(name, buf);
        return;
      } catch {}
    }
    buffers.set(name, 'synth'); // файла нет — синтезируем
  }
  function synth([f1, f2, dur], vol) {
    const o = ctx.createOscillator(), g = ctx.createGain();
    o.type = 'sine';
    o.frequency.setValueAtTime(f1, ctx.currentTime);
    o.frequency.exponentialRampToValueAtTime(Math.max(1, f2), ctx.currentTime + dur);
    g.gain.setValueAtTime(vol * 0.12, ctx.currentTime);
    g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + dur + 0.02);
    o.connect(g); g.connect(master);
    o.start(); o.stop(ctx.currentTime + dur + 0.03);
  }
  function play(name) {
    if (!settings.uiSounds || !DEFS[name] || !ensure()) return;
    if (ctx.state === 'suspended') ctx.resume();
    const b = buffers.get(name);
    if (b instanceof AudioBuffer) {
      const src = ctx.createBufferSource();
      src.buffer = b;
      const g = ctx.createGain();
      g.gain.value = DEFS[name].vol;
      src.connect(g); g.connect(master);
      src.start(0);
    } else if (b === 'synth') {
      synth(DEFS[name].tone, DEFS[name].vol);
    } else {
      preload(name).then(() => play(name));
    }
  }
  return { play, preload };
})();

// автопривязка: клик/наведение по «кликабельному» (cursor:pointer), как в
// Majestic; [data-no-sound] — отключить для поддерева
function _snd_clickable(t) {
  const el = t instanceof Element ? t : null;
  if (!el || getComputedStyle(el).cursor !== 'pointer') return null;
  return el.closest('[data-no-sound]') ? null : el;
}
document.addEventListener('click', (e) => { if (_snd_clickable(e.target)) SND.play('click'); }, { capture: true, passive: true });
document.addEventListener('mouseover', (e) => {
  const en = _snd_clickable(e.target);
  if (en && _snd_clickable(e.relatedTarget) !== en) SND.play('hover');
}, { capture: true, passive: true });
document.addEventListener('change', (e) => {
  if (e.target.matches('input[type="checkbox"], .switch input')) SND.play('toggle');
}, true);

// звук открытия/закрытия любых модалок — по появлению/снятию .hidden
(function wireModalSounds() {
  const sel = '.settings-overlay, .cabinet-overlay, .auth-overlay, .news-modal-overlay, .launch-overlay';
  document.querySelectorAll(sel).forEach((ov) => {
    let wasHidden = ov.classList.contains('hidden');
    new MutationObserver(() => {
      const now = ov.classList.contains('hidden');
      if (now !== wasHidden) { SND.play(now ? 'modalOut' : 'modal'); wasHidden = now; }
    }).observe(ov, { attributes: true, attributeFilter: ['class'] });
  });
})();

// ─── Единая привязка всех контролов настроек к settings + сохранение ──────
SETTINGS_MAP.forEach(([id, key, prop]) => {
  if (id === 'set-gtapath') return; // у него свой обработчик ниже
  const el = document.getElementById(id);
  if (!el) return;
  // Масштаб интерфейса: во время перетаскивания (input) только меняем
  // подпись, сам zoom применяем по change — иначе перекомпоновка на каждый
  // кадр «выкидывает» ползунок из-под курсора.
  const isUiScale = id === 'set-ui-scale';
  const evName = prop === 'checked' ? 'change' : (isUiScale ? 'change' : 'input');

  el.addEventListener(evName, (e) => {
    let v = e.target[prop];
    if (prop === 'value' && el.type === 'number') v = Number(v);
    if (prop === 'value' && el.type === 'range') v = Number(v);
    settings[key] = v;
    if (id === 'set-compact') document.documentElement.classList.toggle('compact', v);
    if (id === 'set-animations') document.documentElement.classList.toggle('no-anim', !v);
    if (isUiScale) { document.getElementById('ui-scale-val').textContent = `${v}%`; applyUiScale(); }
    if (id === 'set-tray-on-close') window.floridaV.setTrayOnClose?.(v);
    if (id === 'set-dl-speed') document.getElementById('dl-speed-val').textContent = dlSpeedLabel(v);
    if (id === 'set-dl-threads') document.getElementById('dl-threads-val').textContent = String(v);
    if (id === 'set-voice-threshold') document.getElementById('voice-thr-val').textContent = voiceThrLabel(v);
    if (id === 'set-upscaler-mode') updateUpscalerUI();
    if (id === 'set-upscaler-sharpness') {
      const sh = document.getElementById('upscaler-sharpness-val');
      if (sh) sh.textContent = `${v}%`;
    }
    saveSettingsDebounced();
  });
  if (isUiScale) {
    // живая подпись при перетаскивании, без применения zoom
    el.addEventListener('input', (e) => {
      document.getElementById('ui-scale-val').textContent = `${e.target.value}%`;
    });
  }
  if (id === 'set-upscaler-sharpness') {
    el.addEventListener('input', (e) => {
      const sh = document.getElementById('upscaler-sharpness-val');
      if (sh) sh.textContent = `${e.target.value}%`;
    });
  }
});

// ─── Аудиоустройства для вкладки «Голос» (реальные, из системы) ───────────
function cleanDeviceLabel(label, kind) {
  if (!label) return kind === 'audioinput' ? 'Микрофон' : 'Динамики';
  return label
    .replace(/^(Default|Communications|По умолчанию|Связь)\s*[-–]\s*/i, '')
    .replace(/\s*\([0-9a-f]{4}:[0-9a-f]{4}\)\s*$/i, '')
    .trim();
}
async function fillAudioDevices() {
  if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) return;
  try {
    const devs = await navigator.mediaDevices.enumerateDevices();
    const fill = (selId, kind) => {
      const sel = document.getElementById(selId);
      if (!sel) return;
      const cur = settings[selId === 'set-voice-input' ? 'voiceInput' : 'voiceOutput'];
      // «default»/«communications» — псевдо-id Windows, дублируют реальное
      // устройство; оставляем только настоящие + первый пункт «По умолчанию».
      const seen = new Set();
      devs.filter((d) => d.kind === kind && d.deviceId
        && d.deviceId !== 'default' && d.deviceId !== 'communications')
        .forEach((d) => {
          const name = cleanDeviceLabel(d.label, kind);
          if (seen.has(name)) return;
          seen.add(name);
          const o = document.createElement('option');
          o.value = d.deviceId; o.textContent = name;
          sel.appendChild(o);
        });
      if (cur && [...sel.options].some((o) => o.value === cur)) sel.value = cur;
    };
    fill('set-voice-input', 'audioinput');
    fill('set-voice-output', 'audiooutput');
  } catch {}
  refreshXSelect(document.getElementById('set-voice-input'));
  refreshXSelect(document.getElementById('set-voice-output'));
}
let _audioDevicesFilled = false;

// ─── Кастомные выпадающие списки (замена нативного <select>) ─────────────
// Нативный select остаётся источником значения; поверх — .xselect с
// анимацией открытия И закрытия. Все существующие обработчики change/input
// на select продолжают работать (клик по опции их и вызывает).
let _openXSelect = null;
function buildXSelect(select) {
  if (select.dataset.xs === '1') return;
  select.dataset.xs = '1';
  select.classList.add('xs-native');
  select.tabIndex = -1;

  const wrap = document.createElement('div');
  wrap.className = 'xselect';
  const btn = document.createElement('button');
  btn.type = 'button';
  btn.className = 'xselect__btn';
  btn.innerHTML = `<span class="xselect__label"></span><span class="xselect__chev"></span>`;
  const menu = document.createElement('div');
  menu.className = 'xselect__menu';
  wrap.append(btn, menu);
  select.after(wrap);
  select._xs = { wrap, btn, menu };

  const close = () => {
    if (!wrap.classList.contains('open')) return;
    wrap.classList.remove('open');
    if (_openXSelect === wrap) _openXSelect = null;
  };
  const open = () => {
    if (_openXSelect && _openXSelect !== wrap) _openXSelect.classList.remove('open');
    // вверх, если снизу мало места
    const spaceBelow = window.innerHeight - btn.getBoundingClientRect().bottom;
    wrap.classList.toggle('up', spaceBelow < 260);
    wrap.classList.add('open');
    _openXSelect = wrap;
    const sel = menu.querySelector('.xselect__opt.sel');
    if (sel) sel.scrollIntoView({ block: 'nearest' });
  };
  btn.addEventListener('click', (e) => {
    e.stopPropagation();
    wrap.classList.contains('open') ? close() : open();
  });
  btn.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); btn.click(); }
    if (e.key === 'Escape') close();
  });
  select._xs.close = close;
  refreshXSelect(select);
}
function refreshXSelect(select) {
  if (!select || !select._xs) return;
  const { btn, menu } = select._xs;
  const opts = [...select.options];
  btn.querySelector('.xselect__label').textContent =
    (select.selectedOptions[0] && select.selectedOptions[0].textContent) || '';
  menu.innerHTML = '';
  opts.forEach((o) => {
    const row = document.createElement('div');
    row.className = 'xselect__opt' + (o.selected ? ' sel' : '');
    row.textContent = o.textContent;
    row.addEventListener('click', (e) => {
      e.stopPropagation();
      if (select.value !== o.value) {
        select.value = o.value;
        select.dispatchEvent(new Event('input', { bubbles: true }));
        select.dispatchEvent(new Event('change', { bubbles: true }));
      }
      refreshXSelect(select);
      select._xs.close();
    });
    menu.appendChild(row);
  });
}
function syncAllXSelects() {
  document.querySelectorAll('select[data-xs="1"]').forEach(refreshXSelect);
}
function enhanceSelects() {
  document.querySelectorAll('select:not([data-xs])').forEach(buildXSelect);
}
document.addEventListener('click', () => { if (_openXSelect) _openXSelect.classList.remove('open'), (_openXSelect = null); });
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && _openXSelect) { _openXSelect.classList.remove('open'); _openXSelect = null; }
});

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
    const data = await window.floridaV.auth(mode, { username: settings.account?.username, ...payload, serverHost: settings.serverHost });
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
    status.textContent = 'Поиск папки GTA V…';
    status.classList.remove('error');
    await detectGta();
  }
  if (!settings.gtaPath) {
    status.textContent = 'Папка GTA V не найдена. Укажите путь в настройках.';
    status.classList.add('error');
    selectSettingsTab('game');
    settingsOverlay.classList.remove('hidden');
    const input = document.getElementById('set-gtapath');
    if (input) input.focus();
    return;
  }
  btn.disabled = true;
  btn.textContent = 'ЗАПУСК…';
  status.classList.remove('error');
  status.textContent = '';
  openLaunchModal('Басманный');

  // Сбросить отложенное сохранение до старта — PlayService на нативной
  // стороне читает settings.json (режим окна, доп. аргументы, приоритет,
  // FPS, пресет графики).
  clearTimeout(saveTimer);
  await window.floridaV.saveSettings(settings);

  // Движок клиента FloV:MP — если не установлен локально и не обновлен, качаем с CDN один раз
  try {
    const eng = await window.floridaV.engineStatus?.();
    if (eng && (!eng.installed || !eng.upToDate)) {
      updateLaunchProgress({
        phase: eng.installed ? 'Обновление движка…' : 'Первый запуск: загрузка движка',
        percent: 0,
      });
      const dl = await window.floridaV.downloadEngine?.();
      if (!dl || !dl.ok) {
        closeLaunchModal();
        btn.disabled = false; btn.textContent = 'ИГРАТЬ';
        const m = (dl && dl.error) || 'не удалось загрузить движок с CDN';
        status.textContent = `Движок не установлен: ${m}`;
        status.classList.add('error');
        alert(`Ошибка загрузки движка:\n${m}`);
        return;
      }
    }
  } catch (e) {
    console.warn('engineStatus check failed/skipped:', e);
  }

  updateLaunchProgress({ phase: 'Инициализация коннектора…', percent: 20 });

  try {
    const result = await window.floridaV.play(settings.gtaPath, settings.serverHost, settings.serverPort, settings.nickname);

    btn.disabled = false;
    btn.textContent = 'ИГРАТЬ';
    if (result && result.success) {
      updateLaunchProgress({ phase: 'Игра запущена', percent: 100 });
      setTimeout(closeLaunchModal, 900);
      if (settings.minimizeOnPlay) setTimeout(() => window.floridaV.minimize(), 1500);
    } else {
      closeLaunchModal();
      const errMsg = (result && result.error) || 'Неизвестная ошибка запуска.';
      status.textContent = errMsg;
      status.classList.add('error');
      alert(`Ошибка запуска игры:\n${errMsg}`);
    }
  } catch (err) {
    closeLaunchModal();
    btn.disabled = false;
    btn.textContent = 'ИГРАТЬ';
    const errMsg = err?.message || String(err);
    status.textContent = `Сбой вызова: ${errMsg}`;
    status.classList.add('error');
    alert(`Сбой вызова коннектора:\n${errMsg}`);
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
    const payload = { username: login, password, serverHost: settings.serverHost };
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

// ─── FloV:Graphics — GPU детекция и аппаратные рекомендации ───────────────
async function initGpuInfo() {
  const badge = document.getElementById('gpu-badge');
  const recEl = document.getElementById('gpu-recommendation');
  if (!badge) return;

  try {
    const gpu = await window.floridaV.detectGpu?.();
    if (!gpu) {
      badge.textContent = 'GPU: Авто';
      return;
    }

    const vramGb = (gpu.vramMb / 1024).toFixed(0);
    badge.textContent = `${gpu.gpuName} (${vramGb} GB)`;

    badge.classList.remove('badge-gpu--rtx', 'badge-gpu--amd', 'badge-gpu--intel');
    if (gpu.isRtx) {
      badge.classList.add('badge-gpu--rtx');
    } else if (gpu.vendor === 'AMD') {
      badge.classList.add('badge-gpu--amd');
    } else if (gpu.vendor === 'Intel') {
      badge.classList.add('badge-gpu--intel');
    }

    if (recEl) {
      let recText = `💡 Рекомендация для ${gpu.gpuName}: `;
      if (gpu.recommendedMode === 'dlss5_neural') {
        recText += 'Рекомендуется «DLSS 5 Neural Reconstruction» или DLSS 3.7 + Frame Generation для максимального FPS и кинематографичной чёткости.';
      } else if (gpu.recommendedMode === 'dlss_framegen') {
        recText += 'Рекомендуется DLSS 3.7 + Frame Generation (поддерживаются RT/Tensor ядра).';
      } else if (gpu.recommendedMode === 'fsr3_framegen') {
        recText += 'Рекомендуется AMD FSR 3.1 + Frame Generation для стабильного прироста FPS на вашей конфигурации.';
      } else {
        recText += 'Стандартный рендеринг без масштабирования.';
      }
      recEl.textContent = recText;
    }
  } catch {
    badge.textContent = 'GPU: Авто';
  }
}

// ─── Инициализация ──────────────────────────────────────────────────────────
(async function init() {
  renderAccentPicker();
  enhanceSelects();
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

  initGpuInfo();

  if (!settings.gtaPath) detectGta();

  window.floridaV.setTrayOnClose?.(settings.trayOnClose);

  pollServerStatus();
  setInterval(pollServerStatus, 10000);

  pollSession();
  setInterval(pollSession, 5000);
  window.addEventListener('focus', pollSession);
  console.log('[renderer.js] COMPLETED INITIALIZATION');
})();

