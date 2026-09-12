/**
 * Extended mock data for the full Hybrid dashboard prototype (/concepts/hybrid/app).
 * Static / illustrative only.
 */

export interface AppProject {
  id: string;
  name: string;
  slug: string;
  tag: string;
  accent: string;
  banner: string;
  online: number;
  peak: number;
  servers: number;
  createdAt: string;
  discord: string;
  website: string;
  members: { name: string; role: 'Owner' | 'Administrator' | 'Developer'; initials: string }[];
}

export const APP_PROJECTS: AppProject[] = [
  {
    id: 'florida-v',
    name: 'Florida V',
    slug: 'florida-v',
    tag: 'Флагманский RP',
    accent: '#ff1493',
    banner: '/branding/hero-showcase.png',
    online: 1840,
    peak: 2310,
    servers: 4,
    createdAt: '2026-03-14',
    discord: 'discord.gg/floridav',
    website: 'floridav.gg',
    members: [
      { name: 'Mikhail D.', role: 'Owner', initials: 'MD' },
      { name: 'Anna K.', role: 'Administrator', initials: 'AK' },
      { name: 'Sergey P.', role: 'Developer', initials: 'SP' },
      { name: 'Iryna V.', role: 'Developer', initials: 'IV' },
    ],
  },
  {
    id: 'sayonara-rp',
    name: 'Sayonara RP',
    slug: 'sayonara-rp',
    tag: 'Хардкор RP',
    accent: '#8b5cf6',
    banner: '/branding/logo-banner-1.png',
    online: 920,
    peak: 1180,
    servers: 2,
    createdAt: '2026-05-02',
    discord: 'discord.gg/sayonara',
    website: 'sayonara-rp.com',
    members: [
      { name: 'Kirill M.', role: 'Owner', initials: 'KM' },
      { name: 'Dasha L.', role: 'Developer', initials: 'DL' },
    ],
  },
  {
    id: 'capital-rp',
    name: 'Capital City RP',
    slug: 'capital-rp',
    tag: 'Custom City Map',
    accent: '#a855f7',
    banner: '/branding/logo-banner-2.png',
    online: 610,
    peak: 1500,
    servers: 3,
    createdAt: '2026-06-20',
    discord: 'discord.gg/capitalrp',
    website: 'capitalrp.ru',
    members: [
      { name: 'Owner', role: 'Owner', initials: 'OW' },
      { name: 'Roman T.', role: 'Administrator', initials: 'RT' },
    ],
  },
];

export interface AppServer {
  id: string;
  name: string;
  env: 'production' | 'development' | 'test';
  status: 'online' | 'offline' | 'deploying';
  ip: string;
  port: number;
  version: string;
  online: number;
  slots: number;
  cpu: number;
  ram: number;
  tick: number;
  uptime: string;
}

export const APP_SERVERS: AppServer[] = [
  { id: 's1', name: 'florida-prod-01', env: 'production', status: 'online', ip: '188.127.229.224', port: 7788, version: 'v16.4.39', online: 1180, slots: 1500, cpu: 61, ram: 68, tick: 58.9, uptime: '21д 4ч' },
  { id: 's2', name: 'florida-prod-02', env: 'production', status: 'online', ip: '188.127.229.225', port: 7788, version: 'v16.4.39', online: 660, slots: 1500, cpu: 44, ram: 52, tick: 59.7, uptime: '21д 4ч' },
  { id: 's3', name: 'florida-dev', env: 'development', status: 'deploying', ip: '188.127.229.226', port: 7790, version: 'v16.5.0-rc2', online: 12, slots: 128, cpu: 18, ram: 33, tick: 60, uptime: '2ч 11м' },
  { id: 's4', name: 'florida-test', env: 'test', status: 'offline', ip: '188.127.229.226', port: 7791, version: 'v16.5.0-rc2', online: 0, slots: 64, cpu: 0, ram: 4, tick: 0, uptime: '—' },
];

export const LOG_ROWS = [
  { time: '2026-09-06 12:41:07', level: 'INFO', source: 'network', msg: 'player connected id=41 "Mikhail_D" ip=***.***.14.2' },
  { time: '2026-09-06 12:41:05', level: 'INFO', source: 'license', msg: 'license FLV-xxxx verified · slots 1500 · sig ok' },
  { time: '2026-09-06 12:41:04', level: 'OK', source: 'fastdl', msg: 'FastDL manifest verified (2 184 files, 6.1 GB)' },
  { time: '2026-09-06 12:40:58', level: 'WARN', source: 'gamemode', msg: 'tick spike 22ms on resource econ_core' },
  { time: '2026-09-06 12:40:32', level: 'INFO', source: 'api', msg: 'API key rotated · project florida-v · by Anna K.' },
  { time: '2026-09-06 12:39:11', level: 'ERROR', source: 'voice', msg: 'webrtc renegotiation failed for peer 88 — retry scheduled' },
  { time: '2026-09-06 12:38:00', level: 'INFO', source: 'agent', msg: 'deploy florida-prod-01 -> v16.4.39 completed in 41s' },
  { time: '2026-09-06 12:31:22', level: 'INFO', source: 'integration', msg: 'Discord webhook delivered (status change)' },
  { time: '2026-09-06 12:20:05', level: 'OK', source: 'db', msg: 'MariaDB pool healthy · 12/50 connections' },
  { time: '2026-09-06 12:04:49', level: 'WARN', source: 'monitoring', msg: 'CPU 82% on florida-prod-01 for 3m — alert sent' },
];

export const API_KEYS = [
  { id: 'k1', name: 'Website widget', token: 'flv_pub_a4c1e0b9xxxxxxxxxxxxa91f', scope: 'read', created: '2026-06-01', lastUsed: '2 мин назад' },
  { id: 'k2', name: 'Launcher connect', token: 'flv_live_77b2f1d3xxxxxxxxxxxx77c2', scope: 'read · connect', created: '2026-06-01', lastUsed: '18 сек назад' },
  { id: 'k3', name: 'Discord bot', token: 'flv_live_de04a7c8xxxxxxxxxxxxde04', scope: 'read · write', created: '2026-07-12', lastUsed: '1 ч назад' },
];

export const WEBHOOKS = [
  { id: 'w1', url: 'https://discord.com/api/webhooks/•••/•••', events: ['server.status', 'deploy.finished'], status: 'active' },
  { id: 'w2', url: 'https://api.myproject.com/flov-hook', events: ['player.peak', 'alert.raised'], status: 'active' },
  { id: 'w3', url: 'https://hooks.slack.com/services/•••', events: ['alert.raised'], status: 'paused' },
];

export const SDK_DOWNLOADS = [
  { name: 'FloV:MP Server Core', file: 'flovmp-server-x64-linux.tar.gz', size: '148 MB', ver: 'v16.4.39', kind: 'core' },
  { name: 'Multiplayer Client', file: 'flovmp-client-setup.exe', size: '92 MB', ver: 'v16.4.39', kind: 'client' },
  { name: 'C# SDK (.NET 8)', file: 'FloVMP.Sdk.1.4.2.nupkg', size: '1.1 MB', ver: '1.4.2', kind: 'sdk' },
  { name: 'JS/TS SDK', file: 'flovmp-sdk-1.4.2.tgz', size: '0.8 MB', ver: '1.4.2', kind: 'sdk' },
  { name: 'Launcher SDK', file: 'flovmp-launcher-sdk.zip', size: '14 MB', ver: '0.9.0', kind: 'launcher' },
  { name: 'Документация (offline)', file: 'flovmp-docs.pdf', size: '6 MB', ver: '2026.09', kind: 'docs' },
];

export const SNIPPETS: { id: string; label: string; code: string }[] = [
  {
    id: 'csharp',
    label: 'C# · verify',
    code: [
      'using FloVMP.Sdk;',
      '',
      'var flov = new FloVClient(Environment.GetEnvironmentVariable("FLOVMP_KEY")!);',
      'var lic  = await flov.License.VerifyAsync(serverIp: "188.127.229.224");',
      '',
      'if (!lic.Valid) { Console.Error.WriteLine(lic.Reason); return 1; }',
      'Console.WriteLine($"{lic.Project} · {lic.MaxPlayers} slots · lifetime");',
    ].join('\n'),
  },
  {
    id: 'ts',
    label: 'TypeScript · stats',
    code: [
      "import { FloVClient } from '@flovmp/sdk';",
      '',
      'const flov = new FloVClient(process.env.FLOVMP_KEY!);',
      "const { online, peak, servers } = await flov.projects.stats('florida-v');",
      '',
      'console.log(`online ${online} · peak ${peak} · ${servers} servers`);',
    ].join('\n'),
  },
  {
    id: 'curl',
    label: 'cURL · webhook test',
    code: [
      'curl -X POST https://api.flovmp.dev/v1/projects/florida-v/webhooks/test \\',
      '  -H "Authorization: Bearer $FLOVMP_KEY" \\',
      '  -d \'{ "event": "server.status", "sample": true }\'',
    ].join('\n'),
  },
];

export const INTEGRATIONS = [
  { name: 'Discord', desc: 'Статусы серверов, алерты и логи в канал', connected: true, accent: '#5865f2' },
  { name: 'Telegram', desc: 'Уведомления и алерты в чат/канал', connected: true, accent: '#2aabee' },
  { name: 'REDL Hosting', desc: 'Деплой на инфраструктуру REDL в один клик', connected: false, accent: '#ff1493' },
  { name: 'Webhooks', desc: 'Произвольные HTTP-хуки на события платформы', connected: true, accent: '#a855f7' },
  { name: 'Grafana', desc: 'Экспорт метрик мониторинга (Prometheus)', connected: false, accent: '#f46800' },
  { name: 'GitHub', desc: 'Авто-деплой из ветки при пуше', connected: false, accent: '#e9eaee' },
];

export const AI_THREAD: { role: 'user' | 'assistant'; text: string }[] = [
  { role: 'user', text: 'Сервер florida-prod-01 держит tick 58.9 вместо 60 — из-за чего?' },
  {
    role: 'assistant',
    text:
      'По логам за последний час вижу повторяющийся «tick spike 22ms on resource econ_core». Это main-thread работа в econ_core.\n\nЧто проверить:\n1. Тяжёлые синхронные запросы к БД в цикле экономики — вынести в async / кэш.\n2. O(n²) обход игроков при пересчёте зарплат — сейчас 1180 онлайн.\n3. Профиль: perf record resource econ_core 30s в консоли.\n\nОриентир: на v16.5.0-rc2 econ_core переписан на батч-апдейты — стоит прогнать на florida-dev.',
  },
  { role: 'user', text: 'Ок, а как включить батч-режим на rc2?' },
  {
    role: 'assistant',
    text:
      'В server.toml проекта:\n\n[gamemode.econ]\nbatch = true\nbatch_interval_ms = 250\n\nПосле рестарта проверь «econ_core: batch scheduler active» в консоли.',
  },
];

export const DOCS_TREE = [
  { group: 'Getting started', items: ['Обзор платформы', 'Первый проект', 'Привязка сервера', 'server.toml'] },
  { group: 'SDK', items: ['C# SDK', 'JS/TS SDK', 'Events reference', 'Примеры'] },
  { group: 'API', items: ['Аутентификация', 'Projects', 'Servers', 'Webhooks', 'Rate limits'] },
  { group: 'Operations', items: ['Console & Logs', 'Мониторинг и алерты', 'Деплой', 'FastDL CDN'] },
];

export const ALERTS = [
  { level: 'warn', title: 'CPU 82% · florida-prod-01', time: '12:04', note: 'Держится 3 минуты. Отправлено в Discord + Telegram.' },
  { level: 'info', title: 'Деплой завершён · florida-prod-01', time: '12:38', note: 'v16.4.39 · 41s · без даунтайма.' },
  { level: 'error', title: 'florida-test offline', time: '11:30', note: 'Остановлен вручную. Авто-рестарт отключён.' },
];

export const ANALYTICS_SERIES = {
  onlineDay: [0.32, 0.28, 0.24, 0.22, 0.26, 0.34, 0.48, 0.62, 0.71, 0.78, 0.83, 0.86, 0.84, 0.8, 0.77, 0.79, 0.85, 0.92, 0.97, 1.0, 0.95, 0.82, 0.64, 0.45],
  cpuDay: [0.4, 0.38, 0.36, 0.35, 0.37, 0.42, 0.5, 0.58, 0.63, 0.67, 0.7, 0.72, 0.71, 0.69, 0.66, 0.68, 0.72, 0.79, 0.84, 0.82, 0.75, 0.62, 0.5, 0.44],
  ramDay: [0.55, 0.55, 0.54, 0.54, 0.55, 0.57, 0.6, 0.62, 0.64, 0.66, 0.67, 0.68, 0.68, 0.67, 0.66, 0.67, 0.69, 0.71, 0.73, 0.72, 0.7, 0.65, 0.6, 0.57],
  netDay: [0.2, 0.18, 0.16, 0.15, 0.17, 0.24, 0.36, 0.5, 0.6, 0.66, 0.72, 0.75, 0.73, 0.7, 0.66, 0.69, 0.75, 0.84, 0.92, 0.96, 0.88, 0.7, 0.5, 0.34],
};

export const DAY_LABELS = ['00', '', '', '03', '', '', '06', '', '', '09', '', '', '12', '', '', '15', '', '', '18', '', '', '21', '', ''];

export interface AppResource {
  id: string;
  name: string;
  type: 'script' | 'map' | 'vehicle' | 'ui';
  status: 'running' | 'stopped' | 'failed';
  version: string;
  dependencies: string[];
  memoryMb: number;
}

export const APP_RESOURCES: AppResource[] = [
  { id: 'r1', name: 'flovmp-core', type: 'script', status: 'running', version: '1.0.0', dependencies: [], memoryMb: 124 },
  { id: 'r2', name: 'flovmp-auth-nui', type: 'ui', status: 'running', version: '0.9.4', dependencies: ['flovmp-core'], memoryMb: 18 },
  { id: 'r3', name: 'custom-city-map', type: 'map', status: 'running', version: '2026.8', dependencies: [], memoryMb: 412 },
  { id: 'r4', name: 'flovmp-vehicles-pack', type: 'vehicle', status: 'running', version: '2.4.1', dependencies: [], memoryMb: 350 },
  { id: 'r5', name: 'flovmp-inventory', type: 'ui', status: 'running', version: '1.2.0', dependencies: ['flovmp-core'], memoryMb: 24 },
  { id: 'r6', name: 'flovmp-economy', type: 'script', status: 'running', version: '1.1.5', dependencies: ['flovmp-core'], memoryMb: 45 },
  { id: 'r7', name: 'casino-event-zone', type: 'script', status: 'stopped', version: '0.5.0', dependencies: ['flovmp-core'], memoryMb: 0 },
];

export interface AppCrashIncident {
  id: string;
  server: string;
  time: string;
  type: 'Freeze (>15s)' | 'CoreCLR Fatal Exception' | 'Memory Leak';
  reason: string;
  stackPreview: string;
  autoRestarted: boolean;
}

export const APP_CRASHES: AppCrashIncident[] = [
  {
    id: 'INC-4091',
    server: 'florida-prod-01',
    time: '2026-09-06 04:12:19',
    type: 'Freeze (>15s)',
    reason: 'Main thread tick freeze (16.2s unresponsive)',
    stackPreview: 'at FloVMP.Core.Economy.EconomyService.RecalculateTaxes() in EconomyService.cs:line 182\n   at System.Threading.Monitor.Enter(Object obj)',
    autoRestarted: true,
  },
  {
    id: 'INC-3982',
    server: 'florida-dev',
    time: '2026-09-05 19:40:02',
    type: 'CoreCLR Fatal Exception',
    reason: 'System.NullReferenceException: Object reference not set',
    stackPreview: 'at FloVMP.Gamemode.CustomPeds.OnSpawnPed(IPlayer player) in CustomPeds.cs:line 44',
    autoRestarted: true,
  },
];

export interface AppInvoice {
  id: string;
  project: string;
  plan: 'Starter' | 'Pro' | 'Enterprise';
  slots: number;
  amount: number;
  status: 'paid' | 'pending';
  date: string;
  expires: string;
}

export const APP_INVOICES: AppInvoice[] = [
  { id: 'INV-2026-0901', project: 'Florida V', plan: 'Enterprise', slots: 1500, amount: 24900, status: 'paid', date: '2026-09-01', expires: '2026-10-01' },
  { id: 'INV-2026-0801', project: 'Florida V', plan: 'Enterprise', slots: 1500, amount: 24900, status: 'paid', date: '2026-08-01', expires: '2026-09-01' },
  { id: 'INV-2026-0701', project: 'Florida V', plan: 'Enterprise', slots: 1500, amount: 24900, status: 'paid', date: '2026-07-01', expires: '2026-08-01' },
  { id: 'INV-2026-0615', project: 'Sayonara RP', plan: 'Pro', slots: 500, amount: 11900, status: 'paid', date: '2026-06-15', expires: '2026-07-15' },
];

