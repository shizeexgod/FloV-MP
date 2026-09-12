/**
 * Shared mock data for the 3 FloV:MP design concepts.
 * Static / illustrative only — no API calls. Used purely for visual exploration.
 */

export const PLATFORM_STATS = [
  { key: 'projects', label: 'Активные проекты', value: 47, suffix: '', delta: '+6 / мес' },
  { key: 'servers', label: 'Активные серверы', value: 128, suffix: '', delta: '+21 / мес' },
  { key: 'players', label: 'Игроков онлайн', value: 9340, suffix: '', delta: 'пик 12 480' },
  { key: 'uptime', label: 'Аптайм платформы', value: 99.98, suffix: '%', delta: '90 дней' },
];

export interface ConceptProject {
  name: string;
  tag: string;
  status: 'online' | 'maintenance' | 'offline';
  online: number;
  peak: number;
  servers: number;
  accent: string;
  banner: string;
}

export const PROJECTS: ConceptProject[] = [
  {
    name: 'Florida V',
    tag: 'Флагманский RP',
    status: 'online',
    online: 1840,
    peak: 2310,
    servers: 3,
    accent: '#ff1493',
    banner: '/branding/hero-showcase.png',
  },
  {
    name: 'Sayonara RP',
    tag: 'Хардкор RP',
    status: 'online',
    online: 920,
    peak: 1180,
    servers: 2,
    accent: '#8b5cf6',
    banner: '/branding/logo-banner-1.png',
  },
  {
    name: 'Capital City RP',
    tag: 'Custom Map',
    status: 'maintenance',
    online: 610,
    peak: 1500,
    servers: 4,
    accent: '#a855f7',
    banner: '/branding/logo-banner-2.png',
  },
  {
    name: 'Los Santos Life',
    tag: 'Casual RP',
    status: 'online',
    online: 430,
    peak: 780,
    servers: 1,
    accent: '#ff007f',
    banner: '/branding/hero-showcase.png',
  },
];

export interface ConceptServer {
  name: string;
  env: 'production' | 'development' | 'test';
  status: 'online' | 'offline' | 'deploying';
  online: number;
  slots: number;
  cpu: number;
  ram: number;
  version: string;
}

export const SERVERS: ConceptServer[] = [
  { name: 'florida-prod-01', env: 'production', status: 'online', online: 1180, slots: 1500, cpu: 61, ram: 68, version: 'v16.4.39' },
  { name: 'florida-prod-02', env: 'production', status: 'online', online: 660, slots: 1500, cpu: 44, ram: 52, version: 'v16.4.39' },
  { name: 'florida-dev', env: 'development', status: 'deploying', online: 12, slots: 128, cpu: 18, ram: 33, version: 'v16.5.0-rc2' },
  { name: 'florida-test', env: 'test', status: 'offline', online: 0, slots: 64, cpu: 0, ram: 4, version: 'v16.5.0-rc2' },
];

export const SIDEBAR = [
  'Dashboard',
  'Projects',
  'Servers',
  'Analytics',
  'Console',
  'Logs',
  'API',
  'SDK',
  'Integrations',
  'Documentation',
  'AI Assistant',
  'Settings',
];

export const ACTIVITY = [
  { time: '12:41', text: 'Деплой florida-prod-01 · v16.4.39', kind: 'deploy' },
  { time: '12:20', text: 'API-ключ создан · project “Florida V”', kind: 'api' },
  { time: '11:58', text: 'Пик онлайна: 12 480 игроков', kind: 'peak' },
  { time: '11:30', text: 'florida-test остановлен вручную', kind: 'stop' },
  { time: '10:12', text: 'Вебхук Discord подключён', kind: 'integration' },
];

export const CONSOLE_LINES = [
  '[12:41:03] [INFO ] EntityStreamer initialised — grid 512',
  '[12:41:03] [INFO ] NetworkWorker listening udp/7788',
  '[12:41:04] [INFO ] csharp-module loaded flovmp-gamemode 1.4.2',
  '[12:41:04] [ OK  ] FastDL manifest verified (2 184 files)',
  '[12:41:05] [INFO ] License FLV-•••• verified · slots 1500',
  '[12:41:07] [INFO ] player connected  id=41  "Mikhail_D"',
  '[12:41:09] [WARN ] tick spike 22ms on resource econ_core',
  '[12:41:12] [INFO ] player connected  id=42  "Anna.K"',
];

/** 0..1 normalised series for the hand-rolled SVG charts */
export const SERIES = {
  onlineWeek: [0.42, 0.48, 0.55, 0.51, 0.63, 0.86, 0.78],
  onlineMonth: [0.3, 0.34, 0.4, 0.38, 0.46, 0.52, 0.5, 0.58, 0.63, 0.67, 0.72, 0.81],
  projectGrowth: [0.15, 0.19, 0.26, 0.3, 0.37, 0.45, 0.52, 0.61, 0.68, 0.79, 0.88, 1.0],
  cpu: [0.35, 0.4, 0.52, 0.48, 0.6, 0.58, 0.66, 0.62, 0.7, 0.61, 0.55, 0.59],
  ram: [0.5, 0.52, 0.55, 0.57, 0.6, 0.62, 0.63, 0.66, 0.68, 0.67, 0.69, 0.71],
  net: [0.2, 0.35, 0.3, 0.5, 0.42, 0.65, 0.58, 0.72, 0.6, 0.8, 0.74, 0.9],
};

export const WEEK_LABELS = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'];
export const MONTH_LABELS = ['Я', 'Ф', 'М', 'А', 'М', 'И', 'И', 'А', 'С', 'О', 'Н', 'Д'];

export const FEATURES = [
  { title: 'Multiplayer Engine', desc: 'Автономная сетевая инфраструктура на отвязанных бинарниках. UDP-стек, стриминг сущностей, интерполяция.' },
  { title: 'Dashboard', desc: 'Управление проектами и серверами через веб: старт/стоп/рестарт, ресурсы, релизы.' },
  { title: 'SDK', desc: 'C# .NET 8 и JS/TS тулкит: типы, хелперы, примеры, локальный раннер.' },
  { title: 'API', desc: 'REST + вебхуки для сайта проекта, лаунчера, Discord-ботов и внешней аналитики.' },
  { title: 'Voice Chat', desc: 'Встроенный WebRTC 3D-войс: зоны, рации, мегафоны — без сторонних сервисов.' },
  { title: 'Documentation', desc: 'Документация внутри платформы: поиск, категории, гайды по SDK и API.' },
  { title: 'Launcher Integration', desc: 'Сборка кастомного лаунчера проекта: бренд, цвет, прямой коннект.' },
  { title: 'Project Management', desc: 'Команды и роли: Owner / Administrator / Developer, права по ролям.' },
];

export const ROADMAP = [
  { q: 'Q3 2026', title: 'Core + Dashboard', state: 'done', items: ['Автономный рантайм', 'Проекты и серверы', 'Console / Logs'] },
  { q: 'Q4 2026', title: 'SDK & API', state: 'active', items: ['C# / TS SDK', 'API-ключи, вебхуки', 'Документация в платформе'] },
  { q: 'Q1 2027', title: 'Automation', state: 'next', items: ['Launcher Builder', 'Авто-деплой', 'Интеграции хостинга'] },
  { q: 'Q2 2027', title: 'Scale', state: 'next', items: ['Расширенная аналитика', 'Планы подписки', 'AI Assistant GA'] },
];

export const LICENSE = {
  price: '24 000 ₽',
  name: 'Lifetime License',
  note: 'Одна лицензия = один проект. Серверов внутри проекта — сколько нужно.',
  includes: [
    'Мультиплеер-клиент и серверное ядро',
    'Dashboard управления проектом',
    'SDK · API · вебхуки',
    'Документация и примеры',
    'Кастомный лаунчер проекта',
    'Все обновления платформы',
  ],
};
