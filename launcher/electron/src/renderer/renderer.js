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
  const short = document.getElementById('news-list-short');
  short.innerHTML = NEWS.slice(0, 4).map((n) => `
    <div class="news-card" data-news-id="${n.id}">
      <div class="news-thumb"></div>
      <div>
        <div class="t">${n.title}</div>
        <div class="d">${n.date}</div>
      </div>
    </div>
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
    <div class="news-full-card" data-news-id="${n.id}">
      <div class="news-thumb"></div>
      <div style="flex:1">
        <span class="news-tag">${n.badge}</span>
        <div class="t">${n.title}</div>
        <div class="summary">${n.summary}</div>
        <div class="news-full-meta">
          <span class="d">${n.date}</span>
          <span class="news-count">${countIcon('heart')} ${n.likes ?? 0}</span>
          <span class="news-count">${countIcon('eye')} ${n.views ?? 0}</span>
        </div>
      </div>
    </div>
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
  accentColor: 'gold',
  language: 'ru',
  animations: true,
  autostart: false,
  minimizeOnPlay: true,
  notifNews: true,
  notifStatus: true,
  notifSound: false,
  accountCreatedUtc: '',
};

// ─── Акцентный цвет: фирменный золотой + пресеты на выбор ──────────────────
const ACCENTS = [
  { id: 'gold',   name: 'Золотой (по умолчанию)', accent: '#fdd015', soft: '#ffe873', deep: '#fc8c06', ink: '#1a1206' },
  { id: 'pink',   name: 'Розовый',                accent: '#ff3d8a', soft: '#ff7ab3', deep: '#c21e63', ink: '#1a0410' },
  { id: 'blue',   name: 'Голубой',                accent: '#4ac3ff', soft: '#8ddcff', deep: '#2e8fdb', ink: '#031420' },
  { id: 'green',  name: 'Зелёный',                accent: '#3fd98a', soft: '#8af0bc', deep: '#22b86b', ink: '#031b10' },
  { id: 'purple', name: 'Фиолетовый',              accent: '#c084fc', soft: '#ddb4ff', deep: '#9d5cf0', ink: '#1a0f26' },
  { id: 'red',    name: 'Красный',                accent: '#ff5d5d', soft: '#ff9a9a', deep: '#e63946', ink: '#210404' },
];

function hexToRgb(hex) {
  const n = parseInt(hex.slice(1), 16);
  return `${(n >> 16) & 255},${(n >> 8) & 255},${n & 255}`;
}

function applyAccent(id) {
  const preset = ACCENTS.find((a) => a.id === id) || ACCENTS[0];
  const root = document.documentElement.style;
  const rgb = hexToRgb(preset.accent);
  root.setProperty('--accent', preset.accent);
  root.setProperty('--accent-soft', preset.soft);
  root.setProperty('--accent-deep', preset.deep);
  root.setProperty('--accent-ink', preset.ink);
  root.setProperty('--accent-dim', preset.deep);
  root.setProperty('--accent-wash', `rgba(${rgb},.14)`);
  root.setProperty('--accent-hover', `rgba(${rgb},.1)`);
  root.setProperty('--accent-glow', `rgba(${rgb},.18)`);
  root.setProperty('--accent-shadow', `rgba(${rgb},.55)`);
  root.setProperty('--accent-shadow-strong', `rgba(${rgb},.7)`);

  document.querySelectorAll('.accent-swatch').forEach((el) => {
    el.classList.toggle('selected', el.dataset.accent === preset.id);
  });
}

function renderAccentPicker() {
  const el = document.getElementById('accent-picker');
  el.innerHTML = ACCENTS.map(
    (a) => `<button class="accent-swatch" data-accent="${a.id}" title="${a.name}"
      style="background:linear-gradient(135deg, ${a.soft}, ${a.accent} 55%, ${a.deep})">
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

function applySettingsToUI() {
  document.getElementById('set-nickname').value = settings.nickname;
  document.getElementById('set-gtapath').value = settings.gtaPath;
  document.getElementById('set-host').value = settings.serverHost;
  document.getElementById('set-port').value = settings.serverPort;
  document.getElementById('set-autoupdate').checked = settings.autoUpdate;
  document.getElementById('set-language').value = settings.language;
  document.getElementById('set-animations').checked = settings.animations;
  document.getElementById('set-autostart').checked = settings.autostart;
  document.getElementById('set-minimizeonplay').checked = settings.minimizeOnPlay;
  document.getElementById('set-notif-news').checked = settings.notifNews;
  document.getElementById('set-notif-status').checked = settings.notifStatus;
  document.getElementById('set-notif-sound').checked = settings.notifSound;
  setEditionToggle(settings.clientEdition);
  applyAccent(settings.accentColor);

  document.getElementById('account-nick').textContent = settings.nickname;
  document.getElementById('avatar-initial').textContent = (settings.nickname || 'И')[0].toUpperCase();
}

// ─── Доп. переключатели вкладки «Внешний вид» / автозапуск / уведомления ──
const SIMPLE_TOGGLES = [
  ['set-language', 'language', 'value'],
  ['set-animations', 'animations', 'checked'],
  ['set-minimizeonplay', 'minimizeOnPlay', 'checked'],
  ['set-notif-news', 'notifNews', 'checked'],
  ['set-notif-status', 'notifStatus', 'checked'],
  ['set-notif-sound', 'notifSound', 'checked'],
];
SIMPLE_TOGGLES.forEach(([id, key, prop]) => {
  document.getElementById(id).addEventListener(prop === 'checked' ? 'change' : 'input', (e) => {
    settings[key] = e.target[prop];
    saveSettingsDebounced();
  });
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
document.getElementById('btn-open-settings').addEventListener('click', () => settingsOverlay.classList.remove('hidden'));
document.getElementById('settings-close').addEventListener('click', () => settingsOverlay.classList.add('hidden'));
settingsOverlay.addEventListener('click', (e) => { if (e.target === settingsOverlay) settingsOverlay.classList.add('hidden'); });
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && !settingsOverlay.classList.contains('hidden')) settingsOverlay.classList.add('hidden');
});

// ─── Настройки: категории слева (Основное/Дополнительно/О программе) ───────
document.querySelectorAll('.settings-subnav [data-subtab]').forEach((btn) => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.settings-subnav [data-subtab]').forEach((b) => b.classList.remove('active'));
    document.querySelectorAll('.settings-tab').forEach((t) => t.classList.remove('active'));
    btn.classList.add('active');
    document.querySelector(`.settings-tab[data-subtab="${btn.dataset.subtab}"]`).classList.add('active');
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
    if (settings.minimizeOnPlay) {
      setTimeout(() => window.floridaV.minimize(), 1500);
    }
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
