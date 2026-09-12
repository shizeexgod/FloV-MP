'use client';

import React, { useMemo, useState } from 'react';
import {
  Activity,
  ArrowRight,
  Bell,
  BookOpen,
  Boxes,
  Check,
  ChevronRight,
  Circle,
  Command,
  Copy,
  Cpu,
  CreditCard,
  Database,
  Download,
  FolderTree,
  Gauge,
  Globe,
  KeyRound,
  LayoutDashboard,
  LifeBuoy,
  Link2,
  Megaphone,
  MemoryStick,
  Paintbrush,
  Play,
  Plug,
  Plus,
  Power,
  Radio,
  RotateCw,
  Search,
  Send,
  Server,
  Settings,
  ShieldAlert,
  Sparkles,
  Terminal,
  Timer,
  Trash2,
  Users,
} from 'lucide-react';
import { AreaChart, Donut, LineChart, Reveal } from '../../_ui';
import {
  ACTIVITY,
  MONTH_LABELS,
  SERIES,
  SERVERS,
} from '../../_data';
import {
  AI_THREAD,
  ALERTS,
  ANALYTICS_SERIES,
  API_KEYS,
  APP_CRASHES,
  APP_INVOICES,
  APP_PROJECTS,
  APP_RESOURCES,
  APP_SERVERS,
  DAY_LABELS,
  DOCS_TREE,
  INTEGRATIONS,
  LOG_ROWS,
  SDK_DOWNLOADS,
  SNIPPETS,
  WEBHOOKS,
  type AppCrashIncident,
  type AppProject,
  type AppResource,
} from '../../_appdata';

const PINK = '#ff1493';
const PURPLE = '#a855f7';

const NAV: { id: Module; label: string }[] = [
  { id: 'dashboard', label: 'Dashboard' },
  { id: 'projects', label: 'Projects' },
  { id: 'servers', label: 'Servers' },
  { id: 'resources', label: 'Resources' },
  { id: 'watchdog', label: 'Watchdog' },
  { id: 'analytics', label: 'Analytics' },
  { id: 'console', label: 'Console' },
  { id: 'logs', label: 'Logs' },
  { id: 'launcher', label: 'Launcher Builder' },
  { id: 'billing', label: 'Billing' },
  { id: 'api', label: 'API' },
  { id: 'sdk', label: 'SDK' },
  { id: 'integrations', label: 'Integrations' },
  { id: 'docs', label: 'Documentation' },
  { id: 'ai', label: 'AI Assistant' },
  { id: 'settings', label: 'Settings' },
];

type Module =
  | 'dashboard'
  | 'projects'
  | 'servers'
  | 'resources'
  | 'watchdog'
  | 'analytics'
  | 'console'
  | 'logs'
  | 'launcher'
  | 'billing'
  | 'api'
  | 'sdk'
  | 'integrations'
  | 'docs'
  | 'ai'
  | 'settings';

export default function HybridApp() {
  const [mod, setMod] = useState<Module>('dashboard');
  const [projectId, setProjectId] = useState(APP_PROJECTS[0].id);
  const project = useMemo(() => APP_PROJECTS.find((p) => p.id === projectId)!, [projectId]);

  const [broadcastOpen, setBroadcastOpen] = useState(false);
  const [broadcastMsg, setBroadcastMsg] = useState('');
  const [broadcastSent, setBroadcastSent] = useState(false);

  const currentNav = NAV.find((n) => n.id === mod);

  return (
    <div className="flex min-h-screen bg-[#08080a] font-sans text-[#e9eaee] antialiased">
      {/* ============ SIDEBAR ============ */}
      <aside className="sticky top-0 hidden h-screen w-56 flex-none flex-col border-r border-white/[0.07] bg-[#08080a] p-3 lg:flex">
        <div className="flex items-center gap-2 px-2 py-2">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/branding/logo.jpg" alt="" className="h-5 w-5 rounded" />
          <span className="text-[13px] font-semibold tracking-tight text-white">FloV:MP</span>
        </div>

        <nav className="mt-3 flex-1 space-y-0.5 overflow-y-auto">
          {NAV.map((n) => (
            <button
              key={n.id}
              onClick={() => setMod(n.id as Module)}
              className={`flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-left text-[12.5px] transition ${
                mod === n.id ? 'bg-white/[0.06] text-white font-medium' : 'text-white/45 hover:text-white/80'
              }`}
            >
              <span className="h-1 w-1 rounded-full" style={{ background: mod === n.id ? PINK : 'transparent' }} />
              {n.label}
            </button>
          ))}
        </nav>
      </aside>

      {/* ============ MAIN ============ */}
      <div className="flex min-w-0 flex-1 flex-col">
        {/* top bar */}
        <header className="sticky top-0 z-20 flex h-14 items-center justify-between border-b border-white/[0.07] bg-[#08080a] px-5">
          <div className="flex items-center gap-2 text-[13px] text-white/50">
            <span className="text-white/80 font-medium">Florida V</span>
            <ChevronRight className="h-3.5 w-3.5 text-white/30" />
            <span>{currentNav?.label}</span>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={() => setBroadcastOpen(true)}
              className="flex items-center gap-1.5 rounded-lg border border-pink-500/30 bg-pink-500/10 px-2.5 py-1 text-[11.5px] font-medium text-pink-400 hover:bg-pink-500/20 transition"
            >
              <Megaphone className="h-3 w-3" />
              <span>Объявление /o</span>
            </button>
            <div className="flex items-center gap-2 rounded-lg border border-white/10 bg-white/[0.02] px-2 py-1 text-[11px] text-white/35">
              <Command className="h-3 w-3" /> K
            </div>
          </div>
        </header>

        {/* mobile nav */}
        <div className="flex gap-1 overflow-x-auto border-b border-white/[0.07] px-3 py-2 lg:hidden">
          {NAV.map((n) => (
            <button
              key={n.id}
              onClick={() => setMod(n.id as Module)}
              className={`flex-none rounded-lg px-2.5 py-1 text-[11.5px] transition ${
                mod === n.id ? 'bg-white/[0.08] text-white font-medium' : 'text-white/45'
              }`}
            >
              {n.label}
            </button>
          ))}
        </div>

        <main className="flex-1 overflow-y-auto p-5">
          <Reveal key={mod + projectId}>
            {mod === 'dashboard' && <DashboardMod project={project} />}
            {mod === 'projects' && <ProjectsMod project={project} onSelect={setProjectId} />}
            {mod === 'servers' && <ServersMod />}
            {mod === 'resources' && <ResourcesMod />}
            {mod === 'watchdog' && <WatchdogMod />}
            {mod === 'analytics' && <AnalyticsMod />}
            {mod === 'console' && <ConsoleMod />}
            {mod === 'logs' && <LogsMod />}
            {mod === 'launcher' && <LauncherMod project={project} />}
            {mod === 'billing' && <BillingMod />}
            {mod === 'api' && <ApiMod />}
            {mod === 'sdk' && <SdkMod />}
            {mod === 'integrations' && <IntegrationsMod />}
            {mod === 'docs' && <DocsMod />}
            {mod === 'ai' && <AiMod />}
            {mod === 'settings' && <SettingsMod project={project} />}
          </Reveal>
          <div className="h-10" />
        </main>
      </div>

      {/* Broadcast Modal */}
      {broadcastOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 p-4 backdrop-blur-md">
          <div className="hyapp-card w-full max-w-md rounded-2xl p-5">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Megaphone className="h-4 w-4" style={{ color: PINK }} />
                <h3 className="text-[14px] font-semibold text-white">Глобальное объявление (/o)</h3>
              </div>
              <button onClick={() => setBroadcastOpen(false)} className="text-white/40 hover:text-white">✕</button>
            </div>
            <p className="mt-1 text-[12px] text-white/50">
              Сообщение будет отправлено всем онлайн-игрокам на серверах проекта через RemoteServerAgent.
            </p>
            <textarea
              rows={3}
              value={broadcastMsg}
              onChange={(e) => setBroadcastMsg(e.target.value)}
              placeholder="Введите текст объявления... (например: Технические работы через 15 минут)"
              className="mt-3 w-full rounded-xl border border-white/10 bg-white/[0.03] p-3 text-[12.5px] text-white outline-none focus:border-pink-500/50"
            />
            <div className="mt-4 flex items-center justify-between">
              <span className="font-mono text-[11px] text-white/40">{broadcastMsg.length}/256 симв.</span>
              <div className="flex gap-2">
                <button
                  onClick={() => setBroadcastOpen(false)}
                  className="rounded-lg border border-white/10 px-3 py-1.5 text-[12px] text-white/60 hover:text-white"
                >
                  Отмена
                </button>
                <button
                  onClick={() => {
                    if (!broadcastMsg.trim()) return;
                    setBroadcastSent(true);
                    setTimeout(() => {
                      setBroadcastSent(false);
                      setBroadcastOpen(false);
                      setBroadcastMsg('');
                    }, 1200);
                  }}
                  className="hyapp-grad rounded-lg px-4 py-1.5 text-[12px] font-semibold text-white"
                >
                  {broadcastSent ? '✓ Отправлено!' : 'Отправить в /o'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/* ================================================================= *
 *  Shared bits
 * ================================================================= */
function Card({ children, className = '' }: { children: React.ReactNode; className?: string; grad?: boolean }) {
  return (
    <div className={`rounded-xl border border-white/[0.08] bg-white/[0.015] ${className}`}>{children}</div>
  );
}

function H({ children, sub }: { children: React.ReactNode; sub?: string }) {
  return (
    <div className="mb-4">
      <h1 className="text-lg font-semibold tracking-tight text-white">{children}</h1>
      {sub && <p className="mt-0.5 text-[12px] text-white/45">{sub}</p>}
    </div>
  );
}

function StatusPill({ status }: { status: string }) {
  const map: Record<string, string> = { online: '#3fb984', deploying: '#d8a13a', offline: '#e5484d', active: '#3fb984', paused: '#d8a13a' };
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[11px] capitalize font-medium"
      style={{ borderColor: `${map[status] || '#888'}44`, color: map[status] || '#aaa' }}
    >
      <Circle className="h-1.5 w-1.5 fill-current" />
      {status}
    </span>
  );
}

function MiniBar({ v, color }: { v: number; color: string }) {
  return (
    <div className="h-1.5 w-full overflow-hidden rounded-full bg-white/[0.06]">
      <div className="h-full rounded-full" style={{ width: `${v}%`, background: color }} />
    </div>
  );
}

function CopyBtn({ text }: { text: string }) {
  const [c, setC] = useState(false);
  return (
    <button
      onClick={() => {
        navigator.clipboard?.writeText(text).catch(() => {});
        setC(true);
        setTimeout(() => setC(false), 1500);
      }}
      className="inline-flex items-center gap-1.5 rounded-md border border-white/10 bg-white/[0.03] px-2 py-1 text-[11px] text-white/60 hover:text-white"
    >
      {c ? <Check className="h-3 w-3 text-[#3fb984]" /> : <Copy className="h-3 w-3" />}
      {c ? 'Скопировано' : 'Копировать'}
    </button>
  );
}

/* ================================================================= *
 *  Dashboard (1:1 with Concept Screenshot)
 * ================================================================= */
function DashboardMod({ project }: { project: AppProject }) {
  return (
    <div className="space-y-4">
      {/* 4 Top KPI Cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[
          { icon: Radio, label: 'Игроков онлайн', value: '9 340', sub: 'пик 12 480' },
          { icon: Cpu, label: 'CPU (avg)', value: '54%', sub: '4 сервера' },
          { icon: MemoryStick, label: 'RAM (avg)', value: '61%', sub: '128 / 210 GB' },
          { icon: Timer, label: 'Uptime', value: '99.98%', sub: '90 дней' },
        ].map((k) => (
          <div key={k.label} className="rounded-xl border border-white/[0.08] bg-white/[0.015] p-4">
            <k.icon className="h-4 w-4 text-white/35" />
            <div className="mt-3 text-[1.5rem] font-semibold tracking-tight text-white">{k.value}</div>
            <div className="text-[12px] text-white/45">{k.label}</div>
            <div className="font-mono text-[10px] text-white/30">{k.sub}</div>
          </div>
        ))}
      </div>

      {/* Middle Row: Online Month Chart + Activity */}
      <div className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
        <div className="rounded-xl border border-white/[0.08] bg-white/[0.015] p-4 text-white/70">
          <div className="mb-3 flex items-center justify-between text-[12px] text-white/50">
            <span>Онлайн за месяц</span>
            <span className="font-mono text-[11px] tracking-widest text-white/30">{MONTH_LABELS.join(' ')}</span>
          </div>
          <AreaChart data={SERIES.onlineMonth} color={PINK} height={150} />
        </div>
        <div className="rounded-xl border border-white/[0.08] bg-white/[0.015] p-4">
          <div className="mb-3 text-[12px] text-white/50">Последняя активность</div>
          <ul className="space-y-2.5">
            {ACTIVITY.map((a) => (
              <li key={a.time} className="flex gap-2.5 text-[12px]">
                <span className="font-mono text-white/30">{a.time}</span>
                <span className="text-white/60">{a.text}</span>
              </li>
            ))}
          </ul>
        </div>
      </div>

      {/* Bottom: Servers Table */}
      <div className="overflow-hidden rounded-xl border border-white/[0.08] bg-white/[0.015]">
        <div className="border-b border-white/[0.07] px-4 py-2.5 text-[12px] font-medium text-white/50">
          Серверы проекта
        </div>
        <table className="w-full text-left text-[12px]">
          <tbody className="divide-y divide-white/[0.05]">
            {SERVERS.map((s) => (
              <tr key={s.name} className="text-white/60 hover:bg-white/[0.02]">
                <td className="px-4 py-2.5 font-mono font-medium text-white/80">{s.name}</td>
                <td className="px-4 py-2.5 text-white/35">{s.env}</td>
                <td className="px-4 py-2.5 tabular-nums text-white/75">{s.online} / {s.slots}</td>
                <td className="px-4 py-2.5 tabular-nums text-white/60">CPU {s.cpu}%</td>
                <td className="px-4 py-2.5 tabular-nums text-white/60">RAM {s.ram}%</td>
                <td className="px-4 py-2.5">
                  <span
                    className="font-medium"
                    style={{
                      color:
                        s.status === 'online' ? '#3fb984' : s.status === 'deploying' ? '#d8a13a' : '#e5484d',
                    }}
                  >
                    {s.status}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/* ================================================================= *
 *  Projects + project management
 * ================================================================= */
function ProjectsMod({ project, onSelect }: { project: AppProject; onSelect: (id: string) => void }) {
  return (
    <>
      <div className="mb-5 flex items-end justify-between">
        <H sub="Аккаунт → проекты → серверы. Одна Lifetime-лицензия на проект.">Projects</H>
        <button className="hyapp-grad inline-flex items-center gap-2 rounded-xl px-3.5 py-2 text-[12px] font-semibold text-white">
          <Plus className="h-4 w-4" /> Новый проект
        </button>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        {APP_PROJECTS.map((p) => (
          <button
            key={p.id}
            onClick={() => onSelect(p.id)}
            className={`hyapp-card overflow-hidden rounded-2xl text-left transition ${p.id === project.id ? 'ring-1 ring-white/25' : 'hover:ring-1 hover:ring-white/15'}`}
          >
            <div className="relative h-20 overflow-hidden">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={p.banner} alt="" className="h-full w-full object-cover opacity-55" />
              <div className="absolute inset-0" style={{ background: `linear-gradient(180deg,transparent,rgba(12,12,18,.92)), radial-gradient(120px 60px at 15% 0%, ${p.accent}55, transparent)` }} />
            </div>
            <div className="p-4">
              <div className="flex items-center justify-between">
                <span className="text-[14px] font-semibold">{p.name}</span>
                <StatusPill status="online" />
              </div>
              <div className="mt-0.5 text-[11px] text-white/40">{p.tag}</div>
              <div className="mt-3 flex items-center gap-4 text-[11px] text-white/45">
                <span><b className="text-white/80">{p.online.toLocaleString('ru-RU')}</b> онлайн</span>
                <span><b className="text-white/80">{p.servers}</b> серв.</span>
                <span><b className="text-white/80">{p.members.length}</b> в команде</span>
              </div>
            </div>
          </button>
        ))}
      </div>

      {/* management panel for selected project */}
      <div className="mt-6 grid gap-4 lg:grid-cols-[1.4fr_1fr]">
        <Card grad className="p-5">
          <div className="mb-4 flex items-center gap-2">
            <span className="h-2.5 w-2.5 rounded-full" style={{ background: project.accent }} />
            <h2 className="text-[15px] font-semibold">Управление · {project.name}</h2>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            {[
              ['Название проекта', project.name],
              ['Slug', project.slug],
              ['Website', project.website],
              ['Discord', project.discord],
            ].map(([label, val]) => (
              <label key={label} className="block">
                <span className="mb-1.5 block font-mono text-[10px] uppercase tracking-wider text-white/40">{label}</span>
                <input
                  defaultValue={val}
                  className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-[13px] text-white outline-none focus:border-white/25"
                />
              </label>
            ))}
            <label className="block sm:col-span-2">
              <span className="mb-1.5 block font-mono text-[10px] uppercase tracking-wider text-white/40">Описание</span>
              <textarea
                rows={2}
                defaultValue="Флагманский RP-проект на карте Florida. 4 сервера, экономика, фракции, кастомная одежда и транспорт."
                className="w-full resize-none rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-[13px] text-white outline-none focus:border-white/25"
              />
            </label>
          </div>
          <div className="mt-4 flex gap-2">
            <button className="rounded-lg bg-white px-4 py-2 text-[12px] font-semibold text-black hover:bg-white/90 transition">Сохранить</button>
            <button className="rounded-lg border border-white/10 px-4 py-2 text-[12px] text-white/60 hover:text-white">Отмена</button>
          </div>
        </Card>

        <div className="space-y-4">
          <Card className="p-5">
            <div className="mb-3 flex items-center justify-between">
              <h3 className="text-[13px] font-semibold">Команда</h3>
              <button className="text-[11px] text-white/50 hover:text-white">+ Пригласить</button>
            </div>
            <div className="space-y-2">
              {project.members.map((m) => (
                <div key={m.name} className="flex items-center gap-2.5">
                  <div className="grid h-7 w-7 place-items-center rounded-full text-[10px] font-bold text-white" style={{ background: `linear-gradient(135deg,${PINK},${PURPLE})` }}>
                    {m.initials}
                  </div>
                  <span className="text-[12.5px]">{m.name}</span>
                  <span className="ml-auto rounded-md border border-white/10 px-1.5 py-0.5 font-mono text-[9px] text-white/50">{m.role}</span>
                </div>
              ))}
            </div>
          </Card>

          {/* FloV:ID & HWID Policy */}
          <Card className="p-5">
            <div className="flex items-center justify-between">
              <h3 className="text-[13px] font-semibold text-white">FloV:ID &amp; HWID Политика</h3>
              <span className="rounded bg-pink-500/20 px-2 py-0.5 font-mono text-[10px] font-bold text-pink-400">Strict</span>
            </div>
            <p className="mt-1 text-[11.5px] text-white/45">Регулирование обходчиков банов и мультиаккаунтов на серверах проекта.</p>
            <div className="mt-3 grid gap-2">
              {[
                ['Strict', 'Жёсткий бан по железу и мгновенный отказ обходчикам'],
                ['Lenient', 'Мягкий режим: логирование без кика (дефицит онлайна)'],
                ['Disabled', 'Отключить проверку железа'],
              ].map(([mode, desc]) => (
                <label key={mode} className="flex items-center gap-2.5 rounded-lg border border-white/[0.07] bg-white/[0.02] p-2.5 cursor-pointer transition hover:bg-white/[0.04]">
                  <input type="radio" name="hwid_policy" defaultChecked={mode === 'Strict'} className="accent-pink-500" />
                  <div>
                    <div className="text-[12px] font-semibold text-white">{mode}</div>
                    <div className="text-[10.5px] text-white/45">{desc}</div>
                  </div>
                </label>
              ))}
            </div>
            <div className="mt-3 flex items-center justify-between border-t border-white/[0.07] pt-2.5 text-[12px]">
              <span className="text-white/70">Разрешить VPN / Proxy</span>
              <input type="checkbox" defaultChecked className="accent-pink-500 h-4 w-4 rounded" />
            </div>
            <div className="mt-2 flex items-center justify-between text-[12px]">
              <span className="text-white/70">Лимит аккаунтов на 1 HWID</span>
              <span className="font-mono font-bold text-pink-400">3 аккаунта</span>
            </div>
          </Card>

          <Card className="border-[#e5484d]/25 p-5">
            <h3 className="text-[13px] font-semibold text-[#e5484d]">Danger zone</h3>
            <p className="mt-1 text-[11.5px] text-white/45">Удаление проекта отвяжет все серверы и API-ключи. Действие необратимо.</p>
            <button className="mt-3 inline-flex items-center gap-1.5 rounded-lg border border-[#e5484d]/30 px-3 py-1.5 text-[11.5px] font-semibold text-[#e5484d] hover:bg-[#e5484d]/10">
              <Trash2 className="h-3.5 w-3.5" /> Удалить проект
            </button>
          </Card>
        </div>
      </div>
    </>
  );
}

/* ================================================================= *
 *  Servers
 * ================================================================= */
function ServersMod() {
  return (
    <>
      <div className="mb-5 flex items-end justify-between">
        <H sub="Florida V · production / development / test">Servers</H>
        <button className="inline-flex items-center gap-2 rounded-lg bg-white px-3.5 py-1.5 text-[12px] font-semibold text-black hover:bg-white/90 transition">
          <Plus className="h-4 w-4" /> Добавить сервер
        </button>
      </div>
      <div className="space-y-4">
        {APP_SERVERS.map((s) => (
          <Card key={s.id} className="p-4">
            <div className="flex flex-wrap items-center gap-3">
              <span className="font-mono text-[13px] font-semibold text-white">{s.name}</span>
              <span className="rounded-md border border-white/10 px-1.5 py-0.5 font-mono text-[9px] uppercase text-white/45">{s.env}</span>
              <StatusPill status={s.status} />
              <span className="font-mono text-[11px] text-white/35">{s.ip}:{s.port} · {s.version}</span>
              <div className="ml-auto flex gap-2">
                {[[Play, 'Start'], [RotateCw, 'Restart'], [Power, 'Stop'], [Terminal, 'Console']].map(([Icon, label]: any) => (
                  <button key={label} className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 text-[11px] text-white/70 hover:text-white">
                    <Icon className="h-3.5 w-3.5" /> {label}
                  </button>
                ))}
              </div>
            </div>
            <div className="mt-4 grid gap-4 sm:grid-cols-4">
              {[
                ['Онлайн', `${s.online} / ${s.slots}`],
                ['Tick', `${s.tick} Hz`],
                ['Uptime', s.uptime],
                ['Версия', s.version],
              ].map(([l, v]) => (
                <div key={l} className="rounded-xl border border-white/[0.07] bg-white/[0.02] p-3">
                  <div className="font-mono text-[10px] uppercase tracking-wider text-white/40">{l}</div>
                  <div className="mt-1 text-[14px] font-semibold">{v}</div>
                </div>
              ))}
            </div>
            <div className="mt-3 grid gap-4 sm:grid-cols-2">
              <div>
                <div className="mb-1 flex justify-between text-[11px] text-white/40"><span>CPU</span><span>{s.cpu}%</span></div>
                <MiniBar v={s.cpu} color={PINK} />
              </div>
              <div>
                <div className="mb-1 flex justify-between text-[11px] text-white/40"><span>RAM</span><span>{s.ram}%</span></div>
                <MiniBar v={s.ram} color={PURPLE} />
              </div>
            </div>
          </Card>
        ))}
      </div>
    </>
  );
}

/* ================================================================= *
 *  Resources (Dynamic Resource Manager)
 * ================================================================= */
function ResourcesMod() {
  const [resources, setResources] = useState<AppResource[]>(APP_RESOURCES);
  const [filter, setFilter] = useState<'all' | 'script' | 'map' | 'vehicle' | 'ui'>('all');

  const filtered = useMemo(() => {
    if (filter === 'all') return resources;
    return resources.filter((r) => r.type === filter);
  }, [resources, filter]);

  const toggleStatus = (id: string, newStatus: 'running' | 'stopped') => {
    setResources((prev) =>
      prev.map((r) => (r.id === id ? { ...r, status: newStatus } : r))
    );
  };

  const totalMem = resources.filter((r) => r.status === 'running').reduce((acc, r) => acc + r.memoryMb, 0);

  return (
    <>
      <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
        <H sub="Динамический менеджер ресурсов (C# DynamicResourceManager) · горячий старт/стоп без перезагрузки сервера">
          Resources
        </H>
        <div className="flex items-center gap-2">
          <span className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 font-mono text-[11px] text-white/60">
            ОЗУ ресурсов: <b className="text-white">{totalMem} MB</b>
          </span>
          <button className="inline-flex items-center gap-1.5 rounded-lg bg-white px-3.5 py-1.5 text-[12px] font-semibold text-black hover:bg-white/90 transition">
            <Plus className="h-4 w-4" /> Добавить ресурс
          </button>
        </div>
      </div>

      {/* filter tabs */}
      <div className="mb-4 flex gap-1.5 overflow-x-auto">
        {(['all', 'script', 'map', 'vehicle', 'ui'] as const).map((t) => (
          <button
            key={t}
            onClick={() => setFilter(t)}
            className={`rounded-lg px-3 py-1.5 text-[12px] font-medium capitalize transition ${
              filter === t ? 'bg-white/[0.08] text-white' : 'border border-white/10 bg-white/[0.03] text-white/50 hover:text-white/80'
            }`}
          >
            {t === 'all' ? 'Все ресурсы' : t}
          </button>
        ))}
      </div>

      <Card className="overflow-hidden">
        <table className="w-full text-left text-[12.5px]">
          <thead className="border-b border-white/[0.08] bg-white/[0.02] text-[11px] font-medium uppercase tracking-wider text-white/40">
            <tr>
              <th className="px-4 py-3">Имя ресурса</th>
              <th className="px-4 py-3">Тип</th>
              <th className="px-4 py-3">Статус</th>
              <th className="px-4 py-3">Память</th>
              <th className="px-4 py-3">Зависимости</th>
              <th className="px-4 py-3 text-right">Действия</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-white/[0.05]">
            {filtered.map((r) => (
              <tr key={r.id} className="text-white/70 hover:bg-white/[0.02]">
                <td className="px-4 py-3 font-mono font-semibold text-white/90">
                  {r.name}
                  <span className="ml-2 font-mono text-[10px] text-white/35">v{r.version}</span>
                </td>
                <td className="px-4 py-3">
                  <span className="rounded-md border border-white/10 bg-white/[0.04] px-2 py-0.5 font-mono text-[10px] uppercase text-white/60">
                    {r.type}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <StatusPill status={r.status === 'running' ? 'online' : r.status === 'stopped' ? 'offline' : 'paused'} />
                </td>
                <td className="px-4 py-3 font-mono text-[11.5px] text-white/50">
                  {r.status === 'running' ? `${r.memoryMb} MB` : '0 MB'}
                </td>
                <td className="px-4 py-3">
                  {r.dependencies.length > 0 ? (
                    <div className="flex flex-wrap gap-1">
                      {r.dependencies.map((dep) => (
                        <span key={dep} className="rounded border border-purple-500/30 bg-purple-500/10 px-1.5 py-0.5 font-mono text-[9px] text-purple-300">
                          {dep}
                        </span>
                      ))}
                    </div>
                  ) : (
                    <span className="text-white/25">—</span>
                  )}
                </td>
                <td className="px-4 py-3 text-right">
                  <div className="inline-flex gap-1.5">
                    {r.status === 'running' ? (
                      <>
                        <button
                          onClick={() => toggleStatus(r.id, 'stopped')}
                          className="rounded-md border border-white/10 bg-white/[0.03] px-2 py-1 text-[11px] text-red-400 transition hover:border-red-500/40 hover:bg-red-500/10"
                        >
                          Стоп
                        </button>
                        <button
                          onClick={() => {
                            toggleStatus(r.id, 'stopped');
                            setTimeout(() => toggleStatus(r.id, 'running'), 500);
                          }}
                          className="rounded-md border border-white/10 bg-white/[0.03] px-2 py-1 text-[11px] text-white/70 transition hover:border-white/20 hover:text-white"
                        >
                          Рестарт
                        </button>
                      </>
                    ) : (
                      <button
                        onClick={() => toggleStatus(r.id, 'running')}
                        className="rounded-md border border-white/10 bg-white/[0.03] px-2.5 py-1 text-[11px] text-[#3fb984] transition hover:border-[#3fb984]/40 hover:bg-[#3fb984]/10"
                      >
                        Старт
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </>
  );
}

/* ================================================================= *
 *  Watchdog & Crashes (Freeze Supervisor)
 * ================================================================= */
function WatchdogMod() {
  const [selectedIncident, setSelectedIncident] = useState<AppCrashIncident | null>(null);

  return (
    <>
      <div className="mb-5">
        <H sub="Автономный надзор за здоровьем сервера (ServerCrashWatchdog) · детекция фризов >15с и rate-limited авторестарт">
          Watchdog &amp; Crashes
        </H>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <Card grad className="p-4">
          <div className="flex items-center gap-2 text-[12px] font-semibold text-white/80">
            <ShieldAlert className="h-4 w-4" style={{ color: PINK }} /> Статус супервизора
          </div>
          <div className="mt-2 text-xl font-bold text-[#3fb984]">Активен (15s порог)</div>
          <div className="mt-1 text-[11px] text-white/45">Лимит: макс. 5 авторестартов / час</div>
        </Card>

        <Card className="p-4">
          <div className="text-[12px] font-semibold text-white/80">Сбоев за 30 дней</div>
          <div className="mt-2 text-xl font-bold text-white">2 инцидента</div>
          <div className="mt-1 text-[11px] text-[#3fb984]">100% автовосстановление</div>
        </Card>

        <Card className="p-4">
          <div className="text-[12px] font-semibold text-white/80">SLA Доступности</div>
          <div className="mt-2 text-xl font-bold text-transparent bg-clip-text bg-gradient-to-r from-pink-400 to-purple-400">
            99.98% SLA
          </div>
          <div className="mt-1 text-[11px] text-white/45">Даунтайм: 41 секунда за месяц</div>
        </Card>
      </div>

      <Card className="mt-5 overflow-hidden">
        <div className="flex items-center justify-between border-b border-white/[0.08] px-4 py-3 text-[12.5px] text-white/60">
          <span>Журнал расследования аварий (portal_server_crashes)</span>
          <span className="font-mono text-[10px] text-white/30">{APP_CRASHES.length} записи</span>
        </div>

        <div className="divide-y divide-white/[0.05]">
          {APP_CRASHES.map((inc) => (
            <div key={inc.id} className="p-4 transition hover:bg-white/[0.01]">
              <div className="flex flex-wrap items-center gap-3">
                <span className="font-mono text-[12px] font-bold text-pink-400">{inc.id}</span>
                <span className="font-mono text-[12px] text-white/80">{inc.server}</span>
                <span className="rounded border border-red-500/30 bg-red-500/10 px-2 py-0.5 text-[10px] text-red-300">
                  {inc.type}
                </span>
                <span className="ml-auto font-mono text-[11px] text-white/35">{inc.time}</span>
              </div>

              <div className="mt-2 text-[12.5px] text-white/75">{inc.reason}</div>

              <div className="mt-3 flex items-center justify-between">
                <span className="inline-flex items-center gap-1.5 text-[11px] text-[#3fb984]">
                  <Check className="h-3 w-3" /> Авторестарт выполнен успешно
                </span>
                <button
                  onClick={() => setSelectedIncident(selectedIncident?.id === inc.id ? null : inc)}
                  className="rounded border border-white/10 bg-white/[0.03] px-2.5 py-1 text-[11px] text-white/70 hover:text-white"
                >
                  {selectedIncident?.id === inc.id ? 'Скрыть стек' : 'Стек вызова C#'}
                </button>
              </div>

              {selectedIncident?.id === inc.id && (
                <pre className="mt-3 overflow-x-auto rounded-xl border border-white/10 bg-black/40 p-3 font-mono text-[11px] text-pink-300/80">
                  {inc.stackPreview}
                </pre>
              )}
            </div>
          ))}
        </div>
      </Card>
    </>
  );
}

/* ================================================================= *
 *  Analytics
 * ================================================================= */
function AnalyticsMod() {
  const charts = [
    { t: 'Онлайн игроков', d: ANALYTICS_SERIES.onlineDay, c: PINK },
    { t: 'CPU нагрузка', d: ANALYTICS_SERIES.cpuDay, c: PURPLE },
    { t: 'RAM использование', d: ANALYTICS_SERIES.ramDay, c: '#6b7280' },
    { t: 'Сетевой трафик', d: ANALYTICS_SERIES.netDay, c: PINK },
  ];
  return (
    <>
      <H sub="Уровень проекта · последние 24 часа">Analytics</H>
      <div className="grid gap-4 sm:grid-cols-3">
        {[
          ['Пик онлайна', '2 310', 'вчера 21:00'],
          ['Средний онлайн', '1 460', '−4% к прошл. неделе'],
          ['Аптайм', '99.98%', '30 дней'],
        ].map(([l, v, s]) => (
          <Card key={l} className="p-4">
            <div className="text-[12px] text-white/50">{l}</div>
            <div className="mt-1.5 text-[1.6rem] font-bold tracking-tight">{v}</div>
            <div className="text-[11px]" style={{ color: PINK }}>{s}</div>
          </Card>
        ))}
      </div>
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        {charts.map((c) => (
          <Card key={c.t} className="p-4 text-white/70">
            <div className="mb-3 flex items-center justify-between text-[12px] text-white/50">
              <span>{c.t}</span>
              <span className="font-mono text-[10px] text-white/30">24ч</span>
            </div>
            <AreaChart data={c.d} color={c.c} height={150} />
            <div className="mt-1 flex justify-between font-mono text-[9px] text-white/25">
              {DAY_LABELS.filter(Boolean).map((l) => <span key={l}>{l}</span>)}
            </div>
          </Card>
        ))}
      </div>
      <div className="mt-4 grid gap-4 sm:grid-cols-3">
        {[
          ['CPU сейчас', 0.61, PINK],
          ['RAM сейчас', 0.68, PURPLE],
          ['Сеть', 0.44, '#6b7280'],
        ].map(([l, v, c]: any) => (
          <Card key={l} className="flex items-center gap-4 p-4">
            <Donut value={v} size={92} stroke={9} color={c}>
              <span className="text-[13px] font-bold">{Math.round(v * 100)}%</span>
            </Donut>
            <div>
              <div className="text-[13px] font-medium">{l}</div>
              <div className="text-[11px] text-white/40">среднее по 4 серверам</div>
            </div>
          </Card>
        ))}
      </div>
    </>
  );
}

/* ================================================================= *
 *  Console
 * ================================================================= */
function ConsoleMod() {
  const [srv, setSrv] = useState(APP_SERVERS[0].id);
  const lines = [
    '[12:41:03] [INFO ] EntityStreamer initialised — grid 512',
    '[12:41:03] [INFO ] NetworkWorker listening udp/7788',
    '[12:41:04] [INFO ] csharp-module loaded flovmp-gamemode 1.4.2',
    '[12:41:04] [ OK  ] FastDL manifest verified (2 184 files)',
    '[12:41:05] [INFO ] License verified · slots 1500',
    '[12:41:07] [INFO ] player connected id=41 "Mikhail_D"',
    '[12:40:58] [WARN ] tick spike 22ms on resource econ_core',
    '[12:41:12] [INFO ] player connected id=42 "Anna.K"',
    '[12:41:20] [INFO ] weather set to EXTRASUNNY by admin',
  ];
  return (
    <>
      <H sub="Живая консоль сервера · txAdmin-style">Console</H>
      <div className="mb-3 flex flex-wrap gap-2">
        {APP_SERVERS.map((s) => (
          <button
            key={s.id}
            onClick={() => setSrv(s.id)}
            className={`rounded-lg px-3 py-1.5 font-mono text-[11px] transition ${srv === s.id ? 'bg-white/[0.08] text-white font-medium' : 'border border-white/10 bg-white/[0.03] text-white/50'}`}
          >
            {s.name}
          </button>
        ))}
      </div>
      <Card className="overflow-hidden">
        <div className="flex items-center gap-2 border-b border-white/[0.08] px-4 py-2.5 font-mono text-[11px] text-white/45">
          <Terminal className="h-3.5 w-3.5" /> {APP_SERVERS.find((s) => s.id === srv)?.name} · live
          <span className="ml-auto flex items-center gap-1 text-[#3fb984]"><span className="h-1.5 w-1.5 rounded-full bg-[#3fb984]" /> connected</span>
        </div>
        <div className="max-h-[340px] space-y-1 overflow-y-auto bg-black/40 p-4 font-mono text-[11.5px] leading-relaxed">
          {lines.map((l, i) => (
            <div key={i} className={l.includes('[WARN') ? 'text-[#d8a13a]' : l.includes('[ OK') ? 'text-[#3fb984]' : 'text-white/60'}>
              {l}
            </div>
          ))}
        </div>
        <div className="flex items-center gap-2 border-t border-white/[0.08] px-3 py-2">
          <span className="font-mono text-[12px]" style={{ color: PINK }}>flovmp&gt;</span>
          <input
            placeholder="Команда (напр. status, players, restart)…"
            className="flex-1 bg-transparent font-mono text-[12px] text-white outline-none placeholder:text-white/25"
          />
          <button className="rounded-md bg-white px-3 py-1 text-[11px] font-semibold text-black hover:bg-white/90">Send</button>
        </div>
      </Card>
    </>
  );
}

/* ================================================================= *
 *  Logs
 * ================================================================= */
function LogsMod() {
  const [q, setQ] = useState('');
  const [lvl, setLvl] = useState('ALL');
  const rows = LOG_ROWS.filter(
    (r) => (lvl === 'ALL' || r.level === lvl) && (q === '' || (r.msg + r.source).toLowerCase().includes(q.toLowerCase()))
  );
  const color = (l: string) => (l === 'ERROR' ? '#e5484d' : l === 'WARN' ? '#d8a13a' : l === 'OK' ? '#3fb984' : '#8b93a1');
  return (
    <>
      <H sub="История событий проекта · поиск и фильтр">Logs</H>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <div className="flex flex-1 items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2">
          <Search className="h-3.5 w-3.5 text-white/35" />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Поиск по сообщению / источнику…"
            className="flex-1 bg-transparent text-[12.5px] text-white outline-none placeholder:text-white/25"
          />
        </div>
        {['ALL', 'INFO', 'OK', 'WARN', 'ERROR'].map((l) => (
          <button
            key={l}
            onClick={() => setLvl(l)}
            className={`rounded-lg px-2.5 py-2 font-mono text-[10px] font-bold transition ${lvl === l ? 'bg-white/[0.08] text-white' : 'border border-white/10 bg-white/[0.03] text-white/45'}`}
          >
            {l}
          </button>
        ))}
      </div>
      <Card className="overflow-hidden">
        <table className="w-full text-left font-mono text-[11.5px]">
          <tbody className="divide-y divide-white/[0.05]">
            {rows.map((r, i) => (
              <tr key={i} className="text-white/55">
                <td className="whitespace-nowrap px-4 py-2.5 text-white/35">{r.time}</td>
                <td className="px-3 py-2.5"><span style={{ color: color(r.level) }}>{r.level}</span></td>
                <td className="px-3 py-2.5 text-white/40">{r.source}</td>
                <td className="px-4 py-2.5 text-white/70">{r.msg}</td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr><td className="px-4 py-8 text-center text-white/30" colSpan={4}>Ничего не найдено</td></tr>
            )}
          </tbody>
        </table>
      </Card>
    </>
  );
}

/* ================================================================= *
 *  Launcher Builder
 * ================================================================= */
function LauncherMod({ project }: { project: AppProject }) {
  const [launcherName, setLauncherName] = useState(project.name);
  const [accent, setAccent] = useState('#ff3d8a');
  const [serverTarget, setServerTarget] = useState('florida-prod-01 (185.220.101.44:7788)');
  const [isBuilding, setIsBuilding] = useState(false);
  const [buildProgress, setBuildProgress] = useState<number | null>(null);

  const startBuild = () => {
    setIsBuilding(true);
    setBuildProgress(10);
    setTimeout(() => setBuildProgress(45), 600);
    setTimeout(() => setBuildProgress(85), 1400);
    setTimeout(() => {
      setBuildProgress(100);
      setTimeout(() => {
        setIsBuilding(false);
        setBuildProgress(null);
      }, 1000);
    }, 2200);
  };

  const PRESET_COLORS = ['#ff3d8a', '#a855f7', '#00e5ff', '#3fb984', '#f59e0b', '#ef4444'];

  return (
    <>
      <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
        <H sub="Конструктор брендированного лаунчера · генератор Setup.exe с CDN-манифестом">Launcher Builder</H>
        <button
          onClick={startBuild}
          disabled={isBuilding}
          className="inline-flex items-center gap-2 rounded-lg bg-white px-4 py-2 text-[12.5px] font-semibold text-black shadow transition hover:bg-white/90 disabled:opacity-50"
        >
          {isBuilding ? (
            <>
              <RotateCw className="h-4 w-4 animate-spin" />
              <span>Сборка инсталлера... {buildProgress}%</span>
            </>
          ) : (
            <>
              <Download className="h-4 w-4" />
              <span>Скомпилировать Setup.exe</span>
            </>
          )}
        </button>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Left: Customization Settings */}
        <div className="space-y-4">
          <Card className="p-5">
            <h3 className="text-[13.5px] font-semibold text-white">Брендинг лаунчера</h3>
            <p className="mt-1 text-[12px] text-white/45">
              Настройте внешний вид клиента, логотип и акцентный цвет для игроков вашего проекта.
            </p>

            <div className="mt-4 space-y-4">
              <div>
                <label className="mb-1 block font-mono text-[10.5px] uppercase tracking-wider text-white/40">
                  Название в заголовке лаунчера
                </label>
                <input
                  type="text"
                  value={launcherName}
                  onChange={(e) => setLauncherName(e.target.value)}
                  className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-3.5 py-2 text-[13px] text-white outline-none focus:border-pink-500/50"
                />
              </div>

              <div>
                <label className="mb-1.5 block font-mono text-[10.5px] uppercase tracking-wider text-white/40">
                  Акцентный цвет интерфейса
                </label>
                <div className="flex flex-wrap items-center gap-2.5">
                  {PRESET_COLORS.map((c) => (
                    <button
                      key={c}
                      type="button"
                      onClick={() => setAccent(c)}
                      className={`h-7 w-7 rounded-full border-2 transition-transform hover:scale-110 ${
                        accent === c ? 'border-white scale-110 shadow-lg' : 'border-transparent'
                      }`}
                      style={{ backgroundColor: c }}
                    />
                  ))}
                  <input
                    type="text"
                    value={accent}
                    onChange={(e) => setAccent(e.target.value)}
                    className="w-24 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1 text-center font-mono text-[12px] text-white outline-none focus:border-pink-500/50"
                  />
                </div>
              </div>

              <div>
                <label className="mb-1 block font-mono text-[10.5px] uppercase tracking-wider text-white/40">
                  Целевой мастер-сервер для подключения
                </label>
                <select
                  value={serverTarget}
                  onChange={(e) => setServerTarget(e.target.value)}
                  className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-[12.5px] text-white outline-none focus:border-pink-500/50"
                >
                  <option value="florida-prod-01 (185.220.101.44:7788)" className="bg-[#0b0c10]">
                    florida-prod-01 (185.220.101.44:7788) · Production
                  </option>
                  <option value="florida-dev (185.220.101.44:7790)" className="bg-[#0b0c10]">
                    florida-dev (185.220.101.44:7790) · Development
                  </option>
                </select>
              </div>

              <div className="grid grid-cols-2 gap-3 pt-2">
                <div className="rounded-xl border border-dashed border-white/15 bg-white/[0.02] p-3 text-center">
                  <div className="mx-auto grid h-8 w-8 place-items-center rounded-lg bg-white/[0.05] text-white/60">
                    <Paintbrush className="h-4 w-4" />
                  </div>
                  <div className="mt-2 text-[11.5px] font-medium text-white">Логотип (PNG)</div>
                  <div className="text-[10px] text-white/35">Рекомендовано 512×512</div>
                  <button className="mt-2 rounded-md border border-white/10 bg-white/[0.04] px-2 py-1 text-[10.5px] text-white/60 hover:text-white">
                    Загрузить
                  </button>
                </div>

                <div className="rounded-xl border border-dashed border-white/15 bg-white/[0.02] p-3 text-center">
                  <div className="mx-auto grid h-8 w-8 place-items-center rounded-lg bg-white/[0.05] text-white/60">
                    <Sparkles className="h-4 w-4" />
                  </div>
                  <div className="mt-2 text-[11.5px] font-medium text-white">Баннер фона (WebP)</div>
                  <div className="text-[10px] text-white/35">1920×1080 без сжатия</div>
                  <button className="mt-2 rounded-md border border-white/10 bg-white/[0.04] px-2 py-1 text-[10.5px] text-white/60 hover:text-white">
                    Загрузить
                  </button>
                </div>
              </div>
            </div>
          </Card>

          <Card className="p-5">
            <h3 className="text-[13px] font-semibold text-white">CDN и целостность клиента</h3>
            <div className="mt-3 space-y-2.5 text-[12px]">
              <div className="flex items-center justify-between">
                <span className="text-white/60">FastDL CDN Manifest</span>
                <span className="font-mono text-emerald-400">manifest.v1.json (2 184 файла)</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-white/60">Проверка SHA-256 хэшей</span>
                <span className="font-mono text-white/80">Включена перед стартом</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-white/60">Авто-подмена Alt:V Runtimes</span>
                <span className="font-mono text-pink-400">FloV:MP Native Bridge</span>
              </div>
            </div>
          </Card>
        </div>

        {/* Right: Live Interactive Launcher Preview */}
        <div>
          <div className="mb-2 flex items-center justify-between">
            <span className="text-[12px] font-medium text-white/50">Превью окна лаунчера</span>
            <span className="font-mono text-[10px] text-white/35">1280 × 720 (Масштаб 65%)</span>
          </div>

          <div className="overflow-hidden rounded-2xl border border-white/15 bg-[#0a0a0f] shadow-2xl">
            {/* Window titlebar */}
            <div className="flex items-center justify-between border-b border-white/[0.08] bg-black/40 px-4 py-2.5 text-[11px] text-white/60">
              <div className="flex items-center gap-2">
                <div className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: accent }} />
                <span className="font-semibold text-white">{launcherName} Launcher</span>
              </div>
              <div className="flex gap-2">
                <span className="h-2 w-2 rounded-full bg-white/20" />
                <span className="h-2 w-2 rounded-full bg-white/20" />
                <span className="h-2 w-2 rounded-full bg-white/20" />
              </div>
            </div>

            {/* Launcher Body Mock */}
            <div className="relative p-6">
              {/* Background Glow */}
              <div
                className="pointer-events-none absolute -right-10 -top-10 h-64 w-64 rounded-full opacity-20 blur-3xl"
                style={{ backgroundColor: accent }}
              />

              <div className="relative z-10 flex flex-col justify-between" style={{ minHeight: '340px' }}>
                <div>
                  <div
                    className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-[10.5px] font-semibold text-white"
                    style={{ backgroundColor: `${accent}33`, border: `1px solid ${accent}66` }}
                  >
                    ● СЕРВЕР ОНЛАЙН · 1 180 / 1 500 ИГРОКОВ
                  </div>
                  <h2 className="mt-3 text-2xl font-black tracking-tight text-white">{launcherName}</h2>
                  <p className="mt-1 max-w-sm text-[12px] text-white/60">
                    Добро пожаловать в обновлённый мультиплеер на движке FloV:MP. Стабильные 60 FPS, моментальный FastDL
                    и надёжная защита.
                  </p>
                </div>

                <div className="mt-6 rounded-xl border border-white/10 bg-black/50 p-4 backdrop-blur-md">
                  <div className="flex items-center justify-between text-[12px]">
                    <span className="text-white/80">Проверка файлов игры...</span>
                    <span className="font-mono text-[11px] text-white/50">100% (Готов к игре)</span>
                  </div>
                  <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-white/10">
                    <div className="h-full w-full rounded-full transition-all" style={{ backgroundColor: accent }} />
                  </div>

                  <div className="mt-4 flex items-center justify-between">
                    <div>
                      <div className="text-[11px] text-white/40">Сервер</div>
                      <div className="font-mono text-[12px] font-medium text-white">{serverTarget.split(' ')[0]}</div>
                    </div>
                    <button
                      className="rounded-xl px-6 py-2.5 font-mono text-[13px] font-bold text-white shadow-xl transition hover:brightness-110"
                      style={{ backgroundColor: accent }}
                    >
                      ИГРАТЬ
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

/* ================================================================= *
 *  Billing & Invoices
 * ================================================================= */
function BillingMod() {
  const [period, setPeriod] = useState<'month' | 'halfYear' | 'year'>('month');

  const discountMultiplier = period === 'year' ? 0.7 : period === 'halfYear' ? 0.85 : 1.0;

  const PLANS = [
    {
      name: 'Starter',
      slots: '128 слотов',
      price: Math.round(4900 * discountMultiplier),
      desc: 'Идеально для закрытых тестов и небольших RP серверов',
      features: ['До 128 онлайн игроков', 'Быстрый FastDL CDN 100 Мбит/с', 'Авто-рестарт при зависаниях', 'Базовая аналитика'],
    },
    {
      name: 'Pro',
      popular: true,
      slots: '500 слотов',
      price: Math.round(11900 * discountMultiplier),
      desc: 'Для активно растущих проектов со средним онлайном',
      features: ['До 500 онлайн игроков', 'Выделенный FastDL CDN 1 Гбит/с', 'Конструктор фирменного лаунчера', 'FloV:ID Hardware защита', 'Приоритетный саппорт'],
    },
    {
      name: 'Enterprise',
      slots: '1 500+ слотов',
      price: Math.round(24900 * discountMultiplier),
      desc: 'Крупные RP-проекты с высоким онлайном (1500+ игроков)',
      features: ['Неограниченно слотов', 'Multi-Node кластер и репликация', 'Персональный менеджер 24/7', 'Выделенный AI-ассистент диагностики', 'SLA 99.98% гарантия'],
    },
  ];

  return (
    <>
      <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
        <H sub="Управление тарифными планами, подписками и выгрузка счетов">Billing &amp; Invoices</H>
        
        {/* Period Selector */}
        <div className="flex items-center rounded-xl border border-white/10 bg-white/[0.03] p-1">
          {[
            ['month', '1 месяц'],
            ['halfYear', '6 месяцев (-15%)'],
            ['year', '1 год (-30%)'],
          ].map(([p, label]) => (
            <button
              key={p}
              onClick={() => setPeriod(p as any)}
              className={`rounded-lg px-3 py-1.5 text-[11.5px] font-medium transition ${
                period === p ? 'hyapp-grad text-white shadow' : 'text-white/50 hover:text-white'
              }`}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {/* Plan Cards */}
      <div className="grid gap-4 sm:grid-cols-3">
        {PLANS.map((plan) => (
          <Card
            key={plan.name}
            className={`flex flex-col justify-between p-5 ${
              plan.popular ? 'border-pink-500/50 shadow-[0_0_24px_rgba(255,20,147,0.15)]' : ''
            }`}
          >
            <div>
              <div className="flex items-center justify-between">
                <span className="text-[14px] font-bold text-white">{plan.name}</span>
                {plan.popular && (
                  <span className="rounded-full bg-pink-500/20 px-2 py-0.5 text-[9.5px] font-bold text-pink-400">
                    ПОПУЛЯРНЫЙ
                  </span>
                )}
              </div>
              <div className="mt-1 text-[11.5px] text-white/40">{plan.desc}</div>

              <div className="mt-4 flex items-baseline gap-1">
                <span className="text-2xl font-black text-white">{plan.price.toLocaleString('ru-RU')} ₽</span>
                <span className="text-[11px] text-white/40">/ месяц</span>
              </div>
              <div className="mt-1 font-mono text-[11px] text-pink-400">{plan.slots}</div>

              <div className="mt-4 space-y-2 border-t border-white/[0.07] pt-4">
                {plan.features.map((f) => (
                  <div key={f} className="flex items-center gap-2 text-[11.5px] text-white/70">
                    <Check className="h-3.5 w-3.5 flex-none text-pink-400" />
                    <span>{f}</span>
                  </div>
                ))}
              </div>
            </div>

            <button
              className={`mt-6 w-full rounded-lg py-2.5 text-[12px] font-semibold transition ${
                plan.popular
                  ? 'bg-[#ff1493] text-black shadow hover:brightness-110'
                  : 'border border-white/10 bg-white/[0.04] text-white hover:bg-white/[0.08]'
              }`}
            >
              {plan.name === 'Enterprise' ? 'Текущий план' : 'Сменить тариф'}
            </button>
          </Card>
        ))}
      </div>

      {/* Invoices History */}
      <div className="mt-8">
        <div className="mb-3 flex items-center justify-between">
          <div>
            <h3 className="text-[13.5px] font-semibold text-white">История платежей и счетов</h3>
            <p className="text-[11.5px] text-white/40">Все счета формируются автоматически в MariaDB portal_invoices</p>
          </div>
          <button className="inline-flex items-center gap-1 text-[12px] text-white/50 hover:text-white">
            <Download className="h-3.5 w-3.5" /> Скачать все (ZIP)
          </button>
        </div>

        <Card className="overflow-hidden">
          <table className="w-full text-left text-[12px]">
            <thead>
              <tr className="border-b border-white/[0.07] font-mono text-[10px] uppercase tracking-wider text-white/35">
                <th className="px-4 py-3">Номер счёта</th>
                <th className="px-4 py-3">Проект</th>
                <th className="px-4 py-3">Тариф / Слоты</th>
                <th className="px-4 py-3">Сумма</th>
                <th className="px-4 py-3">Дата</th>
                <th className="px-4 py-3">Статус</th>
                <th className="px-4 py-3 text-right">Чек</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/[0.05]">
              {APP_INVOICES.map((inv) => (
                <tr key={inv.id} className="text-white/70 hover:bg-white/[0.02]">
                  <td className="px-4 py-3 font-mono font-medium text-white">{inv.id}</td>
                  <td className="px-4 py-3">{inv.project}</td>
                  <td className="px-4 py-3">
                    <span className="font-semibold text-white">{inv.plan}</span>
                    <span className="ml-1 text-[10.5px] text-white/40">({inv.slots} сл.)</span>
                  </td>
                  <td className="px-4 py-3 font-mono font-medium text-white">
                    {inv.amount.toLocaleString('ru-RU')} ₽
                  </td>
                  <td className="px-4 py-3 text-white/45">{inv.date}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10.5px] font-medium ${
                        inv.status === 'paid'
                          ? 'border border-emerald-500/30 bg-emerald-500/10 text-emerald-400'
                          : 'border border-amber-500/30 bg-amber-500/10 text-amber-400'
                      }`}
                    >
                      <Circle className="h-1.5 w-1.5 fill-current" />
                      {inv.status === 'paid' ? 'Оплачен' : 'Ожидает'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button className="inline-flex items-center gap-1 rounded-md border border-white/10 px-2 py-1 text-[11px] text-white/50 hover:text-white">
                      <Download className="h-3 w-3" /> PDF
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      </div>
    </>
  );
}

/* ================================================================= *
 *  API
 * ================================================================= */
function ApiMod() {
  const [shown, setShown] = useState<string | null>(null);
  return (
    <>
      <H sub="Ключи, вебхуки и доступ к данным проекта">API</H>
      <Card className="mb-4 p-4">
        <div className="flex items-center gap-2 text-[12px] text-white/50">
          <Globe className="h-3.5 w-3.5" /> Base URL
        </div>
        <div className="mt-2 flex items-center gap-2">
          <code className="flex-1 rounded-lg border border-white/10 bg-black/30 px-3 py-2 font-mono text-[12px] text-white/80">https://api.flovmp.dev/v1/projects/florida-v</code>
          <CopyBtn text="https://api.flovmp.dev/v1/projects/florida-v" />
        </div>
      </Card>

      <div className="mb-4 flex items-center justify-between">
        <h3 className="text-[13px] font-semibold">API-ключи</h3>
        <button className="inline-flex items-center gap-1.5 rounded-lg bg-white px-3 py-1.5 text-[11.5px] font-semibold text-black hover:bg-white/90 transition">
          <Plus className="h-3.5 w-3.5" /> Создать ключ
        </button>
      </div>
      <Card className="mb-6 overflow-hidden">
        <table className="w-full text-left text-[12px]">
          <thead>
            <tr className="border-b border-white/[0.07] font-mono text-[10px] uppercase tracking-wider text-white/35">
              <th className="px-4 py-2.5 font-semibold">Имя</th>
              <th className="px-4 py-2.5 font-semibold">Токен</th>
              <th className="px-4 py-2.5 font-semibold">Scope</th>
              <th className="px-4 py-2.5 font-semibold">Активность</th>
              <th className="px-4 py-2.5" />
            </tr>
          </thead>
          <tbody className="divide-y divide-white/[0.05]">
            {API_KEYS.map((k) => (
              <tr key={k.id} className="text-white/60">
                <td className="px-4 py-3 font-medium text-white/85">{k.name}</td>
                <td className="px-4 py-3 font-mono text-[11px]">
                  {shown === k.id ? k.token : `${k.token.slice(0, 12)}••••••••`}
                  <button onClick={() => setShown(shown === k.id ? null : k.id)} className="ml-2 text-white/35 hover:text-white">
                    {shown === k.id ? 'скрыть' : 'показать'}
                  </button>
                </td>
                <td className="px-4 py-3 font-mono text-[11px]">{k.scope}</td>
                <td className="px-4 py-3 text-white/40">{k.lastUsed}</td>
                <td className="px-4 py-3 text-right">
                  <button className="text-[11px] text-white/45 hover:text-white">Ротировать</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      <div className="mb-4 flex items-center justify-between">
        <h3 className="text-[13px] font-semibold">Вебхуки</h3>
        <button className="text-[11.5px] text-white/50 hover:text-white">+ Добавить</button>
      </div>
      <div className="space-y-3">
        {WEBHOOKS.map((w) => (
          <Card key={w.id} className="flex flex-wrap items-center gap-3 p-4">
            <Link2 className="h-4 w-4 text-white/35" />
            <code className="font-mono text-[11.5px] text-white/70">{w.url}</code>
            <div className="flex gap-1.5">
              {w.events.map((e) => (
                <span key={e} className="rounded border border-white/10 px-1.5 py-0.5 font-mono text-[9px] text-white/50">{e}</span>
              ))}
            </div>
            <span className="ml-auto"><StatusPill status={w.status} /></span>
          </Card>
        ))}
      </div>
    </>
  );
}

/* ================================================================= *
 *  SDK
 * ================================================================= */
function SdkMod() {
  const [tab, setTab] = useState(SNIPPETS[0].id);
  const snip = SNIPPETS.find((s) => s.id === tab)!;
  return (
    <>
      <H sub="Клиент, ядро, SDK и примеры кода">SDK & Downloads</H>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {SDK_DOWNLOADS.map((d) => (
          <Card key={d.file} className="p-4">
            <div className="flex items-start justify-between">
              <div>
                <div className="text-[13px] font-semibold">{d.name}</div>
                <div className="mt-0.5 font-mono text-[10.5px] text-white/40">{d.file}</div>
              </div>
              <span className="rounded-md border border-white/10 px-1.5 py-0.5 font-mono text-[9px] text-white/45">{d.ver}</span>
            </div>
            <div className="mt-3 flex items-center justify-between">
              <span className="font-mono text-[11px] text-white/35">{d.size}</span>
              <button className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 text-[11px] text-white/70 hover:text-white">
                <Download className="h-3.5 w-3.5" /> Скачать
              </button>
            </div>
          </Card>
        ))}
      </div>

      <div className="mt-6">
        <h3 className="mb-3 text-[13px] font-semibold">Примеры кода</h3>
        <Card className="overflow-hidden">
          <div className="flex items-center gap-1 border-b border-white/[0.08] p-2">
            {SNIPPETS.map((s) => (
              <button
                key={s.id}
                onClick={() => setTab(s.id)}
                className={`rounded-md px-3 py-1.5 font-mono text-[11px] transition ${tab === s.id ? 'bg-white/10 text-white' : 'text-white/45 hover:text-white/75'}`}
              >
                {s.label}
              </button>
            ))}
            <span className="ml-auto pr-1"><CopyBtn text={snip.code} /></span>
          </div>
          <pre className="hyapp-scroll overflow-x-auto bg-black/40 p-4 font-mono text-[11.5px] leading-relaxed text-white/75">
            <code>{snip.code}</code>
          </pre>
        </Card>
      </div>
    </>
  );
}

/* ================================================================= *
 *  Integrations
 * ================================================================= */
function IntegrationsMod() {
  const [state, setState] = useState<Record<string, boolean>>(
    Object.fromEntries(INTEGRATIONS.map((i) => [i.name, i.connected]))
  );
  return (
    <>
      <H sub="Discord, Telegram, хостинг, вебхуки и экспорт метрик">Integrations</H>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {INTEGRATIONS.map((it) => (
          <Card key={it.name} className="p-4">
            <div className="flex items-center gap-3">
              <div className="grid h-9 w-9 place-items-center rounded-xl text-[13px] font-bold" style={{ background: `${it.accent}22`, color: it.accent, border: `1px solid ${it.accent}44` }}>
                {it.name[0]}
              </div>
              <div>
                <div className="text-[13px] font-semibold">{it.name}</div>
                <div className="text-[11px] text-white/40">{state[it.name] ? 'Подключено' : 'Не подключено'}</div>
              </div>
              <button
                onClick={() => setState((s) => ({ ...s, [it.name]: !s[it.name] }))}
                className={`ml-auto h-5 w-9 rounded-full p-0.5 transition ${state[it.name] ? 'hyapp-grad' : 'bg-white/10'}`}
              >
                <span className={`block h-4 w-4 rounded-full bg-white transition ${state[it.name] ? 'translate-x-4' : ''}`} />
              </button>
            </div>
            <p className="mt-3 text-[11.5px] leading-relaxed text-white/45">{it.desc}</p>
          </Card>
        ))}
      </div>
    </>
  );
}

/* ================================================================= *
 *  Documentation
 * ================================================================= */
function DocsMod() {
  const [active, setActive] = useState('Обзор платформы');
  return (
    <>
      <H sub="Документация внутри платформы · поиск, категории, гайды">Documentation</H>
      <div className="grid gap-4 lg:grid-cols-[240px_1fr]">
        <Card className="h-max p-3">
          <div className="flex items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 text-[12px] text-white/40">
            <Search className="h-3.5 w-3.5" /> Поиск в документации
          </div>
          <div className="mt-3 space-y-3">
            {DOCS_TREE.map((g) => (
              <div key={g.group}>
                <div className="px-2 py-1 font-mono text-[10px] uppercase tracking-wider text-white/30">{g.group}</div>
                {g.items.map((it) => (
                  <button
                    key={it}
                    onClick={() => setActive(it)}
                    className={`block w-full rounded-lg px-2.5 py-1.5 text-left text-[12px] transition ${active === it ? 'bg-white/[0.06] text-white' : 'text-white/50 hover:text-white/80'}`}
                  >
                    {it}
                  </button>
                ))}
              </div>
            ))}
          </div>
        </Card>
        <Card grad className="p-6">
          <div className="font-mono text-[10px] uppercase tracking-wider" style={{ color: PINK }}>Documentation</div>
          <h2 className="mt-1 text-xl font-bold tracking-tight">{active}</h2>
          <div className="mt-4 space-y-3 text-[13px] leading-relaxed text-white/60">
            <p>
              FloV:MP — платформа для запуска и управления GTA V проектами. Всё строится вокруг сущности
              <b className="text-white/85"> проект</b>: аккаунт → проекты → серверы.
            </p>
            <p>
              Одна Lifetime-лицензия закрепляется за проектом. Внутри проекта — сколько угодно серверов
              (production / development / test), общий SDK, API-ключи и вебхуки.
            </p>
            <div className="rounded-xl border border-white/10 bg-black/30 p-4 font-mono text-[11.5px] text-white/70">
              <div className="text-white/35"># быстрый старт</div>
              npm i -g @flovmp/cli<br />
              flovmp login<br />
              flovmp projects create &quot;My RP&quot;<br />
              flovmp servers add --env production
            </div>
            <p className="text-white/45">Дальше: «Привязка сервера» → «server.toml» → «Console &amp; Logs».</p>
          </div>
          <button className="mt-5 inline-flex items-center gap-1.5 text-[12px] font-semibold" style={{ color: PINK }}>
            Открыть полную статью <ArrowRight className="h-3.5 w-3.5" />
          </button>
        </Card>
      </div>
    </>
  );
}

/* ================================================================= *
 *  AI Assistant
 * ================================================================= */
function AiMod() {
  return (
    <>
      <H sub="Помощь по документации, SDK, API и диагностике · лимит 40 запросов / день">AI Assistant</H>
      <Card grad className="flex h-[560px] flex-col overflow-hidden">
        <div className="flex items-center gap-2 border-b border-white/[0.08] px-4 py-3 text-[12px] text-white/50">
          <Sparkles className="h-4 w-4" style={{ color: PINK }} />
          FloV Assistant
          <span className="ml-auto font-mono text-[10px] text-white/30">37 / 40 запросов сегодня</span>
        </div>
        <div className="hyapp-scroll flex-1 space-y-4 overflow-y-auto p-4">
          {AI_THREAD.map((m, i) => (
            <div key={i} className={`flex gap-3 ${m.role === 'user' ? 'justify-end' : ''}`}>
              {m.role === 'assistant' && (
                <div className="grid h-7 w-7 flex-none place-items-center rounded-lg text-white" style={{ background: `linear-gradient(135deg,${PINK},${PURPLE})` }}>
                  <Sparkles className="h-3.5 w-3.5" />
                </div>
              )}
              <div
                className={`max-w-[78%] whitespace-pre-wrap rounded-2xl px-3.5 py-2.5 text-[12.5px] leading-relaxed ${
                  m.role === 'user' ? 'bg-white/10 text-white' : 'border border-white/10 bg-white/[0.03] text-white/75'
                }`}
              >
                {m.text}
              </div>
            </div>
          ))}
        </div>
        <div className="border-t border-white/[0.08] p-3">
          <div className="mb-2 flex flex-wrap gap-1.5">
            {['Почему растёт RAM?', 'Пример вебхука на C#', 'Как поднять tickrate?'].map((s) => (
              <button key={s} className="rounded-full border border-white/10 bg-white/[0.03] px-2.5 py-1 text-[11px] text-white/55 hover:text-white">
                {s}
              </button>
            ))}
          </div>
          <div className="flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-3 py-2">
            <input placeholder="Спросить ассистента…" className="flex-1 bg-transparent text-[12.5px] text-white outline-none placeholder:text-white/25" />
            <button className="grid h-7 w-7 place-items-center rounded-lg bg-white text-black hover:bg-white/90 transition">
              <Send className="h-3.5 w-3.5" />
            </button>
          </div>
        </div>
      </Card>
    </>
  );
}

/* ================================================================= *
 *  Settings
 * ================================================================= */
function SettingsMod({ project }: { project: AppProject }) {
  return (
    <>
      <H sub="Аккаунт, команда и лицензия">Settings</H>
      <div className="grid gap-4 lg:grid-cols-2">
        <Card className="p-5">
          <h3 className="text-[13px] font-semibold">Аккаунт</h3>
          <div className="mt-3 grid gap-3">
            {[['Имя', 'Mikhail D.'], ['Email', 'mikhail@floridav.gg'], ['Telegram', '@mikhail_dev']].map(([l, v]) => (
              <label key={l} className="block">
                <span className="mb-1 block font-mono text-[10px] uppercase tracking-wider text-white/40">{l}</span>
                <input defaultValue={v} className="w-full rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 text-[13px] text-white outline-none focus:border-white/25" />
              </label>
            ))}
          </div>
          <button className="mt-4 rounded-lg bg-white px-4 py-2 text-[12px] font-semibold text-black hover:bg-white/90 transition">Сохранить</button>
        </Card>

        <div className="space-y-4">
          <Card className="p-5">
            <div className="flex items-center gap-2 text-[13px] font-semibold">
              <LifeBuoy className="h-4 w-4" style={{ color: PINK }} /> Лицензия
            </div>
            <div className="mt-3 flex items-end gap-2">
              <span className="text-[2rem] font-bold leading-none text-white">Lifetime</span>
              <span className="pb-1 text-[12px] text-white/40">· проект «{project.name}»</span>
            </div>
            <div className="mt-3 grid grid-cols-2 gap-2 text-[11.5px] text-white/55">
              {['Мультиплеер + ядро', 'Dashboard', 'SDK · API', 'Кастомный лаунчер', 'Все обновления', 'Приоритетная поддержка'].map((x) => (
                <div key={x} className="flex items-center gap-1.5"><Check className="h-3.5 w-3.5 text-pink-400" /> {x}</div>
              ))}
            </div>
          </Card>
          <Card className="p-5">
            <h3 className="text-[13px] font-semibold">Роли и доступ</h3>
            <div className="mt-3 space-y-2 text-[12px]">
              {[['Owner', 'полный доступ, биллинг, удаление'], ['Administrator', 'серверы, консоль, интеграции'], ['Developer', 'SDK, API-ключи, логи']].map(([r, d]) => (
                <div key={r} className="flex items-center gap-3 rounded-lg border border-white/[0.07] bg-white/[0.02] px-3 py-2">
                  <span className="rounded-md border border-white/10 px-1.5 py-0.5 font-mono text-[9px] text-white/60">{r}</span>
                  <span className="text-white/45">{d}</span>
                </div>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </>
  );
}
