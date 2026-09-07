'use client';

import React, { useMemo, useState } from 'react';
import {
  Activity,
  AlertTriangle,
  BellRing,
  Boxes,
  Check,
  ChevronRight,
  Circle,
  Copy,
  Cpu,
  CreditCard,
  Download,
  Eye,
  EyeOff,
  KeyRound,
  LayoutDashboard,
  Link2,
  MemoryStick,
  Megaphone,
  Package,
  Play,
  Plus,
  Power,
  RefreshCw,
  RotateCw,
  Search,
  Send,
  Server,
  Settings,
  ShieldCheck,
  Sparkles,
  Terminal,
  Trash2,
  Users,
  Wifi,
} from 'lucide-react';
import { AreaChart, Donut, LineChart, Reveal } from '../../_ui';
import {
  AI_THREAD,
  ANALYTICS_SERIES,
  API_KEYS,
  APP_PROJECTS,
  APP_SERVERS,
  DAY_LABELS,
  INTEGRATIONS,
  LOG_ROWS,
  SDK_DOWNLOADS,
  SNIPPETS,
  WEBHOOKS,
  type AppProject,
} from '../../_appdata';

const ACCENT = '#ff3d8a';

/* ---------- extra mock data (inline) ---------- */
const RESOURCES = [
  { name: 'core-framework', type: 'script', status: 'running', deps: '—' },
  { name: 'econ_core', type: 'script', status: 'running', deps: 'core-framework' },
  { name: 'admin_menu', type: 'script', status: 'running', deps: 'core-framework' },
  { name: 'inventory', type: 'script', status: 'running', deps: 'core-framework, econ_core' },
  { name: 'map_moscow_v3', type: 'map', status: 'running', deps: '—' },
  { name: 'vehpack_ru', type: 'cars', status: 'stopped', deps: '—' },
  { name: 'hud_nui', type: 'nui', status: 'running', deps: 'core-framework' },
  { name: 'interiors_dim', type: 'map', status: 'running', deps: 'map_moscow_v3' },
] as const;

const CRASHES = [
  { time: '2026-09-06 09:12:44', srv: 'florida-prod-02', reason: 'CoreCLR SIGSEGV in econ_core.PayrollTick', restarted: true },
  { time: '2026-09-05 22:41:03', srv: 'florida-dev', reason: 'Watchdog: main thread hang 18.3s', restarted: true },
  { time: '2026-09-04 03:07:19', srv: 'florida-prod-01', reason: 'OOM — heap 7.8 GB / 8 GB', restarted: true },
];

const SESSIONS = [
  { agent: 'Chrome 141 · Windows 11', ip: '188.127.•.14', where: 'Москва, RU', current: true, last: 'сейчас' },
  { agent: 'FloV:MP Launcher', ip: '188.127.•.14', where: 'Москва, RU', current: false, last: '2 ч назад' },
  { agent: 'Safari · iPhone', ip: '31.173.•.88', where: 'Казань, RU', current: false, last: 'вчера' },
];

type Mod =
  | 'dashboard' | 'projects' | 'servers' | 'resources' | 'watchdog' | 'analytics'
  | 'console' | 'logs' | 'launcher' | 'billing' | 'api' | 'sdk' | 'ai' | 'settings';

const NAV: { id: Mod; label: string; icon: React.ElementType }[] = [
  { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'projects', label: 'Projects', icon: Boxes },
  { id: 'servers', label: 'Servers', icon: Server },
  { id: 'resources', label: 'Resources', icon: Package },
  { id: 'watchdog', label: 'Watchdog & Crashes', icon: AlertTriangle },
  { id: 'analytics', label: 'Analytics', icon: Activity },
  { id: 'console', label: 'Console', icon: Terminal },
  { id: 'logs', label: 'Logs', icon: Search },
  { id: 'launcher', label: 'Launcher Builder', icon: Download },
  { id: 'billing', label: 'Billing', icon: CreditCard },
  { id: 'api', label: 'API & Webhooks', icon: KeyRound },
  { id: 'sdk', label: 'SDK & FastDL', icon: Package },
  { id: 'ai', label: 'AI Assistant', icon: Sparkles },
  { id: 'settings', label: 'Settings', icon: Settings },
];

const CSS = `
.sa-card{border:1px solid rgba(255,255,255,.08);background:rgba(255,255,255,.018);border-radius:12px}
.sa-in{height:36px;border:1px solid rgba(255,255,255,.1);background:rgba(255,255,255,.02);border-radius:8px;color:#fff;font-size:13px;padding:0 12px;width:100%}
.sa-in:focus{outline:none;border-color:${ACCENT}88;box-shadow:0 0 0 3px ${ACCENT}22}
.sa-btn{height:36px;border-radius:8px;font-size:12.5px;font-weight:600;display:inline-flex;align-items:center;justify-content:center;gap:6px;padding:0 14px;white-space:nowrap}
.sa-btn-primary{background:${ACCENT};color:#08080a}
.sa-btn-ghost{border:1px solid rgba(255,255,255,.1);background:rgba(255,255,255,.03);color:#e9eaee}
.sa-btn-ghost:hover{background:rgba(255,255,255,.06)}
.sa-scroll::-webkit-scrollbar{width:8px;height:8px}
.sa-scroll::-webkit-scrollbar-thumb{background:rgba(255,255,255,.12);border-radius:8px}
`;

export default function SaaSApp() {
  const [mod, setMod] = useState<Mod>('dashboard');
  const [pid, setPid] = useState(APP_PROJECTS[0].id);
  const project = useMemo(() => APP_PROJECTS.find((p) => p.id === pid)!, [pid]);

  return (
    <div className="flex min-h-screen bg-[#08080a] text-[#e9eaee]">
      <style dangerouslySetInnerHTML={{ __html: CSS }} />

      {/* ---------- SIDEBAR ---------- */}
      <aside className="sticky top-0 hidden h-screen w-60 flex-none flex-col border-r border-white/[0.07] bg-[#0a0a0c] p-3 lg:flex">
        <a href="/concepts/saas" className="flex items-center gap-2.5 px-2 py-2">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/branding/logo.jpg" alt="" className="h-6 w-6 rounded-md" />
          <span className="text-[14px] font-semibold tracking-tight">FloV:MP</span>
          <span className="ml-auto rounded border border-white/10 px-1.5 py-0.5 font-mono text-[9px] text-white/40">app</span>
        </a>

        <div className="mt-2 px-1">
          <div className="px-1 pb-1 font-mono text-[10px] uppercase tracking-wider text-white/30">Проект</div>
          <div className="space-y-0.5 rounded-lg border border-white/[0.07] bg-white/[0.02] p-1">
            {APP_PROJECTS.map((p) => (
              <button
                key={p.id}
                onClick={() => setPid(p.id)}
                className={`flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-[12.5px] transition ${
                  p.id === pid ? 'bg-white/[0.06] text-white' : 'text-white/45 hover:text-white/80'
                }`}
              >
                <span className="h-1.5 w-1.5 rounded-full" style={{ background: p.id === pid ? ACCENT : 'rgba(255,255,255,.2)' }} />
                <span className="truncate">{p.name}</span>
              </button>
            ))}
          </div>
        </div>

        <nav className="sa-scroll mt-3 flex-1 space-y-0.5 overflow-y-auto pr-1">
          {NAV.map((n) => (
            <button
              key={n.id}
              onClick={() => setMod(n.id)}
              className={`flex w-full items-center gap-2.5 rounded-lg px-2.5 py-[7px] text-left text-[12.5px] transition ${
                mod === n.id ? 'bg-white/[0.06] text-white' : 'text-white/45 hover:text-white/80'
              }`}
            >
              <n.icon className="h-4 w-4" style={{ color: mod === n.id ? ACCENT : undefined }} />
              <span className="truncate">{n.label}</span>
            </button>
          ))}
        </nav>

        <div className="mt-2 flex items-center gap-2 rounded-lg border border-white/[0.07] bg-white/[0.02] px-2.5 py-2">
          <div className="grid h-7 w-7 place-items-center rounded-full text-[11px] font-bold text-[#08080a]" style={{ background: ACCENT }}>MD</div>
          <div className="min-w-0">
            <div className="truncate text-[12px] font-medium">Mikhail D.</div>
            <div className="truncate text-[10px] text-white/40">Owner · Lifetime</div>
          </div>
        </div>
      </aside>

      {/* ---------- MAIN ---------- */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-14 items-center gap-3 border-b border-white/[0.07] bg-[#08080a]/85 px-4 backdrop-blur-xl sm:px-6">
          <div className="flex items-center gap-2 text-[13px] text-white/45">
            <span className="text-white/80">{project.name}</span>
            <ChevronRight className="h-3.5 w-3.5" />
            <span>{NAV.find((n) => n.id === mod)?.label}</span>
          </div>
          <div className="ml-auto hidden items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 text-[12px] text-white/35 md:flex">
            <Search className="h-3.5 w-3.5" /> Поиск
            <kbd className="ml-4 rounded border border-white/10 px-1 text-[10px]">⌘K</kbd>
          </div>
          <button className="relative rounded-lg border border-white/10 bg-white/[0.03] p-2 text-white/55 hover:text-white">
            <BellRing className="h-4 w-4" />
            <span className="absolute right-1.5 top-1.5 h-1.5 w-1.5 rounded-full" style={{ background: ACCENT }} />
          </button>
        </header>

        {/* mobile module switch */}
        <div className="sa-scroll flex gap-1 overflow-x-auto border-b border-white/[0.07] px-3 py-2 lg:hidden">
          {NAV.map((n) => (
            <button
              key={n.id}
              onClick={() => setMod(n.id)}
              className={`flex-none rounded-lg px-3 py-1.5 text-[11px] font-medium transition ${mod === n.id ? 'text-[#08080a]' : 'bg-white/[0.04] text-white/50'}`}
              style={mod === n.id ? { background: ACCENT } : undefined}
            >
              {n.label}
            </button>
          ))}
        </div>

        <main className="sa-scroll flex-1 overflow-y-auto px-4 py-6 sm:px-6">
          <Reveal key={mod + pid}>
            {mod === 'dashboard' && <Dashboard project={project} />}
            {mod === 'projects' && <Projects project={project} onSelect={setPid} />}
            {mod === 'servers' && <Servers />}
            {mod === 'resources' && <Resources />}
            {mod === 'watchdog' && <Watchdog />}
            {mod === 'analytics' && <Analytics />}
            {mod === 'console' && <Console />}
            {mod === 'logs' && <Logs />}
            {mod === 'launcher' && <Launcher project={project} />}
            {mod === 'billing' && <Billing />}
            {mod === 'api' && <Api project={project} />}
            {mod === 'sdk' && <Sdk />}
            {mod === 'ai' && <Ai />}
            {mod === 'settings' && <SettingsMod />}
          </Reveal>
          <div className="h-12" />
        </main>
      </div>
    </div>
  );
}

/* ================= shared ================= */
function PageHead({ title, sub, action }: { title: string; sub?: string; action?: React.ReactNode }) {
  return (
    <div className="mb-6 flex items-end justify-between gap-4">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">{title}</h1>
        {sub && <p className="mt-1 text-[13px] text-white/45">{sub}</p>}
      </div>
      {action}
    </div>
  );
}
function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return <div className={`sa-card p-5 ${className}`}>{children}</div>;
}
function Label({ children }: { children: React.ReactNode }) {
  return <span className="mb-1.5 block font-mono text-[10px] uppercase tracking-wider text-white/40">{children}</span>;
}
function Stat({ status }: { status: string }) {
  const m: Record<string, string> = { online: '#3fb984', running: '#3fb984', active: '#3fb984', deploying: '#d8a13a', paused: '#d8a13a', stopped: '#8a8f99', offline: '#e5484d' };
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[11px] capitalize" style={{ borderColor: `${m[status] || '#888'}44`, color: m[status] || '#aaa' }}>
      <Circle className="h-1.5 w-1.5 fill-current" />{status}
    </span>
  );
}
function Bar({ v, c = ACCENT }: { v: number; c?: string }) {
  return <div className="h-1.5 w-full overflow-hidden rounded-full bg-white/[0.06]"><div className="h-full rounded-full" style={{ width: `${v}%`, background: c }} /></div>;
}
function Copyable({ text }: { text: string }) {
  const [c, setC] = useState(false);
  return (
    <button
      onClick={() => { navigator.clipboard?.writeText(text).catch(() => {}); setC(true); setTimeout(() => setC(false), 1400); }}
      className="inline-flex items-center gap-1.5 rounded-md border border-white/10 bg-white/[0.03] px-2 py-1 text-[11px] text-white/55 hover:text-white"
    >
      {c ? <Check className="h-3 w-3 text-[#3fb984]" /> : <Copy className="h-3 w-3" />}{c ? 'Скопировано' : 'Копировать'}
    </button>
  );
}
function Toggle({ on, onChange }: { on: boolean; onChange: () => void }) {
  return (
    <button onClick={onChange} className="h-5 w-9 flex-none rounded-full p-0.5 transition" style={{ background: on ? ACCENT : 'rgba(255,255,255,.14)' }}>
      <span className={`block h-4 w-4 rounded-full bg-white transition ${on ? 'translate-x-4' : ''}`} />
    </button>
  );
}

/* ================= 1. Dashboard ================= */
function Dashboard({ project }: { project: AppProject }) {
  const kpi = [
    { icon: Users, l: 'Игроков онлайн', v: project.online.toLocaleString('ru-RU'), s: `пик ${project.peak.toLocaleString('ru-RU')}` },
    { icon: Cpu, l: 'CPU (avg)', v: '54%', s: `${project.servers} сервера` },
    { icon: MemoryStick, l: 'RAM (avg)', v: '61%', s: '128 / 210 GB' },
    { icon: Wifi, l: 'VDS', v: 'Online', s: '188.127.229.224' },
  ];
  return (
    <>
      <PageHead
        title="Dashboard"
        sub={`Обзор проекта · ${project.servers} серверов · Lifetime`}
        action={<button className="sa-btn sa-btn-primary"><Megaphone className="h-4 w-4" /> Быстрое /o</button>}
      />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {kpi.map((k) => (
          <Card key={k.l}>
            <div className="flex items-center justify-between">
              <span className="text-[12px] text-white/45">{k.l}</span>
              <k.icon className="h-4 w-4 text-white/30" />
            </div>
            <div className="mt-2 text-[1.6rem] font-semibold tracking-tight">{k.v}</div>
            <div className="font-mono text-[10px] text-white/30">{k.s}</div>
          </Card>
        ))}
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-[1.5fr_1fr]">
        <Card>
          <div className="mb-3 flex items-center justify-between text-[12px] text-white/45">
            <span>Онлайн за 24 часа</span><span className="font-mono text-[10px] text-white/25">avg 1 460</span>
          </div>
          <AreaChart data={ANALYTICS_SERIES.onlineDay} color={ACCENT} height={190} />
          <div className="mt-1 flex justify-between font-mono text-[9px] text-white/25">
            {DAY_LABELS.filter(Boolean).map((l) => <span key={l}>{l}</span>)}
          </div>
        </Card>
        <Card>
          <div className="mb-3 text-[12px] text-white/45">Последняя активность</div>
          <ul className="space-y-2.5 text-[12px]">
            {[
              ['12:41', 'player connected · id=41 Mikhail_D'],
              ['12:38', 'deploy florida-prod-01 → v16.4.39 (41s)'],
              ['12:31', 'Discord webhook доставлен'],
              ['12:04', 'alert: CPU 82% florida-prod-01'],
              ['11:30', 'florida-test остановлен вручную'],
            ].map(([t, x]) => (
              <li key={t} className="flex gap-2.5">
                <span className="font-mono text-white/25">{t}</span><span className="text-white/60">{x}</span>
              </li>
            ))}
          </ul>
        </Card>
      </div>

      <div className="mt-4 overflow-hidden rounded-xl border border-white/[0.08]">
        <div className="flex items-center justify-between border-b border-white/[0.07] px-4 py-2.5 text-[12px] text-white/45">
          <span>Серверы проекта</span><span className="font-mono text-[10px] text-white/25">{APP_SERVERS.length}</span>
        </div>
        <table className="w-full text-left text-[12px]">
          <tbody className="divide-y divide-white/[0.05]">
            {APP_SERVERS.map((s) => (
              <tr key={s.id} className="text-white/55">
                <td className="px-4 py-3 font-mono text-white/85">{s.name}</td>
                <td className="px-4 py-3 capitalize text-white/35">{s.env}</td>
                <td className="w-32 px-4 py-3"><div className="mb-1 flex justify-between text-[10px] text-white/35"><span>CPU</span><span>{s.cpu}%</span></div><Bar v={s.cpu} /></td>
                <td className="w-32 px-4 py-3"><div className="mb-1 flex justify-between text-[10px] text-white/35"><span>RAM</span><span>{s.ram}%</span></div><Bar v={s.ram} c="#7c86f5" /></td>
                <td className="px-4 py-3 tabular-nums">{s.online}/{s.slots}</td>
                <td className="px-4 py-3"><Stat status={s.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

/* ================= 2. Projects ================= */
function Projects({ project, onSelect }: { project: AppProject; onSelect: (id: string) => void }) {
  const [floVId, setFloVId] = useState<'strict' | 'lenient' | 'disabled'>('strict');
  const [vpn, setVpn] = useState(false);
  const [limit, setLimit] = useState('2');
  return (
    <>
      <PageHead
        title="Projects"
        sub="Аккаунт → проекты → серверы. Одна Lifetime-лицензия на проект."
        action={<button className="sa-btn sa-btn-primary"><Plus className="h-4 w-4" /> Новый проект</button>}
      />
      <div className="grid gap-3 sm:grid-cols-3">
        {APP_PROJECTS.map((p) => (
          <button
            key={p.id}
            onClick={() => onSelect(p.id)}
            className={`sa-card p-4 text-left transition ${p.id === project.id ? 'ring-1 ring-white/20' : 'hover:bg-white/[0.03]'}`}
          >
            <div className="flex items-center justify-between">
              <span className="text-[14px] font-medium">{p.name}</span>
              <Stat status="online" />
            </div>
            <div className="mt-0.5 text-[11px] text-white/40">{p.tag}</div>
            <div className="mt-3 flex gap-4 font-mono text-[11px] text-white/40">
              <span>{p.online.toLocaleString('ru-RU')} онлайн</span><span>{p.servers} серв.</span>
            </div>
          </button>
        ))}
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-[1.4fr_1fr]">
        <Card>
          <h2 className="text-[14px] font-medium">Управление · {project.name}</h2>
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            {[['Название', project.name], ['Slug', project.slug], ['Website', project.website], ['Discord', project.discord]].map(([l, v]) => (
              <label key={l}><Label>{l}</Label><input defaultValue={v} className="sa-in" /></label>
            ))}
          </div>
          <label className="mt-4 block"><Label>Описание</Label>
            <textarea rows={2} defaultValue="Флагманский RP на карте Florida. Экономика, фракции, кастомный контент." className="sa-in resize-none" style={{ height: 'auto', padding: '8px 12px' }} />
          </label>
          <div className="mt-4 flex gap-2"><button className="sa-btn sa-btn-primary">Сохранить</button><button className="sa-btn sa-btn-ghost">Отмена</button></div>
        </Card>

        <div className="space-y-4">
          <Card>
            <h3 className="text-[13px] font-medium">Команда</h3>
            <div className="mt-3 space-y-2">
              {project.members.map((m) => (
                <div key={m.name} className="flex items-center gap-2.5">
                  <div className="grid h-7 w-7 place-items-center rounded-full text-[10px] font-bold text-[#08080a]" style={{ background: ACCENT }}>{m.initials}</div>
                  <span className="text-[12.5px]">{m.name}</span>
                  <span className="ml-auto rounded border border-white/10 px-1.5 py-0.5 font-mono text-[9px] text-white/50">{m.role}</span>
                </div>
              ))}
            </div>
          </Card>

          <Card>
            <h3 className="text-[13px] font-medium">Политика FloV:ID</h3>
            <div className="mt-3 flex gap-1.5">
              {(['strict', 'lenient', 'disabled'] as const).map((k) => (
                <button
                  key={k}
                  onClick={() => setFloVId(k)}
                  className={`rounded-lg px-2.5 py-1.5 text-[11px] font-medium capitalize transition ${floVId === k ? 'text-[#08080a]' : 'border border-white/10 bg-white/[0.03] text-white/50'}`}
                  style={floVId === k ? { background: ACCENT } : undefined}
                >{k}</button>
              ))}
            </div>
            <div className="mt-4 flex items-center justify-between text-[12.5px]">
              <span className="text-white/60">Allow VPN</span><Toggle on={vpn} onChange={() => setVpn((v) => !v)} />
            </div>
            <label className="mt-3 block"><Label>Лимит аккаунтов на одно железо</Label>
              <input value={limit} onChange={(e) => setLimit(e.target.value)} className="sa-in" />
            </label>
          </Card>

          <Card className="border-[#e5484d]/25">
            <h3 className="text-[13px] font-medium text-[#e5484d]">Danger zone</h3>
            <p className="mt-1 text-[11.5px] text-white/45">Удаление проекта отвяжет серверы и ключи. Необратимо.</p>
            <button className="mt-3 inline-flex items-center gap-1.5 rounded-lg border border-[#e5484d]/30 px-3 py-1.5 text-[11.5px] font-semibold text-[#e5484d] hover:bg-[#e5484d]/10">
              <Trash2 className="h-3.5 w-3.5" /> Удалить проект
            </button>
          </Card>
        </div>
      </div>
    </>
  );
}

/* ================= 3. Servers ================= */
function Servers() {
  return (
    <>
      <PageHead title="Servers" sub="Production · Development · Test" action={<button className="sa-btn sa-btn-primary"><Plus className="h-4 w-4" /> Добавить</button>} />
      <div className="space-y-4">
        {APP_SERVERS.map((s) => (
          <Card key={s.id}>
            <div className="flex flex-wrap items-center gap-3">
              <span className="font-mono text-[13px] font-medium text-white">{s.name}</span>
              <span className="rounded border border-white/10 px-1.5 py-0.5 font-mono text-[9px] uppercase text-white/45">{s.env}</span>
              <Stat status={s.status} />
              <span className="font-mono text-[11px] text-white/35">{s.ip}:{s.port} · {s.version}</span>
              <div className="ml-auto flex gap-2">
                {([[Play, 'Start'], [RotateCw, 'Restart'], [Power, 'Stop'], [Terminal, 'Console']] as const).map(([Icon, l]) => (
                  <button key={l} className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 text-[11px] text-white/65 hover:text-white">
                    <Icon className="h-3.5 w-3.5" /> {l}
                  </button>
                ))}
              </div>
            </div>
            <div className="mt-4 grid gap-4 sm:grid-cols-4">
              {[['Онлайн', `${s.online} / ${s.slots}`], ['Tick', `${s.tick} Hz`], ['Uptime', s.uptime], ['Версия', s.version]].map(([l, v]) => (
                <div key={l} className="rounded-lg border border-white/[0.07] bg-white/[0.02] p-3">
                  <div className="font-mono text-[10px] uppercase tracking-wider text-white/40">{l}</div>
                  <div className="mt-1 text-[13px] font-medium">{v}</div>
                </div>
              ))}
            </div>
            <div className="mt-3 grid gap-4 sm:grid-cols-2">
              <div><div className="mb-1 flex justify-between text-[11px] text-white/40"><span>CPU</span><span>{s.cpu}%</span></div><Bar v={s.cpu} /></div>
              <div><div className="mb-1 flex justify-between text-[11px] text-white/40"><span>RAM</span><span>{s.ram}%</span></div><Bar v={s.ram} c="#7c86f5" /></div>
            </div>
          </Card>
        ))}
      </div>
    </>
  );
}

/* ================= 4. Resources ================= */
function Resources() {
  const [state, setState] = useState<Record<string, string>>(Object.fromEntries(RESOURCES.map((r) => [r.name, r.status])));
  const set = (n: string, v: string) => setState((s) => ({ ...s, [n]: v }));
  return (
    <>
      <PageHead title="Resources" sub="Горячий менеджер: старт / стоп / рестарт без перезагрузки сервера" />
      <Card className="!p-0 overflow-hidden">
        <table className="w-full text-left text-[12px]">
          <thead>
            <tr className="border-b border-white/[0.07] font-mono text-[10px] uppercase tracking-wider text-white/35">
              <th className="px-4 py-2.5 font-semibold">Ресурс</th>
              <th className="px-4 py-2.5 font-semibold">Тип</th>
              <th className="px-4 py-2.5 font-semibold">Зависимости</th>
              <th className="px-4 py-2.5 font-semibold">Статус</th>
              <th className="px-4 py-2.5" />
            </tr>
          </thead>
          <tbody className="divide-y divide-white/[0.05]">
            {RESOURCES.map((r) => (
              <tr key={r.name} className="text-white/55">
                <td className="px-4 py-3 font-mono text-white/85">{r.name}</td>
                <td className="px-4 py-3 capitalize text-white/40">{r.type}</td>
                <td className="px-4 py-3 font-mono text-[11px] text-white/35">{r.deps}</td>
                <td className="px-4 py-3"><Stat status={state[r.name]} /></td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-1.5">
                    <button onClick={() => set(r.name, 'running')} className="rounded-md border border-white/10 bg-white/[0.03] px-2 py-1 text-[10px] hover:text-white">Старт</button>
                    <button onClick={() => set(r.name, 'stopped')} className="rounded-md border border-white/10 bg-white/[0.03] px-2 py-1 text-[10px] hover:text-white">Стоп</button>
                    <button onClick={() => set(r.name, 'running')} className="rounded-md border border-white/10 bg-white/[0.03] px-2 py-1 text-[10px] hover:text-white">Рестарт</button>
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

/* ================= 5. Watchdog & Crashes ================= */
function Watchdog() {
  return (
    <>
      <PageHead title="Watchdog & Crashes" sub="Супервизор зависаний > 15 с · авторестарт ≤ 5 / час" />
      <div className="grid gap-4 sm:grid-cols-3">
        {[['Порог зависания', '15.0 с'], ['Авторестартов за час', '2 / 5'], ['Аптайм супервизора', '30 дней']].map(([l, v]) => (
          <Card key={l}><div className="text-[12px] text-white/45">{l}</div><div className="mt-1.5 text-[1.5rem] font-semibold tracking-tight">{v}</div></Card>
        ))}
      </div>
      <div className="mt-4 overflow-hidden rounded-xl border border-white/[0.08]">
        <div className="border-b border-white/[0.07] px-4 py-2.5 text-[12px] text-white/45">Логи аварийных завершений</div>
        <table className="w-full text-left text-[12px]">
          <tbody className="divide-y divide-white/[0.05]">
            {CRASHES.map((c, i) => (
              <tr key={i} className="text-white/55">
                <td className="whitespace-nowrap px-4 py-3 font-mono text-white/35">{c.time}</td>
                <td className="px-4 py-3 font-mono text-white/70">{c.srv}</td>
                <td className="px-4 py-3">{c.reason}</td>
                <td className="px-4 py-3">{c.restarted ? <Stat status="online" /> : <Stat status="offline" />}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Card className="mt-4">
        <div className="mb-2 flex items-center gap-2 text-[12px] text-white/45"><Terminal className="h-3.5 w-3.5" /> Дамп стека CoreCLR · последний краш</div>
        <pre className="sa-scroll overflow-x-auto rounded-lg bg-black/40 p-4 font-mono text-[11px] leading-relaxed text-white/60">{`Unhandled exception. System.AccessViolationException
  at FloVMP.Gamemode.Econ.PayrollTick(Int32 batch)
  at FloVMP.Core.Scheduler.RunResourceTick(Resource r)
  at FloVMP.Core.Loop.Tick() in Loop.cs:line 214
  -> watchdog: SIGSEGV captured, core dumped /var/crash/econ_core_0912.dmp
  -> autorestart: florida-prod-02 back online in 6.1s`}</pre>
      </Card>
    </>
  );
}

/* ================= 6. Analytics ================= */
function Analytics() {
  const charts = [
    { t: 'Онлайн игроков', d: ANALYTICS_SERIES.onlineDay, c: ACCENT },
    { t: 'CPU нагрузка', d: ANALYTICS_SERIES.cpuDay, c: '#7c86f5' },
    { t: 'RAM использование', d: ANALYTICS_SERIES.ramDay, c: '#3fb984' },
    { t: 'Сетевой трафик', d: ANALYTICS_SERIES.netDay, c: ACCENT },
  ];
  return (
    <>
      <PageHead title="Analytics" sub="Уровень проекта · последние 24 часа" />
      <div className="grid gap-4 sm:grid-cols-4">
        {[['Средний тикрейт', '60 Hz'], ['Server FPS', '60.0'], ['Пик онлайна', '2 310'], ['SLA', '99.98%']].map(([l, v]) => (
          <Card key={l}><div className="text-[12px] text-white/45">{l}</div><div className="mt-1.5 text-[1.5rem] font-semibold tracking-tight">{v}</div></Card>
        ))}
      </div>
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        {charts.map((c) => (
          <Card key={c.t}>
            <div className="mb-3 flex items-center justify-between text-[12px] text-white/45"><span>{c.t}</span><span className="font-mono text-[10px] text-white/25">24ч</span></div>
            <AreaChart data={c.d} color={c.c} height={150} />
          </Card>
        ))}
      </div>
      <div className="mt-4 grid gap-4 sm:grid-cols-3">
        {([['CPU', 0.61, ACCENT], ['RAM', 0.68, '#7c86f5'], ['Network', 0.44, '#3fb984']] as const).map(([l, v, c]) => (
          <Card key={l} className="flex items-center gap-4">
            <Donut value={v} size={88} stroke={9} color={c}><span className="text-[13px] font-semibold">{Math.round(v * 100)}%</span></Donut>
            <div><div className="text-[13px] font-medium">{l}</div><div className="text-[11px] text-white/40">avg по 4 серверам</div></div>
          </Card>
        ))}
      </div>
    </>
  );
}

/* ================= 7. Console ================= */
function Console() {
  const [srv, setSrv] = useState(APP_SERVERS[0].id);
  const [log, setLog] = useState<string[]>([
    '[12:41:03] [INFO ] EntityStreamer initialised — grid 512',
    '[12:41:03] [INFO ] NetworkWorker listening udp/7788',
    '[12:41:04] [ OK  ] FastDL manifest verified (2 184 files)',
    '[12:41:05] [INFO ] License verified · slots 1500',
    '[12:41:07] [INFO ] player connected id=41 "Mikhail_D"',
    '[12:40:58] [WARN ] tick spike 22ms on resource econ_core',
  ]);
  const [cmd, setCmd] = useState('');
  const send = () => {
    if (!cmd.trim()) return;
    const now = new Date().toLocaleTimeString('ru-RU');
    setLog((l) => [...l, `[${now}] [RCON ] > ${cmd}`, `[${now}] [ OK  ] command accepted`]);
    setCmd('');
  };
  return (
    <>
      <PageHead title="Console" sub="Прямой SSE-стрим логов · инпут RCON-команд" />
      <div className="mb-3 flex flex-wrap gap-2">
        {APP_SERVERS.map((s) => (
          <button key={s.id} onClick={() => setSrv(s.id)} className={`rounded-lg px-3 py-1.5 font-mono text-[11px] transition ${srv === s.id ? 'text-[#08080a]' : 'border border-white/10 bg-white/[0.03] text-white/50'}`} style={srv === s.id ? { background: ACCENT } : undefined}>{s.name}</button>
        ))}
      </div>
      <div className="overflow-hidden rounded-xl border border-white/[0.08]">
        <div className="flex items-center gap-2 border-b border-white/[0.07] px-4 py-2.5 font-mono text-[11px] text-white/45">
          <Terminal className="h-3.5 w-3.5" /> {APP_SERVERS.find((s) => s.id === srv)?.name}
          <span className="ml-auto flex items-center gap-1 text-[#3fb984]"><span className="h-1.5 w-1.5 rounded-full bg-[#3fb984]" /> connected</span>
        </div>
        <div className="sa-scroll max-h-[340px] space-y-1 overflow-y-auto bg-black/40 p-4 font-mono text-[11.5px] leading-relaxed">
          {log.map((l, i) => (
            <div key={i} className={l.includes('[WARN') ? 'text-[#d8a13a]' : l.includes('[ OK') ? 'text-[#3fb984]' : l.includes('[RCON') ? 'text-[#ff3d8a]' : 'text-white/60'}>{l}</div>
          ))}
        </div>
        <div className="flex items-center gap-2 border-t border-white/[0.07] px-3 py-2">
          <span className="font-mono text-[12px]" style={{ color: ACCENT }}>flovmp&gt;</span>
          <input value={cmd} onChange={(e) => setCmd(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && send()} placeholder="status · players · restart · /o привет" className="flex-1 bg-transparent font-mono text-[12px] text-white outline-none placeholder:text-white/25" />
          <button onClick={send} className="sa-btn sa-btn-primary !h-8">Send</button>
        </div>
      </div>
    </>
  );
}

/* ================= 8. Logs ================= */
function Logs() {
  const [q, setQ] = useState('');
  const [lvl, setLvl] = useState('ALL');
  const extra = [...LOG_ROWS, { time: '2026-09-06 09:12:44', level: 'CRASH', source: 'watchdog', msg: 'CoreCLR SIGSEGV — core dumped, autorestart ok' }];
  const rows = extra.filter((r) => (lvl === 'ALL' || r.level === lvl) && (q === '' || (r.msg + r.source).toLowerCase().includes(q.toLowerCase())));
  const col = (l: string) => (l === 'ERROR' || l === 'CRASH' ? '#e5484d' : l === 'WARN' ? '#d8a13a' : l === 'OK' ? '#3fb984' : '#8a93a1');
  return (
    <>
      <PageHead title="Logs" sub="Системные логи · фильтр и поиск" />
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <div className="flex flex-1 items-center gap-2 rounded-lg border border-white/10 bg-white/[0.02] px-3" style={{ height: 36 }}>
          <Search className="h-3.5 w-3.5 text-white/35" />
          <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Поиск по сообщению / источнику" className="flex-1 bg-transparent text-[12.5px] text-white outline-none placeholder:text-white/25" />
        </div>
        {['ALL', 'INFO', 'OK', 'WARN', 'ERROR', 'CRASH'].map((l) => (
          <button key={l} onClick={() => setLvl(l)} className={`rounded-lg px-2.5 font-mono text-[10px] font-bold transition ${lvl === l ? 'text-[#08080a]' : 'border border-white/10 bg-white/[0.03] text-white/45'}`} style={{ height: 36, ...(lvl === l ? { background: ACCENT } : {}) }}>{l}</button>
        ))}
      </div>
      <Card className="!p-0 overflow-hidden">
        <table className="w-full text-left font-mono text-[11.5px]">
          <tbody className="divide-y divide-white/[0.05]">
            {rows.map((r, i) => (
              <tr key={i} className="text-white/55">
                <td className="whitespace-nowrap px-4 py-2.5 text-white/30">{r.time}</td>
                <td className="px-3 py-2.5" style={{ color: col(r.level) }}>{r.level}</td>
                <td className="px-3 py-2.5 text-white/35">{r.source}</td>
                <td className="px-4 py-2.5 text-white/70">{r.msg}</td>
              </tr>
            ))}
            {rows.length === 0 && <tr><td className="px-4 py-8 text-center text-white/30" colSpan={4}>Ничего не найдено</td></tr>}
          </tbody>
        </table>
      </Card>
    </>
  );
}

/* ================= 9. Launcher Builder ================= */
function Launcher({ project }: { project: AppProject }) {
  const PRESETS = ['#ff3d8a', '#00f0ff', '#f59e0b', '#10b981', '#8b5cf6'];
  const [name, setName] = useState(project.name);
  const [color, setColor] = useState('#ff3d8a');
  const [stage, setStage] = useState(0); // 0 idle · 1..3 building · 4 done
  const STEPS = ['Генерация конфигурации', 'Упаковка Electron + Native', 'Подписание EXE', 'Готово'];
  const build = () => {
    setStage(1);
    [2, 3, 4].forEach((n, i) => setTimeout(() => setStage(n), (i + 1) * 700));
  };
  return (
    <>
      <PageHead title="Launcher Builder" sub="Название, цвет, логотип → компиляция Setup.exe" />
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <label className="block"><Label>Название проекта</Label><input value={name} onChange={(e) => setName(e.target.value)} className="sa-in" /></label>
          <div className="mt-4"><Label>Фирменный цвет</Label>
            <div className="flex items-center gap-3">
              <input type="color" value={color} onChange={(e) => setColor(e.target.value)} className="h-9 w-12 rounded-lg border border-white/10 bg-transparent" />
              <input value={color} onChange={(e) => setColor(e.target.value)} className="sa-in font-mono" />
            </div>
            <div className="mt-2 flex gap-1.5">
              {PRESETS.map((p) => (
                <button key={p} onClick={() => setColor(p)} className={`h-6 w-6 rounded-md border ${color.toLowerCase() === p ? 'border-white/60' : 'border-white/15'}`} style={{ background: p }} />
              ))}
            </div>
          </div>
          <div className="mt-4"><Label>Логотип</Label>
            <div className="flex h-20 items-center justify-center rounded-lg border border-dashed border-white/15 bg-white/[0.02] text-[12px] text-white/35">Перетащите PNG / SVG</div>
          </div>
          <button onClick={build} disabled={stage > 0 && stage < 4} className="sa-btn sa-btn-primary mt-4 w-full disabled:opacity-50">
            <Download className="h-4 w-4" /> {stage > 0 && stage < 4 ? 'Компиляция…' : 'Скомпилировать Setup.exe'}
          </button>
        </Card>
        <Card>
          <div className="text-[13px] font-medium">Статус сборки</div>
          {stage === 0 ? (
            <div className="mt-4 rounded-lg border border-white/[0.07] bg-white/[0.02] p-6 text-center text-[12px] text-white/35">
              <Package className="mx-auto h-8 w-8 text-white/20" />
              <p className="mt-2">Заполните параметры и запустите сборку.</p>
            </div>
          ) : (
            <div className="mt-4 space-y-2">
              {STEPS.map((s, i) => {
                const st = stage > i + 1 || stage === 4 ? 'done' : stage === i + 1 ? 'run' : 'idle';
                return (
                  <div key={s} className={`flex items-center gap-2.5 rounded-lg border px-3 py-2 text-[12px] ${st === 'done' ? 'border-[#3fb984]/25 bg-[#3fb984]/[0.06] text-[#3fb984]' : st === 'run' ? 'border-white/15 bg-white/[0.04] text-white' : 'border-white/[0.06] text-white/35'}`}>
                    {st === 'done' ? <Check className="h-4 w-4" /> : st === 'run' ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Circle className="h-4 w-4" />}
                    {s}
                  </div>
                );
              })}
            </div>
          )}
          {stage === 4 && (
            <a href="#" className="sa-btn mt-4 w-full" style={{ background: '#3fb984', color: '#08080a' }}>
              <Download className="h-4 w-4" /> {name}-Setup.exe · 94 MB
            </a>
          )}
        </Card>
      </div>
    </>
  );
}

/* ================= 10. Billing ================= */
function Billing() {
  const [auto, setAuto] = useState(true);
  const invoices = [
    { id: 1042, date: '2026-03-14', sum: '24 000 ₽', item: 'Lifetime License · Florida V', status: 'paid' },
    { id: 1088, date: '2026-06-01', sum: '9 900 ₽', item: 'Доп. слоты +500 · florida-prod-01', status: 'paid' },
    { id: 1121, date: '2026-09-01', sum: '6 930 ₽', item: 'FastDL трафик · август', status: 'pending' },
  ];
  return (
    <>
      <PageHead title="Billing" sub="Счета, скидки и автопродление" />
      <div className="grid gap-4 sm:grid-cols-3">
        <Card><div className="text-[12px] text-white/45">Полгода</div><div className="mt-1.5 text-[1.5rem] font-semibold" style={{ color: ACCENT }}>−15%</div></Card>
        <Card><div className="text-[12px] text-white/45">Год</div><div className="mt-1.5 text-[1.5rem] font-semibold" style={{ color: ACCENT }}>−30%</div></Card>
        <Card className="flex items-center justify-between"><div><div className="text-[12px] text-white/45">Автопродление</div><div className="mt-1 text-[13px]">{auto ? 'Включено' : 'Выключено'}</div></div><Toggle on={auto} onChange={() => setAuto((v) => !v)} /></Card>
      </div>
      <Card className="mt-4 !p-0 overflow-hidden">
        <table className="w-full text-left text-[12px]">
          <thead><tr className="border-b border-white/[0.07] font-mono text-[10px] uppercase tracking-wider text-white/35">
            <th className="px-4 py-2.5 font-semibold">Счёт</th><th className="px-4 py-2.5 font-semibold">Дата</th><th className="px-4 py-2.5 font-semibold">Позиция</th><th className="px-4 py-2.5 font-semibold">Сумма</th><th className="px-4 py-2.5 font-semibold">Статус</th><th className="px-4 py-2.5" />
          </tr></thead>
          <tbody className="divide-y divide-white/[0.05]">
            {invoices.map((v) => (
              <tr key={v.id} className="text-white/55">
                <td className="px-4 py-3 font-mono text-white/80">#{v.id}</td>
                <td className="px-4 py-3 font-mono text-white/40">{v.date}</td>
                <td className="px-4 py-3">{v.item}</td>
                <td className="px-4 py-3 font-mono font-medium text-white">{v.sum}</td>
                <td className="px-4 py-3"><Stat status={v.status === 'paid' ? 'active' : 'paused'} /></td>
                <td className="px-4 py-3 text-right">{v.status === 'pending' ? <button className="sa-btn sa-btn-primary !h-8">Оплатить</button> : <span className="text-[11px] text-white/25">закрыт</span>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </>
  );
}

/* ================= 11. API & Webhooks ================= */
function Api({ project }: { project: AppProject }) {
  const [shown, setShown] = useState<string | null>(null);
  const donate = `https://api.flovmp.dev/api/v1/public/${project.slug}/donate`;
  return (
    <>
      <PageHead title="API & Webhooks" sub="Ключи доступа, вебхуки и эндпоинт донат-магазина" />
      <Card className="mb-4">
        <Label>Эндпоинт донат-магазина</Label>
        <div className="flex items-center gap-2">
          <code className="flex-1 truncate rounded-lg border border-white/10 bg-black/30 px-3 py-2 font-mono text-[12px] text-white/80">{donate}</code>
          <Copyable text={donate} />
        </div>
      </Card>

      <div className="mb-3 flex items-center justify-between"><h3 className="text-[13px] font-medium">API-ключи</h3><button className="sa-btn sa-btn-primary !h-8"><Plus className="h-3.5 w-3.5" /> Создать</button></div>
      <Card className="!p-0 mb-6 overflow-hidden">
        <table className="w-full text-left text-[12px]">
          <thead><tr className="border-b border-white/[0.07] font-mono text-[10px] uppercase tracking-wider text-white/35">
            <th className="px-4 py-2.5 font-semibold">Имя</th><th className="px-4 py-2.5 font-semibold">Токен</th><th className="px-4 py-2.5 font-semibold">Scope</th><th className="px-4 py-2.5 font-semibold">Активность</th><th className="px-4 py-2.5" />
          </tr></thead>
          <tbody className="divide-y divide-white/[0.05]">
            {API_KEYS.map((k) => (
              <tr key={k.id} className="text-white/55">
                <td className="px-4 py-3 font-medium text-white/85">{k.name}</td>
                <td className="px-4 py-3 font-mono text-[11px]">
                  {shown === k.id ? k.token : `${k.token.slice(0, 12)}••••••••`}
                  <button onClick={() => setShown(shown === k.id ? null : k.id)} className="ml-2 align-middle text-white/35 hover:text-white">
                    {shown === k.id ? <EyeOff className="inline h-3.5 w-3.5" /> : <Eye className="inline h-3.5 w-3.5" />}
                  </button>
                </td>
                <td className="px-4 py-3 font-mono text-[11px]">{k.scope}</td>
                <td className="px-4 py-3 text-white/40">{k.lastUsed}</td>
                <td className="px-4 py-3 text-right"><button className="text-[11px] text-white/45 hover:text-white">Ротировать</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      <div className="mb-3 flex items-center justify-between"><h3 className="text-[13px] font-medium">Вебхуки Discord / Telegram</h3><button className="text-[11.5px] text-white/50 hover:text-white">+ Добавить</button></div>
      <div className="space-y-3">
        {WEBHOOKS.map((w) => (
          <Card key={w.id} className="flex flex-wrap items-center gap-3">
            <Link2 className="h-4 w-4 text-white/35" />
            <code className="font-mono text-[11.5px] text-white/70">{w.url}</code>
            <div className="flex gap-1.5">{w.events.map((e) => <span key={e} className="rounded border border-white/10 px-1.5 py-0.5 font-mono text-[9px] text-white/50">{e}</span>)}</div>
            <span className="ml-auto"><Stat status={w.status} /></span>
          </Card>
        ))}
      </div>
    </>
  );
}

/* ================= 12. SDK & FastDL ================= */
function Sdk() {
  const [tab, setTab] = useState(SNIPPETS[0].id);
  const snip = SNIPPETS.find((s) => s.id === tab)!;
  return (
    <>
      <PageHead title="SDK & FastDL" sub="Ядра, C# SDK, Asset Packer CLI и сниппеты" />
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {[...SDK_DOWNLOADS, { name: 'Asset Packer CLI', file: 'flovmp-asset-packer.exe', size: '8 MB', ver: '1.2.0', kind: 'cli' }].map((d) => (
          <Card key={d.file}>
            <div className="flex items-start justify-between">
              <div><div className="text-[13px] font-medium">{d.name}</div><div className="mt-0.5 font-mono text-[10.5px] text-white/40">{d.file}</div></div>
              <span className="rounded border border-white/10 px-1.5 py-0.5 font-mono text-[9px] text-white/45">{d.ver}</span>
            </div>
            <div className="mt-3 flex items-center justify-between">
              <span className="font-mono text-[11px] text-white/35">{d.size}</span>
              <button className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 text-[11px] text-white/65 hover:text-white"><Download className="h-3.5 w-3.5" /> Скачать</button>
            </div>
          </Card>
        ))}
      </div>
      <div className="mt-6">
        <h3 className="mb-3 text-[13px] font-medium">Примеры кода</h3>
        <Card className="!p-0 overflow-hidden">
          <div className="flex items-center gap-1 border-b border-white/[0.07] p-2">
            {SNIPPETS.map((s) => (
              <button key={s.id} onClick={() => setTab(s.id)} className={`rounded-md px-3 py-1.5 font-mono text-[11px] transition ${tab === s.id ? 'bg-white/10 text-white' : 'text-white/45 hover:text-white/75'}`}>{s.label}</button>
            ))}
            <span className="ml-auto pr-1"><Copyable text={snip.code} /></span>
          </div>
          <pre className="sa-scroll overflow-x-auto bg-black/40 p-4 font-mono text-[11.5px] leading-relaxed text-white/75"><code>{snip.code}</code></pre>
        </Card>
      </div>
    </>
  );
}

/* ================= 13. AI Assistant ================= */
function Ai() {
  const [thread, setThread] = useState(AI_THREAD);
  const [text, setText] = useState('');
  const send = () => {
    if (!text.trim()) return;
    setThread((t) => [...t, { role: 'user', text }, { role: 'assistant', text: 'Анализирую дамп и метрики за последний час — секунду…' }]);
    setText('');
  };
  return (
    <>
      <PageHead title="AI Assistant" sub="Разбор краш-дампов и советы по оптимизации · 37 / 40 запросов" />
      <Card className="!p-0 flex h-[560px] flex-col overflow-hidden">
        <div className="flex items-center gap-2 border-b border-white/[0.07] px-4 py-3 text-[12px] text-white/45">
          <Sparkles className="h-4 w-4" style={{ color: ACCENT }} /> FloV Assistant
        </div>
        <div className="sa-scroll flex-1 space-y-4 overflow-y-auto p-4">
          {thread.map((m, i) => (
            <div key={i} className={`flex gap-3 ${m.role === 'user' ? 'justify-end' : ''}`}>
              {m.role === 'assistant' && <div className="grid h-7 w-7 flex-none place-items-center rounded-lg text-[#08080a]" style={{ background: ACCENT }}><Sparkles className="h-3.5 w-3.5" /></div>}
              <div className={`max-w-[78%] whitespace-pre-wrap rounded-2xl px-3.5 py-2.5 text-[12.5px] leading-relaxed ${m.role === 'user' ? 'bg-white/10 text-white' : 'border border-white/10 bg-white/[0.03] text-white/75'}`}>{m.text}</div>
            </div>
          ))}
        </div>
        <div className="border-t border-white/[0.07] p-3">
          <div className="mb-2 flex flex-wrap gap-1.5">
            {['Разбери последний краш', 'Почему растёт RAM?', 'Как поднять tickrate?'].map((s) => (
              <button key={s} onClick={() => setText(s)} className="rounded-full border border-white/10 bg-white/[0.03] px-2.5 py-1 text-[11px] text-white/55 hover:text-white">{s}</button>
            ))}
          </div>
          <div className="flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-3 py-2">
            <input value={text} onChange={(e) => setText(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && send()} placeholder="Спросить ассистента" className="flex-1 bg-transparent text-[12.5px] text-white outline-none placeholder:text-white/25" />
            <button onClick={send} className="grid h-7 w-7 place-items-center rounded-lg text-[#08080a]" style={{ background: ACCENT }}><Send className="h-3.5 w-3.5" /></button>
          </div>
        </div>
      </Card>
    </>
  );
}

/* ================= 14. Settings ================= */
function SettingsMod() {
  const [tfa, setTfa] = useState(true);
  return (
    <>
      <PageHead title="Settings" sub="2FA / TOTP, сессии и реферальная программа" />
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <h3 className="text-[13px] font-medium">Аккаунт</h3>
          <div className="mt-3 grid gap-3">
            {[['Имя', 'Mikhail D.'], ['Email', 'mikhail@floridav.gg'], ['Telegram', '@mikhail_dev']].map(([l, v]) => (
              <label key={l}><Label>{l}</Label><input defaultValue={v} className="sa-in" /></label>
            ))}
          </div>
          <button className="sa-btn sa-btn-primary mt-4">Сохранить</button>
        </Card>

        <div className="space-y-4">
          <Card className="flex items-center justify-between">
            <div className="flex items-center gap-2"><ShieldCheck className="h-4 w-4" style={{ color: ACCENT }} /><div><div className="text-[13px] font-medium">2FA / TOTP</div><div className="text-[11px] text-white/40">{tfa ? 'Активно · Google Authenticator' : 'Выключено'}</div></div></div>
            <Toggle on={tfa} onChange={() => setTfa((v) => !v)} />
          </Card>
          <Card>
            <h3 className="text-[13px] font-medium">Реферальная программа · 20% RevShare</h3>
            <div className="mt-3 flex items-center gap-2">
              <code className="flex-1 rounded-lg border border-white/10 bg-black/30 px-3 py-2 font-mono text-[12px]" style={{ color: ACCENT }}>FLOV-MIKHAIL</code>
              <Copyable text="FLOV-MIKHAIL" />
            </div>
            <div className="mt-3 grid grid-cols-3 gap-2 text-center">
              {[['Приглашено', '4'], ['Начислено', '38 400 ₽'], ['Доля', '20%']].map(([l, v]) => (
                <div key={l} className="rounded-lg border border-white/[0.07] bg-white/[0.02] py-2"><div className="text-[13px] font-semibold">{v}</div><div className="font-mono text-[9px] uppercase text-white/35">{l}</div></div>
              ))}
            </div>
          </Card>
          <Card className="!p-0 overflow-hidden">
            <div className="border-b border-white/[0.07] px-4 py-2.5 text-[12px] text-white/45">Активные сессии</div>
            <div className="divide-y divide-white/[0.05]">
              {SESSIONS.map((s, i) => (
                <div key={i} className="flex items-center gap-3 px-4 py-3 text-[12px]">
                  <div><div className="text-white/80">{s.agent}</div><div className="font-mono text-[10px] text-white/35">{s.ip} · {s.where}</div></div>
                  <span className="ml-auto text-[11px] text-white/35">{s.last}</span>
                  {s.current ? <span className="rounded border border-[#3fb984]/30 px-1.5 py-0.5 text-[10px] text-[#3fb984]">эта</span> : <button className="text-[11px] text-[#e5484d]/80 hover:text-[#e5484d]">Выйти</button>}
                </div>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </>
  );
}
