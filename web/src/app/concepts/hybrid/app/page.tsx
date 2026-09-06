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
  Copy,
  Cpu,
  Database,
  Download,
  Gauge,
  Globe,
  KeyRound,
  LayoutDashboard,
  LifeBuoy,
  Link2,
  MemoryStick,
  Play,
  Plug,
  Plus,
  Power,
  RotateCw,
  Search,
  Send,
  Server,
  Settings,
  Sparkles,
  Terminal,
  Trash2,
  Users,
} from 'lucide-react';
import { AreaChart, Donut, LineChart, Reveal } from '../../_ui';
import {
  AI_THREAD,
  ALERTS,
  ANALYTICS_SERIES,
  API_KEYS,
  APP_PROJECTS,
  APP_SERVERS,
  DAY_LABELS,
  DOCS_TREE,
  INTEGRATIONS,
  LOG_ROWS,
  SDK_DOWNLOADS,
  SNIPPETS,
  WEBHOOKS,
  type AppProject,
} from '../../_appdata';

const PINK = '#ff1493';
const PURPLE = '#a855f7';

const NAV: { id: Module; label: string; icon: React.ElementType }[] = [
  { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'projects', label: 'Projects', icon: Boxes },
  { id: 'servers', label: 'Servers', icon: Server },
  { id: 'analytics', label: 'Analytics', icon: Activity },
  { id: 'console', label: 'Console', icon: Terminal },
  { id: 'logs', label: 'Logs', icon: Database },
  { id: 'api', label: 'API', icon: KeyRound },
  { id: 'sdk', label: 'SDK', icon: Download },
  { id: 'integrations', label: 'Integrations', icon: Plug },
  { id: 'docs', label: 'Documentation', icon: BookOpen },
  { id: 'ai', label: 'AI Assistant', icon: Sparkles },
  { id: 'settings', label: 'Settings', icon: Settings },
];

type Module =
  | 'dashboard'
  | 'projects'
  | 'servers'
  | 'analytics'
  | 'console'
  | 'logs'
  | 'api'
  | 'sdk'
  | 'integrations'
  | 'docs'
  | 'ai'
  | 'settings';

const STYLES = `
.hyapp-card{background:linear-gradient(180deg,rgba(255,255,255,.045),rgba(255,255,255,.015));border:1px solid rgba(255,255,255,.09);backdrop-filter:blur(18px) saturate(140%)}
.hyapp-grad{background:linear-gradient(115deg,${PINK},${PURPLE})}
.hyapp-gb{position:relative}
.hyapp-gb::before{content:'';position:absolute;inset:0;border-radius:inherit;padding:1px;background:linear-gradient(130deg,rgba(255,20,147,.6),rgba(168,85,247,.5),transparent 70%);-webkit-mask:linear-gradient(#000 0 0) content-box,linear-gradient(#000 0 0);-webkit-mask-composite:xor;mask-composite:exclude;pointer-events:none}
.hyapp-scroll::-webkit-scrollbar{width:8px;height:8px}
.hyapp-scroll::-webkit-scrollbar-thumb{background:rgba(255,255,255,.12);border-radius:8px}
`;

export default function HybridApp() {
  const [mod, setMod] = useState<Module>('dashboard');
  const [projectId, setProjectId] = useState(APP_PROJECTS[0].id);
  const project = useMemo(() => APP_PROJECTS.find((p) => p.id === projectId)!, [projectId]);

  return (
    <div className="flex min-h-screen bg-[#0c0c12] font-sans text-[#e9eaee] antialiased">
      <style dangerouslySetInnerHTML={{ __html: STYLES }} />

      {/* ambient */}
      <div className="pointer-events-none fixed inset-0 -z-10">
        <div className="absolute left-[-6%] top-[-8%] h-[420px] w-[420px] rounded-full opacity-20 blur-[130px]" style={{ background: PINK }} />
        <div className="absolute right-[-4%] top-[30%] h-[420px] w-[420px] rounded-full opacity-[0.16] blur-[140px]" style={{ background: PURPLE }} />
      </div>

      {/* ============ SIDEBAR ============ */}
      <aside className="sticky top-0 hidden h-screen w-60 flex-none flex-col border-r border-white/[0.08] bg-[#0b0b10]/80 p-3 backdrop-blur-xl lg:flex">
        <a href="/concepts/hybrid" className="flex items-center gap-2.5 px-2 py-2">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/branding/logo.jpg" alt="" className="h-7 w-7 rounded-lg" />
          <span className="text-[14px] font-semibold tracking-tight">FloV:MP</span>
          <span className="ml-auto rounded-md border border-white/10 px-1.5 py-0.5 font-mono text-[9px] text-white/40">app</span>
        </a>

        {/* project switcher */}
        <div className="mt-2">
          <div className="px-2 pb-1 font-mono text-[10px] uppercase tracking-wider text-white/30">Проект</div>
          <div className="hyapp-gb rounded-xl">
            <div className="hyapp-card rounded-xl p-1">
              {APP_PROJECTS.map((p) => (
                <button
                  key={p.id}
                  onClick={() => setProjectId(p.id)}
                  className={`flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-[12.5px] transition ${
                    p.id === projectId ? 'bg-white/[0.06] text-white' : 'text-white/50 hover:text-white/80'
                  }`}
                >
                  <span className="h-2 w-2 rounded-full" style={{ background: p.accent }} />
                  <span className="truncate">{p.name}</span>
                </button>
              ))}
            </div>
          </div>
        </div>

        <nav className="hyapp-scroll mt-3 flex-1 space-y-0.5 overflow-y-auto pr-1">
          {NAV.map((n) => (
            <button
              key={n.id}
              onClick={() => setMod(n.id)}
              className={`flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-[12.5px] transition ${
                mod === n.id ? 'text-white' : 'text-white/45 hover:text-white/80'
              }`}
              style={mod === n.id ? { background: `linear-gradient(120deg, ${PINK}22, ${PURPLE}22)`, border: '1px solid rgba(255,255,255,.1)' } : { border: '1px solid transparent' }}
            >
              <n.icon className="h-4 w-4" style={{ color: mod === n.id ? PINK : undefined }} />
              {n.label}
            </button>
          ))}
        </nav>

        <div className="mt-2 flex items-center gap-2 rounded-lg border border-white/[0.08] bg-white/[0.02] px-2.5 py-2">
          <div className="grid h-7 w-7 place-items-center rounded-full text-[11px] font-bold text-white" style={{ background: `linear-gradient(135deg,${PINK},${PURPLE})` }}>
            MD
          </div>
          <div className="min-w-0">
            <div className="truncate text-[12px] font-medium">Mikhail D.</div>
            <div className="truncate text-[10px] text-white/40">Owner · Lifetime</div>
          </div>
        </div>
      </aside>

      {/* ============ MAIN ============ */}
      <div className="flex min-w-0 flex-1 flex-col">
        {/* top bar */}
        <header className="sticky top-0 z-20 flex h-14 items-center gap-3 border-b border-white/[0.08] bg-[#0c0c12]/80 px-4 backdrop-blur-xl sm:px-6">
          <div className="flex items-center gap-2 text-[13px] text-white/50">
            <span className="text-white/80">{project.name}</span>
            <ChevronRight className="h-3.5 w-3.5" />
            <span className="capitalize">{NAV.find((n) => n.id === mod)?.label}</span>
          </div>
          <div className="ml-auto hidden items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 text-[12px] text-white/40 md:flex">
            <Search className="h-3.5 w-3.5" />
            Поиск…
            <span className="ml-6 rounded border border-white/10 px-1 font-mono text-[10px]">⌘K</span>
          </div>
          <button className="relative rounded-lg border border-white/10 bg-white/[0.03] p-2 text-white/60 hover:text-white">
            <Bell className="h-4 w-4" />
            <span className="absolute right-1.5 top-1.5 h-1.5 w-1.5 rounded-full" style={{ background: PINK }} />
          </button>
          <a
            href="/concepts/hybrid"
            className="hidden rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-[12px] font-medium text-white/70 hover:text-white sm:block"
          >
            ← Лендинг
          </a>
        </header>

        {/* mobile module switch */}
        <div className="hyapp-scroll flex gap-1 overflow-x-auto border-b border-white/[0.08] px-3 py-2 lg:hidden">
          {NAV.map((n) => (
            <button
              key={n.id}
              onClick={() => setMod(n.id)}
              className={`flex-none rounded-lg px-3 py-1.5 text-[11px] font-medium transition ${
                mod === n.id ? 'hyapp-grad text-white' : 'bg-white/[0.04] text-white/50'
              }`}
            >
              {n.label}
            </button>
          ))}
        </div>

        <main className="hyapp-scroll flex-1 overflow-y-auto p-4 sm:p-6">
          <Reveal key={mod + projectId}>
            {mod === 'dashboard' && <DashboardMod project={project} />}
            {mod === 'projects' && <ProjectsMod project={project} onSelect={setProjectId} />}
            {mod === 'servers' && <ServersMod />}
            {mod === 'analytics' && <AnalyticsMod />}
            {mod === 'console' && <ConsoleMod />}
            {mod === 'logs' && <LogsMod />}
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
    </div>
  );
}

/* ================================================================= *
 *  Shared bits
 * ================================================================= */
function Card({ children, className = '', grad = false }: { children: React.ReactNode; className?: string; grad?: boolean }) {
  return grad ? (
    <div className={`hyapp-gb rounded-2xl ${className}`}>
      <div className="hyapp-card h-full rounded-2xl">{children}</div>
    </div>
  ) : (
    <div className={`hyapp-card rounded-2xl ${className}`}>{children}</div>
  );
}

function H({ children, sub }: { children: React.ReactNode; sub?: string }) {
  return (
    <div className="mb-5">
      <h1 className="text-xl font-bold tracking-tight sm:text-2xl">{children}</h1>
      {sub && <p className="mt-1 text-[13px] text-white/45">{sub}</p>}
    </div>
  );
}

function StatusPill({ status }: { status: string }) {
  const map: Record<string, string> = { online: '#3fb984', deploying: '#d8a13a', offline: '#e5484d', active: '#3fb984', paused: '#d8a13a' };
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[11px] capitalize"
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
 *  Dashboard
 * ================================================================= */
function DashboardMod({ project }: { project: AppProject }) {
  const kpis = [
    { icon: Users, label: 'Игроков онлайн', value: project.online.toLocaleString('ru-RU'), sub: `пик ${project.peak.toLocaleString('ru-RU')}`, accent: PINK },
    { icon: Cpu, label: 'CPU (avg)', value: '54%', sub: `${project.servers} сервера`, accent: PURPLE },
    { icon: MemoryStick, label: 'RAM (avg)', value: '61%', sub: '128 / 210 GB', accent: PINK },
    { icon: Gauge, label: 'Uptime', value: '99.98%', sub: '90 дней', accent: PURPLE },
  ];
  return (
    <>
      <H sub={`Обзор проекта · ${project.servers} серверов · Lifetime license`}>Dashboard</H>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {kpis.map((k) => (
          <Card key={k.label} className="p-4">
            <div className="flex items-center justify-between">
              <span className="text-[12px] text-white/50">{k.label}</span>
              <k.icon className="h-4 w-4" style={{ color: k.accent }} />
            </div>
            <div className="mt-2 text-[1.7rem] font-bold tracking-tight">{k.value}</div>
            <div className="text-[11px] text-white/35">{k.sub}</div>
          </Card>
        ))}
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-[1.5fr_1fr]">
        <Card className="p-4 text-white/70">
          <div className="mb-3 flex items-center justify-between text-[12px] text-white/50">
            <span>Онлайн · CPU · RAM (24ч)</span>
            <div className="flex gap-3 font-mono text-[10px]">
              <span style={{ color: PINK }}>● online</span>
              <span style={{ color: PURPLE }}>● cpu</span>
              <span className="text-white/40">● ram</span>
            </div>
          </div>
          <LineChart
            series={[
              { data: ANALYTICS_SERIES.onlineDay, color: PINK },
              { data: ANALYTICS_SERIES.cpuDay, color: PURPLE },
              { data: ANALYTICS_SERIES.ramDay, color: '#6b7280' },
            ]}
            height={190}
          />
        </Card>
        <Card className="p-4">
          <div className="mb-3 text-[12px] text-white/50">Алерты</div>
          <div className="space-y-2.5">
            {ALERTS.map((a) => (
              <div key={a.title} className="rounded-xl border border-white/[0.07] bg-white/[0.02] p-3">
                <div className="flex items-center gap-2 text-[12.5px] font-medium">
                  <span
                    className="h-1.5 w-1.5 rounded-full"
                    style={{ background: a.level === 'error' ? '#e5484d' : a.level === 'warn' ? '#d8a13a' : '#3fb984' }}
                  />
                  {a.title}
                  <span className="ml-auto font-mono text-[10px] text-white/30">{a.time}</span>
                </div>
                <div className="mt-1 text-[11.5px] text-white/45">{a.note}</div>
              </div>
            ))}
          </div>
        </Card>
      </div>

      <Card className="mt-4 overflow-hidden">
        <div className="flex items-center justify-between border-b border-white/[0.07] px-4 py-2.5 text-[12px] text-white/50">
          <span>Серверы проекта</span>
          <span className="font-mono text-[10px] text-white/30">{APP_SERVERS.length}</span>
        </div>
        <table className="w-full text-left text-[12px]">
          <tbody className="divide-y divide-white/[0.05]">
            {APP_SERVERS.map((s) => (
              <tr key={s.id} className="text-white/60">
                <td className="px-4 py-3 font-mono text-white/85">{s.name}</td>
                <td className="px-4 py-3 capitalize text-white/35">{s.env}</td>
                <td className="w-28 px-4 py-3">
                  <div className="mb-1 flex justify-between text-[10px] text-white/40"><span>CPU</span><span>{s.cpu}%</span></div>
                  <MiniBar v={s.cpu} color={PINK} />
                </td>
                <td className="w-28 px-4 py-3">
                  <div className="mb-1 flex justify-between text-[10px] text-white/40"><span>RAM</span><span>{s.ram}%</span></div>
                  <MiniBar v={s.ram} color={PURPLE} />
                </td>
                <td className="px-4 py-3 tabular-nums">{s.online}/{s.slots}</td>
                <td className="px-4 py-3"><StatusPill status={s.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </>
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
            <button className="hyapp-grad rounded-lg px-4 py-2 text-[12px] font-semibold text-white">Сохранить</button>
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
        <button className="hyapp-grad inline-flex items-center gap-2 rounded-xl px-3.5 py-2 text-[12px] font-semibold text-white">
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
            className={`rounded-lg px-3 py-1.5 font-mono text-[11px] transition ${srv === s.id ? 'hyapp-grad text-white' : 'border border-white/10 bg-white/[0.03] text-white/50'}`}
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
        <div className="hyapp-scroll max-h-[340px] space-y-1 overflow-y-auto bg-black/40 p-4 font-mono text-[11.5px] leading-relaxed">
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
          <button className="hyapp-grad rounded-md px-3 py-1 text-[11px] font-semibold text-white">Send</button>
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
            className={`rounded-lg px-2.5 py-2 font-mono text-[10px] font-bold transition ${lvl === l ? 'hyapp-grad text-white' : 'border border-white/10 bg-white/[0.03] text-white/45'}`}
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
        <button className="hyapp-grad inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-[11.5px] font-semibold text-white">
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
            <button className="hyapp-grad grid h-7 w-7 place-items-center rounded-lg text-white">
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
          <button className="hyapp-grad mt-4 rounded-lg px-4 py-2 text-[12px] font-semibold text-white">Сохранить</button>
        </Card>

        <div className="space-y-4">
          <Card grad className="p-5">
            <div className="flex items-center gap-2 text-[13px] font-semibold">
              <LifeBuoy className="h-4 w-4" style={{ color: PINK }} /> Лицензия
            </div>
            <div className="mt-3 flex items-end gap-2">
              <span className="bg-gradient-to-r from-[#ff1493] to-[#a855f7] bg-clip-text text-[2rem] font-bold leading-none text-transparent">Lifetime</span>
              <span className="pb-1 text-[12px] text-white/40">· проект «{project.name}»</span>
            </div>
            <div className="mt-3 grid grid-cols-2 gap-2 text-[11.5px] text-white/55">
              {['Мультиплеер + ядро', 'Dashboard', 'SDK · API', 'Кастомный лаунчер', 'Все обновления', 'Приоритетная поддержка'].map((x) => (
                <div key={x} className="flex items-center gap-1.5"><Check className="h-3.5 w-3.5" style={{ color: PURPLE }} /> {x}</div>
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
