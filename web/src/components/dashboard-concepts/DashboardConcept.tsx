'use client';

import React, { FormEvent, useMemo, useState } from 'react';
import Link from 'next/link';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import {
  Activity,
  AlertTriangle,
  ArrowLeft,
  BarChart3,
  Bell,
  Blocks,
  BookOpen,
  Bot,
  Boxes,
  Braces,
  Check,
  ChevronDown,
  CircleDollarSign,
  CloudDownload,
  Command,
  Copy,
  CreditCard,
  Database,
  ExternalLink,
  FileClock,
  Gauge,
  Globe2,
  HardDriveDownload,
  KeyRound,
  Layers3,
  LifeBuoy,
  Link2,
  ListFilter,
  LockKeyhole,
  Menu,
  MoreHorizontal,
  PanelLeftClose,
  Play,
  Plus,
  RefreshCw,
  RotateCw,
  Search,
  Server,
  Settings,
  ShieldCheck,
  SquareTerminal,
  Users,
  Webhook,
  X,
  Zap,
  type LucideIcon,
} from 'lucide-react';

export type DashboardConceptVariant = 'precision' | 'command' | 'studio';

type SectionKey =
  | 'projects'
  | 'overview'
  | 'telemetry'
  | 'analytics'
  | 'watchdog'
  | 'logs'
  | 'console'
  | 'api'
  | 'sdk'
  | 'builder'
  | 'billing'
  | 'affiliate'
  | 'settings';

type NavItem = {
  key: SectionKey;
  label: string;
  icon: LucideIcon;
};

const NAV_GROUPS: Array<{ label: string; icon: LucideIcon; items: NavItem[] }> = [
  {
    label: 'Управление',
    icon: Layers3,
    items: [
      { key: 'projects', label: 'Проекты и серверы', icon: Server },
      { key: 'overview', label: 'Ключи и статус', icon: KeyRound },
    ],
  },
  {
    label: 'Мониторинг',
    icon: Activity,
    items: [
      { key: 'telemetry', label: 'Телеметрия VDS', icon: Gauge },
      { key: 'analytics', label: 'Аналитика', icon: BarChart3 },
      { key: 'watchdog', label: 'Watchdog и краши', icon: AlertTriangle },
      { key: 'logs', label: 'Логи', icon: FileClock },
    ],
  },
  {
    label: 'Инструменты',
    icon: Blocks,
    items: [
      { key: 'console', label: 'txAdmin консоль', icon: SquareTerminal },
      { key: 'api', label: 'API и вебхуки', icon: Braces },
      { key: 'sdk', label: 'Загрузки и SDK', icon: CloudDownload },
      { key: 'builder', label: 'Сборщик лаунчера', icon: Boxes },
    ],
  },
  {
    label: 'Финансы',
    icon: CircleDollarSign,
    items: [
      { key: 'billing', label: 'Лицензия и счета', icon: CreditCard },
      { key: 'affiliate', label: 'Партнёрская программа', icon: Users },
    ],
  },
  {
    label: 'Аккаунт',
    icon: Settings,
    items: [{ key: 'settings', label: 'Настройки', icon: Settings }],
  },
];

const NAV_ITEMS = NAV_GROUPS.flatMap((group) => group.items);

const VARIANTS: Record<DashboardConceptVariant, { number: string; name: string; note: string }> = {
  precision: {
    number: '01',
    name: 'Precision',
    note: 'Плотный рабочий интерфейс',
  },
  command: {
    number: '02',
    name: 'Command',
    note: 'Операционный центр',
  },
  studio: {
    number: '03',
    name: 'Studio',
    note: 'Проектное пространство',
  },
};

const SECTION_COPY: Record<SectionKey, { title: string; description: string }> = {
  projects: { title: 'Проекты и серверы', description: 'Среды проекта, ресурсы, команда и быстрые действия.' },
  overview: { title: 'Ключи и статус', description: 'Лицензия проекта, привязка сервера и контроль первого запуска.' },
  telemetry: { title: 'Телеметрия VDS', description: 'Метрики агента появятся после первого heartbeat сервера.' },
  analytics: { title: 'Аналитика', description: 'Онлайн, стабильность и нагрузка проекта в выбранном периоде.' },
  watchdog: { title: 'Watchdog и краши', description: 'Авторестарт, аварийные завершения и дампы CoreCLR.' },
  logs: { title: 'Логи', description: 'Поиск и фильтрация событий всех сред проекта.' },
  console: { title: 'txAdmin консоль', description: 'Команды игровому серверу и поток вывода агента.' },
  api: { title: 'API и вебхуки', description: 'Ключи доступа, Discord, Telegram и внешние интеграции.' },
  sdk: { title: 'Загрузки и SDK', description: 'Ядра, SDK, Asset Packer и документация проекта.' },
  builder: { title: 'Сборщик лаунчера', description: 'Брендинг, адрес сервера и публикация клиентской сборки.' },
  billing: { title: 'Лицензия и счета', description: 'Единственная бессрочная лицензия проекта и история оплаты.' },
  affiliate: { title: 'Партнёрская программа', description: 'Промокод проекта и пожизненное вознаграждение 20%.' },
  settings: { title: 'Настройки', description: 'Безопасность аккаунта, сессии и уведомления.' },
};

const SURFACE = 'border border-white/[0.08] bg-white/[0.025]';
const MUTED = 'text-white/45';

function Logo() {
  return (
    <div className="flex items-center gap-2.5">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src="/branding/logo.png" alt="" width="30" height="30" className="h-[30px] w-[30px] object-contain" />
      <span translate="no" className="text-sm font-extrabold text-white">
        FloV<span className="text-brand">:MP</span>
      </span>
    </div>
  );
}

function StatusDot({ tone = 'muted' }: { tone?: 'online' | 'warning' | 'muted' }) {
  const color = tone === 'online' ? 'bg-emerald-400' : tone === 'warning' ? 'bg-amber-400' : 'bg-white/25';
  return <span aria-hidden="true" className={`h-1.5 w-1.5 rounded-full ${color}`} />;
}

function ActionButton({ children, onClick, primary = false, disabled = false, type = 'button' }: { children: React.ReactNode; onClick?: () => void; primary?: boolean; disabled?: boolean; type?: 'button' | 'submit' }) {
  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      className={`inline-flex h-9 items-center justify-center gap-2 rounded-lg px-3.5 text-xs font-bold transition-[transform,color,background-color,border-color,box-shadow,opacity] duration-200 active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-40 ${
        primary
          ? 'border border-brand bg-brand text-[#16040c] shadow-[0_8px_24px_-12px_rgba(255,61,138,0.8)] hover:bg-[#ff5b9d]'
          : 'border border-white/[0.1] bg-white/[0.035] text-white/70 hover:border-white/20 hover:bg-white/[0.065] hover:text-white'
      }`}
    >
      {children}
    </button>
  );
}

function EmptyChart({ compact = false }: { compact?: boolean }) {
  return (
    <div className={`relative overflow-hidden rounded-xl border border-dashed border-white/[0.1] bg-black/10 ${compact ? 'h-36' : 'h-52'}`}>
      <div aria-hidden="true" className="absolute inset-0 opacity-30 [background-image:linear-gradient(rgba(255,255,255,.05)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,.05)_1px,transparent_1px)] [background-size:40px_40px]" />
      <div className="relative flex h-full flex-col items-center justify-center px-6 text-center">
        <Activity aria-hidden="true" className="h-5 w-5 text-white/25" />
        <p className="mt-3 text-xs font-semibold text-white/60">Ожидаем данные агента</p>
        <p className="mt-1 text-[11px] text-white/30">График появится после первого heartbeat</p>
      </div>
    </div>
  );
}

function Metric({ label, value, note, icon: Icon }: { label: string; value: string; note: string; icon: LucideIcon }) {
  return (
    <div className={`${SURFACE} rounded-xl p-4`}>
      <div className="flex items-center justify-between">
        <span className="text-[11px] font-semibold text-white/40">{label}</span>
        <Icon aria-hidden="true" className="h-4 w-4 text-brand" />
      </div>
      <div className="mt-5 text-xl font-extrabold text-white">{value}</div>
      <div className="mt-1 text-[10px] text-white/30">{note}</div>
    </div>
  );
}

function Toggle({ label, description, initial = false }: { label: string; description: string; initial?: boolean }) {
  const [enabled, setEnabled] = useState(initial);
  return (
    <button type="button" onClick={() => setEnabled((value) => !value)} className="group flex w-full items-center justify-between gap-5 rounded-xl border border-white/[0.08] bg-white/[0.02] p-4 text-left hover:border-white/[0.14] hover:bg-white/[0.035]">
      <span>
        <span className="block text-xs font-bold text-white/80">{label}</span>
        <span className="mt-1 block text-[11px] leading-relaxed text-white/35">{description}</span>
      </span>
      <span className={`relative h-6 w-10 shrink-0 rounded-full transition-colors ${enabled ? 'bg-brand' : 'bg-white/10'}`}>
        <span className={`absolute top-1 h-4 w-4 rounded-full bg-white shadow-sm transition-transform ${enabled ? 'translate-x-5' : 'translate-x-1'}`} />
      </span>
    </button>
  );
}

function ProjectOverview({ notify }: { notify: (message: string) => void }) {
  const [environment, setEnvironment] = useState('Production');
  const [serverState, setServerState] = useState<'online' | 'restarting'>('online');

  const restart = () => {
    setServerState('restarting');
    notify('Команда перезапуска поставлена в очередь');
    window.setTimeout(() => setServerState('online'), 1200);
  };

  return (
    <div className="space-y-4">
      <div className="grid gap-4 xl:grid-cols-[1.35fr_.65fr]">
        <section className={`${SURFACE} rounded-2xl p-5`}>
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-base font-extrabold text-white">Проект клиента</h3>
                <span className="rounded-md border border-brand/25 bg-brand/[0.08] px-2 py-1 text-[9px] font-bold uppercase tracking-wider text-brand">Lifetime</span>
              </div>
              <p className={`mt-1 text-xs ${MUTED}`}>Основное рабочее пространство проекта</p>
            </div>
            <ActionButton onClick={() => notify('Окно создания проекта открыто')}><Plus aria-hidden="true" className="h-3.5 w-3.5" /> Новый проект</ActionButton>
          </div>
          <div className="mt-5 flex gap-2 overflow-x-auto pb-1">
            {['Production', 'Development', 'Test'].map((item) => (
              <button key={item} type="button" onClick={() => setEnvironment(item)} className={`rounded-lg px-3 py-2 text-[11px] font-bold transition-colors ${environment === item ? 'bg-brand text-[#17040c]' : 'bg-white/[0.035] text-white/45 hover:text-white'}`}>
                {item}
              </button>
            ))}
          </div>
          <div className="mt-4 rounded-xl border border-white/[0.08] bg-black/10 p-4">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div className="flex items-center gap-3">
                <span className="grid h-9 w-9 place-items-center rounded-lg bg-emerald-400/10 text-emerald-300"><Server aria-hidden="true" className="h-4 w-4" /></span>
                <div>
                  <div className="flex items-center gap-2 text-xs font-bold text-white/85"><StatusDot tone={serverState === 'online' ? 'online' : 'warning'} /> {environment}</div>
                  <p className="mt-1 font-mono text-[10px] text-white/30">188.127.229.224:7788</p>
                </div>
              </div>
              <div className="flex gap-2">
                <ActionButton onClick={() => notify('Запуск сервера поставлен в очередь')}><Play aria-hidden="true" className="h-3.5 w-3.5" /> Запустить</ActionButton>
                <ActionButton onClick={restart} disabled={serverState === 'restarting'}><RotateCw aria-hidden="true" className={`h-3.5 w-3.5 ${serverState === 'restarting' ? 'animate-spin' : ''}`} /> {serverState === 'restarting' ? 'Перезапуск…' : 'Перезапустить'}</ActionButton>
              </div>
            </div>
          </div>
        </section>
        <section className={`${SURFACE} rounded-2xl p-5`}>
          <div className="flex items-center justify-between">
            <h3 className="text-xs font-bold text-white/75">Команда проекта</h3>
            <Users aria-hidden="true" className="h-4 w-4 text-brand" />
          </div>
          <div className="mt-4 space-y-3">
            {['Owner', 'Administrator', 'Developer'].map((role, index) => (
              <div key={role} className="flex items-center justify-between text-[11px]">
                <span className="flex items-center gap-2 text-white/55"><span className="grid h-6 w-6 place-items-center rounded-full bg-white/[0.06] text-[9px] text-white/55">{index + 1}</span>{role}</span>
                <span className="text-white/25">{index === 0 ? '1' : '—'}</span>
              </div>
            ))}
          </div>
          <button type="button" onClick={() => notify('Приглашение участника создано')} className="mt-5 flex w-full items-center justify-center gap-2 rounded-lg border border-dashed border-white/[0.12] py-2.5 text-[11px] font-semibold text-white/40 hover:border-brand/30 hover:text-brand">
            <Plus aria-hidden="true" className="h-3.5 w-3.5" /> Добавить участника
          </button>
        </section>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Metric label="Агент сервера" value="Подключён" note="Production" icon={Link2} />
        <Metric label="Лицензия" value="Lifetime" note="Без продления" icon={ShieldCheck} />
        <Metric label="Последний heartbeat" value="Ожидается" note="Демо-состояние" icon={Activity} />
      </div>

      <div className="grid gap-4 xl:grid-cols-[1.25fr_.75fr]">
        <section className={`${SURFACE} overflow-hidden rounded-2xl`}>
          <div className="flex items-center justify-between border-b border-white/[0.07] px-5 py-4">
            <div><h3 className="text-xs font-bold text-white/75">Ресурсы сервера</h3><p className="mt-1 text-[10px] text-white/30">Горячее управление без перезапуска среды</p></div>
            <ActionButton onClick={() => notify('Окно добавления ресурса открыто')}><Plus aria-hidden="true" className="h-3.5 w-3.5" /> Добавить</ActionButton>
          </div>
          <div className="divide-y divide-white/[0.06]">
            {[
              ['core-runtime', 'Скрипт', 'Запущен'],
              ['voice-module', 'Модуль', 'Запущен'],
              ['project-map', 'Карта', 'Остановлен'],
            ].map(([name, type, state]) => (
              <div key={name} className="flex flex-wrap items-center gap-3 px-5 py-3.5">
                <span className="grid h-8 w-8 place-items-center rounded-lg bg-white/[0.04] text-white/35"><Blocks aria-hidden="true" className="h-4 w-4" /></span>
                <span className="min-w-0 flex-1"><span className="block truncate font-mono text-[11px] text-white/65">{name}</span><span className="mt-0.5 block text-[9px] text-white/25">{type}</span></span>
                <span className="flex items-center gap-2 text-[10px] text-white/35"><StatusDot tone={state === 'Запущен' ? 'online' : 'muted'} />{state}</span>
                <button type="button" onClick={() => notify(`${name}: команда отправлена`)} className="rounded-md p-2 text-white/30 hover:bg-white/[0.05] hover:text-brand" aria-label={`Перезапустить ${name}`}><RefreshCw aria-hidden="true" className="h-3.5 w-3.5" /></button>
              </div>
            ))}
          </div>
        </section>
        <section className={`${SURFACE} rounded-2xl p-5`}>
          <div className="flex items-center justify-between"><div><h3 className="text-xs font-bold text-white/75">Политика FloV:ID</h3><p className="mt-1 text-[10px] text-white/30">Защита аккаунтов проекта</p></div><ShieldCheck aria-hidden="true" className="h-4 w-4 text-brand" /></div>
          <div className="mt-4 space-y-3">
            <Toggle label="Strict" description="Строгая HWID-проверка для Production." initial />
            <Toggle label="Разрешить VPN" description="Допускать подключения через VPN-сервисы." />
          </div>
        </section>
      </div>
    </div>
  );
}

function LicenseOverview({ notify }: { notify: (message: string) => void }) {
  const [visible, setVisible] = useState(false);
  return (
    <div className="grid gap-4 xl:grid-cols-[1.2fr_.8fr]">
      <section className={`${SURFACE} rounded-2xl p-5`}>
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-brand">Активная лицензия</p>
            <h3 className="mt-2 text-lg font-extrabold text-white">FloV:MP Lifetime</h3>
          </div>
          <span className="flex items-center gap-2 rounded-full bg-emerald-400/10 px-2.5 py-1 text-[10px] font-bold text-emerald-300"><StatusDot tone="online" /> Активна</span>
        </div>
        <div className="mt-6 rounded-xl border border-white/[0.08] bg-black/15 p-4">
          <div className="text-[10px] text-white/30">Лицензионный ключ</div>
          <div className="mt-2 flex items-center justify-between gap-4">
            <code className="truncate text-xs text-white/70">{visible ? 'FLV-DEMO-KEY0-0001' : 'FLV-••••-••••-••••'}</code>
            <div className="flex gap-1">
              <button type="button" onClick={() => setVisible((value) => !value)} className="rounded-md p-2 text-white/35 hover:bg-white/[0.06] hover:text-white" aria-label={visible ? 'Скрыть ключ' : 'Показать ключ'}><KeyRound aria-hidden="true" className="h-4 w-4" /></button>
              <button type="button" onClick={() => notify('Ключ скопирован')} className="rounded-md p-2 text-white/35 hover:bg-white/[0.06] hover:text-white" aria-label="Копировать ключ"><Copy aria-hidden="true" className="h-4 w-4" /></button>
            </div>
          </div>
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          <ActionButton primary onClick={() => notify('Скачивание серверного пакета началось')}><HardDriveDownload aria-hidden="true" className="h-3.5 w-3.5" /> Скачать сервер</ActionButton>
          <ActionButton onClick={() => notify('Окно привязки IP открыто')}><Globe2 aria-hidden="true" className="h-3.5 w-3.5" /> Привязать IPv4</ActionButton>
        </div>
      </section>
      <section className={`${SURFACE} rounded-2xl p-5`}>
        <p className="text-xs font-bold text-white/75">Первый запуск</p>
        <div className="mt-4 space-y-3">
          {['Аккаунт создан', 'Лицензия активирована', 'IPv4 привязан', 'Агент подключён'].map((step, index) => (
            <div key={step} className="flex items-center gap-3 text-[11px] text-white/50">
              <span className={`grid h-5 w-5 place-items-center rounded-full ${index < 2 ? 'bg-emerald-400/15 text-emerald-300' : 'bg-white/[0.06] text-white/25'}`}>{index < 2 ? <Check aria-hidden="true" className="h-3 w-3" /> : index + 1}</span>
              {step}
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

function ConsolePanel({ notify }: { notify: (message: string) => void }) {
  const [command, setCommand] = useState('');
  const [lines, setLines] = useState(['[system] Подключение к Remote Agent ожидается…']);
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const value = command.trim();
    if (!value) return;
    setLines((current) => [...current.slice(-6), `> ${value}`, '[queue] Команда добавлена в очередь']);
    setCommand('');
    notify('Команда отправлена в очередь');
  };
  return (
    <section className="overflow-hidden rounded-2xl border border-white/[0.1] bg-[#08080a]">
      <div className="flex items-center justify-between border-b border-white/[0.07] px-4 py-3">
        <span className="flex items-center gap-2 text-[11px] font-bold text-white/60"><StatusDot tone="warning" /> SSE отключён</span>
        <ActionButton onClick={() => notify('Переключатель SSE изменён')}><Zap aria-hidden="true" className="h-3.5 w-3.5" /> Включить поток</ActionButton>
      </div>
      <div className="h-72 overflow-y-auto p-5 font-mono text-[11px] leading-7 text-white/45" aria-live="polite">
        {lines.map((line, index) => <div key={`${line}-${index}`} className={line.startsWith('>') ? 'text-brand' : ''}>{line}</div>)}
      </div>
      <form onSubmit={submit} className="flex border-t border-white/[0.07] p-3">
        <label htmlFor="concept-console" className="sr-only">Команда сервера</label>
        <input id="concept-console" name="command" value={command} onChange={(event) => setCommand(event.target.value)} autoComplete="off" spellCheck={false} placeholder="Введите RCON-команду…" className="min-w-0 flex-1 bg-transparent px-2 font-mono text-xs text-white placeholder:text-white/20" />
        <ActionButton type="submit" primary><Command aria-hidden="true" className="h-3.5 w-3.5" /> Выполнить</ActionButton>
      </form>
    </section>
  );
}

function GenericSection({ section, notify }: { section: SectionKey; notify: (message: string) => void }) {
  if (section === 'projects') return <ProjectOverview notify={notify} />;
  if (section === 'overview') return <LicenseOverview notify={notify} />;
  if (section === 'console') return <ConsolePanel notify={notify} />;

  if (section === 'telemetry' || section === 'analytics') {
    return (
      <div className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Metric label="Игроки онлайн" value="—" note="Нет heartbeat" icon={Users} />
          <Metric label="Tick Rate" value="—" note="Нет heartbeat" icon={Gauge} />
          <Metric label="CoreCLR RAM" value="—" note="Нет heartbeat" icon={Database} />
          <Metric label="Аптайм" value="—" note="Агент не передал данные" icon={Activity} />
        </div>
        <section className={`${SURFACE} rounded-2xl p-5`}>
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div><h3 className="text-xs font-bold text-white/75">{section === 'analytics' ? 'Динамика проекта' : 'Последние 24 часа'}</h3><p className="mt-1 text-[10px] text-white/30">Только реальные данные игрового агента</p></div>
            <div className="flex gap-1 rounded-lg bg-white/[0.035] p-1">{['24ч', '7д', '30д'].map((period) => <button key={period} type="button" className="rounded-md px-2.5 py-1.5 text-[10px] font-bold text-white/40 hover:bg-white/[0.06] hover:text-white">{period}</button>)}</div>
          </div>
          <EmptyChart />
        </section>
      </div>
    );
  }

  if (section === 'watchdog') {
    return (
      <div className="grid gap-4 xl:grid-cols-[.8fr_1.2fr]">
        <section className={`${SURFACE} rounded-2xl p-5`}>
          <h3 className="text-xs font-bold text-white/75">Политика восстановления</h3>
          <div className="mt-4 space-y-3">
            <Toggle label="Автоматический перезапуск" description="Перезапустить сервер при отсутствии heartbeat более 15 секунд." initial />
            <Toggle label="Сохранять дамп CoreCLR" description="Приложить стек и сведения о ресурсах к инциденту." initial />
          </div>
        </section>
        <section className={`${SURFACE} rounded-2xl p-5`}>
          <h3 className="text-xs font-bold text-white/75">Последние инциденты</h3>
          <div className="mt-6 grid place-items-center rounded-xl border border-dashed border-white/[0.09] py-16 text-center"><ShieldCheck aria-hidden="true" className="h-5 w-5 text-emerald-300/60" /><p className="mt-3 text-xs font-semibold text-white/55">Инцидентов нет</p><p className="mt-1 text-[10px] text-white/25">Новые краши появятся здесь</p></div>
        </section>
      </div>
    );
  }

  if (section === 'logs') {
    return (
      <section className={`${SURFACE} overflow-hidden rounded-2xl`}>
        <div className="flex flex-col gap-3 border-b border-white/[0.07] p-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex gap-2"><ActionButton><ListFilter aria-hidden="true" className="h-3.5 w-3.5" /> Все уровни</ActionButton><ActionButton><Server aria-hidden="true" className="h-3.5 w-3.5" /> Все среды</ActionButton></div>
          <label className="relative"><span className="sr-only">Поиск по логам</span><Search aria-hidden="true" className="absolute left-3 top-2.5 h-4 w-4 text-white/25" /><input name="log-search" autoComplete="off" placeholder="Поиск по логам…" className="h-9 rounded-lg border border-white/[0.09] bg-black/10 pl-9 pr-3 text-xs text-white placeholder:text-white/25" /></label>
        </div>
        <div className="grid place-items-center py-24 text-center"><FileClock aria-hidden="true" className="h-5 w-5 text-white/20" /><p className="mt-3 text-xs font-semibold text-white/50">Событий пока нет</p><p className="mt-1 text-[10px] text-white/25">Подключите игровой агент для получения логов</p></div>
      </section>
    );
  }

  if (section === 'api') {
    return (
      <div className="grid gap-4 xl:grid-cols-2">
        <section className={`${SURFACE} rounded-2xl p-5`}><div className="flex items-center justify-between"><div><p className="text-[10px] font-bold uppercase tracking-wider text-brand">Project API</p><h3 className="mt-2 text-sm font-extrabold text-white">Ключ доступа</h3></div><Braces aria-hidden="true" className="h-5 w-5 text-brand" /></div><div className="mt-5 flex items-center justify-between gap-3 rounded-xl bg-black/15 p-4"><code className="truncate text-[11px] text-white/45">flov_live_••••••••••••••••</code><button type="button" onClick={() => notify('API-ключ скопирован')} aria-label="Копировать API-ключ" className="text-white/35 hover:text-white"><Copy aria-hidden="true" className="h-4 w-4" /></button></div><div className="mt-4 flex gap-2"><ActionButton onClick={() => notify('Новый API-ключ создан')}>Ротировать ключ</ActionButton><ActionButton><BookOpen aria-hidden="true" className="h-3.5 w-3.5" /> Документация</ActionButton></div></section>
        <section className={`${SURFACE} rounded-2xl p-5`}><div className="flex items-center justify-between"><div><p className="text-[10px] font-bold uppercase tracking-wider text-brand">Уведомления</p><h3 className="mt-2 text-sm font-extrabold text-white">Вебхуки</h3></div><Webhook aria-hidden="true" className="h-5 w-5 text-brand" /></div><div className="mt-5 space-y-3"><Toggle label="Discord" description="Краши, высокая нагрузка и восстановление сервера." /><Toggle label="Telegram" description="Критические события и быстрые ссылки в кабинет." /></div></section>
      </div>
    );
  }

  if (section === 'sdk') {
    const downloads = [
      ['Сервер Linux x64', 'CoreCLR · стабильная сборка', Server],
      ['Сервер Windows x64', 'Среда разработки', HardDriveDownload],
      ['C# SDK', 'API, типы и примеры', Braces],
      ['Asset Packer CLI', 'Карты, одежда и транспорт', Blocks],
    ] as const;
    return <div className="grid gap-4 md:grid-cols-2">{downloads.map(([name, note, Icon]) => <section key={name} className={`${SURFACE} group rounded-2xl p-5`}><div className="flex items-start justify-between"><span className="grid h-10 w-10 place-items-center rounded-xl bg-brand/10 text-brand"><Icon aria-hidden="true" className="h-5 w-5" /></span><span className="text-[9px] text-white/25">Доступно по лицензии</span></div><h3 className="mt-6 text-sm font-extrabold text-white">{name}</h3><p className="mt-1 text-[11px] text-white/35">{note}</p><button type="button" onClick={() => notify(`Скачивание: ${name}`)} className="mt-5 flex items-center gap-2 text-[11px] font-bold text-brand hover:text-[#ff70ab]">Скачать <CloudDownload aria-hidden="true" className="h-3.5 w-3.5" /></button></section>)}</div>;
  }

  if (section === 'builder') {
    return (
      <div className="grid gap-4 xl:grid-cols-[1fr_.8fr]">
        <section className={`${SURFACE} rounded-2xl p-5`}><div className="grid gap-4 sm:grid-cols-2"><label className="text-[11px] font-semibold text-white/45">Название проекта<input defaultValue="Проект клиента" name="project-name" autoComplete="off" className="mt-2 h-10 w-full rounded-lg border border-white/[0.09] bg-black/10 px-3 text-xs text-white" /></label><label className="text-[11px] font-semibold text-white/45">Основной цвет<input defaultValue="#ff3d8a" name="primary-color" spellCheck={false} className="mt-2 h-10 w-full rounded-lg border border-white/[0.09] bg-black/10 px-3 font-mono text-xs text-white" /></label><label className="text-[11px] font-semibold text-white/45">IP сервера<input defaultValue="188.127.229.224" name="server-ip" spellCheck={false} className="mt-2 h-10 w-full rounded-lg border border-white/[0.09] bg-black/10 px-3 font-mono text-xs text-white" /></label><label className="text-[11px] font-semibold text-white/45">Порт<input defaultValue="7788" name="server-port" inputMode="numeric" className="mt-2 h-10 w-full rounded-lg border border-white/[0.09] bg-black/10 px-3 font-mono text-xs text-white" /></label></div><div className="mt-5"><ActionButton primary onClick={() => notify('Демо-сборка поставлена в очередь')}><Boxes aria-hidden="true" className="h-3.5 w-3.5" /> Собрать лаунчер</ActionButton></div></section>
        <section className="relative overflow-hidden rounded-2xl border border-white/[0.09] bg-gradient-to-br from-brand/[0.12] to-white/[0.02] p-6"><div className="absolute -right-12 -top-12 h-40 w-40 rounded-full bg-brand/10 blur-3xl" /><Logo /><h3 className="relative mt-16 text-xl font-extrabold text-white">Лаунчер проекта</h3><p className="relative mt-2 text-xs text-white/40">Обновление клиента · подключение · новости</p><div className="relative mt-6 h-2 overflow-hidden rounded-full bg-white/[0.06]"><div className="h-full w-2/3 rounded-full bg-brand" /></div></section>
      </div>
    );
  }

  if (section === 'billing') {
    return (
      <div className="grid gap-4 xl:grid-cols-[.8fr_1.2fr]">
        <section className="rounded-2xl border border-brand/25 bg-brand/[0.055] p-6"><p className="text-[10px] font-bold uppercase tracking-[0.18em] text-brand">Единственный план</p><h3 className="mt-3 text-xl font-extrabold text-white">Lifetime</h3><p className="mt-2 text-xs leading-relaxed text-white/45">Одна лицензия на проект. Без подписки и автоматических списаний.</p><div className="mt-7 text-3xl font-extrabold text-white">25 000 ₽</div><ActionButton primary onClick={() => notify('Переход к оплате')}><CreditCard aria-hidden="true" className="h-3.5 w-3.5" /> Купить лицензию</ActionButton></section>
        <section className={`${SURFACE} rounded-2xl p-5`}><h3 className="text-xs font-bold text-white/75">История счетов</h3><div className="mt-5 grid place-items-center rounded-xl border border-dashed border-white/[0.09] py-16 text-center"><CreditCard aria-hidden="true" className="h-5 w-5 text-white/20" /><p className="mt-3 text-xs font-semibold text-white/50">Счетов пока нет</p></div></section>
      </div>
    );
  }

  if (section === 'affiliate') {
    return (
      <div className="grid gap-4 xl:grid-cols-[1.1fr_.9fr]">
        <section className="rounded-2xl border border-brand/25 bg-brand/[0.055] p-6"><p className="text-[10px] font-bold uppercase tracking-[0.18em] text-brand">RevShare</p><h3 className="mt-3 text-xl font-extrabold text-white">20% пожизненно</h3><p className="mt-2 max-w-md text-xs leading-relaxed text-white/45">Получайте вознаграждение с каждой лицензии проекта, который пришёл по вашему коду.</p><div className="mt-6 flex items-center justify-between gap-3 rounded-xl bg-black/15 p-4"><code className="text-sm font-bold text-white">FLOV-PROJECT</code><button type="button" onClick={() => notify('Промокод скопирован')} aria-label="Копировать промокод" className="text-white/35 hover:text-white"><Copy aria-hidden="true" className="h-4 w-4" /></button></div></section>
        <section className={`${SURFACE} rounded-2xl p-5`}><h3 className="text-xs font-bold text-white/75">Статистика</h3><div className="mt-5 grid grid-cols-2 gap-3"><Metric label="Проекты" value="—" note="Нет приглашений" icon={Users} /><Metric label="Начислено" value="—" note="Нет операций" icon={CircleDollarSign} /></div></section>
      </div>
    );
  }

  return (
    <div className="grid gap-4 xl:grid-cols-2">
      <section className={`${SURFACE} rounded-2xl p-5`}><div className="flex items-center gap-3"><span className="grid h-10 w-10 place-items-center rounded-xl bg-brand/10 text-brand"><LockKeyhole aria-hidden="true" className="h-5 w-5" /></span><div><h3 className="text-sm font-extrabold text-white">Безопасность аккаунта</h3><p className="mt-1 text-[11px] text-white/35">2FA, резервные коды и активные сессии</p></div></div><div className="mt-5 space-y-3"><Toggle label="Двухфакторная защита" description="Подтверждать вход кодом из приложения-аутентификатора." /><Toggle label="Уведомления о входе" description="Отправлять письмо при входе с нового устройства." initial /></div></section>
      <section className={`${SURFACE} rounded-2xl p-5`}><div className="flex items-center gap-3"><span className="grid h-10 w-10 place-items-center rounded-xl bg-white/[0.05] text-white/50"><Settings aria-hidden="true" className="h-5 w-5" /></span><div><h3 className="text-sm font-extrabold text-white">Интерфейс и уведомления</h3><p className="mt-1 text-[11px] text-white/35">Язык, тема и каналы событий</p></div></div><div className="mt-5 space-y-3"><Toggle label="Telegram-уведомления" description="Критические события проекта и серверов." /><Toggle label="Компактный режим" description="Уменьшить высоту строк и карточек кабинета." /></div></section>
    </div>
  );
}

function VariantSwitcher({ active }: { active: DashboardConceptVariant }) {
  return (
    <div className="flex items-center gap-1 rounded-xl border border-white/[0.08] bg-black/20 p-1" aria-label="Варианты дизайна">
      {(Object.keys(VARIANTS) as DashboardConceptVariant[]).map((key) => {
        const item = VARIANTS[key];
        return (
          <Link key={key} href={`/dashboard/concepts/${key}`} className={`flex h-8 items-center gap-2 rounded-lg px-3 text-[10px] font-bold transition-colors ${active === key ? 'bg-white/[0.09] text-white' : 'text-white/35 hover:text-white/75'}`} aria-current={active === key ? 'page' : undefined}>
            <span className={active === key ? 'text-brand' : ''}>{item.number}</span>
            <span className="hidden sm:inline">{item.name}</span>
          </Link>
        );
      })}
    </div>
  );
}

function SectionHeader({ active, variant, notify }: { active: SectionKey; variant: DashboardConceptVariant; notify: (message: string) => void }) {
  const copy = SECTION_COPY[active];
  return (
    <header className={`mb-5 flex flex-col gap-4 ${variant === 'precision' ? 'border-b border-white/[0.07] pb-5' : ''} sm:flex-row sm:items-end sm:justify-between`}>
      <div>
        <div className="mb-2 flex items-center gap-2 text-[10px] font-bold uppercase tracking-[0.16em] text-brand"><span>Проект клиента</span><span className="text-white/15">/</span><span className="text-white/35">{copy.title}</span></div>
        <h1 className="text-xl font-extrabold tracking-tight text-white sm:text-2xl">{copy.title}</h1>
        <p className="mt-1.5 text-xs leading-relaxed text-white/40">{copy.description}</p>
      </div>
      <div className="flex gap-2"><ActionButton onClick={() => notify('Данные обновлены')}><RefreshCw aria-hidden="true" className="h-3.5 w-3.5" /> Обновить</ActionButton><ActionButton primary onClick={() => notify('Панель быстрых действий открыта')}><Zap aria-hidden="true" className="h-3.5 w-3.5" /> Быстрое действие</ActionButton></div>
    </header>
  );
}

function StandardSidebar({ active, select, variant }: { active: SectionKey; select: (key: SectionKey) => void; variant: 'precision' | 'studio' }) {
  return (
    <aside className={`hidden shrink-0 border-r border-white/[0.07] lg:flex lg:flex-col ${variant === 'precision' ? 'w-[248px] bg-[#0d0d10]' : 'w-[292px] bg-[#101013]'}`}>
      <div className="flex h-16 items-center justify-between border-b border-white/[0.07] px-5"><Logo /><button type="button" aria-label="Свернуть меню" className="rounded-md p-1.5 text-white/25 hover:bg-white/[0.05] hover:text-white"><PanelLeftClose aria-hidden="true" className="h-4 w-4" /></button></div>
      <button type="button" className={`mx-3 mt-4 flex items-center justify-between rounded-xl border border-white/[0.08] bg-white/[0.025] p-3 text-left ${variant === 'studio' ? 'py-4' : ''}`}>
        <span><span className="block text-[10px] text-white/30">Текущий проект</span><span className="mt-1 block text-xs font-bold text-white/80">Проект клиента</span></span><ChevronDown aria-hidden="true" className="h-4 w-4 text-white/25" />
      </button>
      <nav className="min-h-0 flex-1 overflow-y-auto px-3 py-4" aria-label="Разделы кабинета">
        {NAV_GROUPS.map((group, groupIndex) => (
          <div key={group.label} className={groupIndex > 0 ? 'mt-5' : ''}>
            <div className="mb-1.5 px-2 text-[9px] font-bold uppercase tracking-[0.18em] text-white/25">{group.label}</div>
            <div className="space-y-1">{group.items.map((item) => {
              const Icon = item.icon;
              const selected = active === item.key;
              return <button key={item.key} type="button" data-section={item.key} onClick={() => select(item.key)} className={`group relative flex w-full items-center gap-3 rounded-lg px-2.5 py-2.5 text-left text-[11px] font-bold transition-colors ${selected ? (variant === 'precision' ? 'bg-white/[0.07] text-white' : 'bg-brand/[0.1] text-brand') : 'text-white/38 hover:bg-white/[0.035] hover:text-white/75'}`}>{selected && variant === 'precision' && <span className="absolute -left-3 h-5 w-0.5 rounded-r-full bg-brand" />}<Icon aria-hidden="true" className={`h-4 w-4 ${selected ? 'text-brand' : 'text-white/25 group-hover:text-white/50'}`} /><span className="truncate">{item.label}</span></button>;
            })}</div>
          </div>
        ))}
      </nav>
      <div className="border-t border-white/[0.07] p-3"><button type="button" className="flex w-full items-center gap-3 rounded-lg p-2 text-left hover:bg-white/[0.04]"><span className="grid h-8 w-8 place-items-center rounded-lg bg-brand/10 text-[10px] font-extrabold text-brand">FG</span><span className="min-w-0 flex-1"><span className="block truncate text-[11px] font-bold text-white/65">Владелец проекта</span><span className="block truncate text-[9px] text-white/25">owner@flovmp.ru</span></span><MoreHorizontal aria-hidden="true" className="h-4 w-4 text-white/20" /></button></div>
    </aside>
  );
}

function CommandSidebar({ active, select }: { active: SectionKey; select: (key: SectionKey) => void }) {
  const [groupIndex, setGroupIndex] = useState(() => Math.max(0, NAV_GROUPS.findIndex((group) => group.items.some((item) => item.key === active))));
  const group = NAV_GROUPS[groupIndex];
  const selectGroup = (index: number) => {
    setGroupIndex(index);
    select(NAV_GROUPS[index].items[0].key);
  };
  return (
    <div className="hidden shrink-0 lg:flex">
      <aside className="flex w-[68px] flex-col items-center border-r border-white/[0.07] bg-[#09090b] py-3">
        <Link href="/" aria-label="На главную" className="mb-5 grid h-10 w-10 place-items-center rounded-xl bg-white/[0.035]">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/branding/logo.png" alt="" width="28" height="28" className="h-7 w-7 object-contain" />
        </Link>
        <nav className="space-y-2" aria-label="Категории кабинета">{NAV_GROUPS.map((item, index) => { const Icon = item.icon; const selected = groupIndex === index; return <button key={item.label} type="button" onClick={() => selectGroup(index)} aria-label={item.label} title={item.label} className={`grid h-10 w-10 place-items-center rounded-xl transition-colors ${selected ? 'bg-brand text-[#16040c]' : 'text-white/28 hover:bg-white/[0.05] hover:text-white/70'}`}><Icon aria-hidden="true" className="h-[18px] w-[18px]" /></button>; })}</nav>
        <button type="button" aria-label="Помощь" className="mt-auto grid h-10 w-10 place-items-center rounded-xl text-white/25 hover:bg-white/[0.05] hover:text-white"><LifeBuoy aria-hidden="true" className="h-[18px] w-[18px]" /></button>
      </aside>
      <aside className="flex w-[220px] flex-col border-r border-white/[0.07] bg-[#0e0e11]">
        <div className="border-b border-white/[0.07] px-4 py-[18px]"><p className="text-[9px] font-bold uppercase tracking-[0.18em] text-brand">{group.label}</p><p className="mt-1 text-xs font-extrabold text-white">Проект клиента</p></div>
        <nav className="space-y-1 p-3" aria-label={group.label}>{group.items.map((item) => { const Icon = item.icon; const selected = active === item.key; return <button key={item.key} type="button" data-section={item.key} onClick={() => select(item.key)} className={`flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-[11px] font-bold transition-colors ${selected ? 'bg-white/[0.07] text-white' : 'text-white/35 hover:bg-white/[0.035] hover:text-white/70'}`}><Icon aria-hidden="true" className={selected ? 'h-4 w-4 text-brand' : 'h-4 w-4 text-white/25'} />{item.label}</button>; })}</nav>
        <div className="mx-3 mt-auto mb-3 rounded-xl border border-white/[0.07] bg-black/15 p-3"><div className="flex items-center gap-2 text-[10px] font-bold text-white/55"><StatusDot tone="online" /> Production</div><p className="mt-2 text-[9px] leading-relaxed text-white/25">Remote Agent подключён</p></div>
      </aside>
    </div>
  );
}

function ActivityRail() {
  return (
    <aside className="hidden w-[260px] shrink-0 border-l border-white/[0.07] bg-[#0d0d10] p-4 2xl:block">
      <div className="flex items-center justify-between"><h2 className="text-[11px] font-extrabold text-white/65">Активность</h2><Bell aria-hidden="true" className="h-4 w-4 text-white/25" /></div>
      <div className="mt-5 space-y-5">{[
        ['Проект создан', 'Рабочее пространство готово'],
        ['Лицензия активна', 'Lifetime · один проект'],
        ['Ожидается агент', 'Подключите Production'],
      ].map(([title, note], index) => <div key={title} className="relative pl-5 before:absolute before:left-[3px] before:top-2 before:h-full before:w-px before:bg-white/[0.07] last:before:hidden"><span className={`absolute left-0 top-1 h-2 w-2 rounded-full ${index < 2 ? 'bg-brand' : 'bg-white/20'}`} /><p className="text-[10px] font-bold text-white/60">{title}</p><p className="mt-1 text-[9px] leading-relaxed text-white/28">{note}</p></div>)}</div>
      <div className="mt-8 rounded-xl border border-white/[0.07] bg-white/[0.02] p-4"><Bot aria-hidden="true" className="h-4 w-4 text-brand" /><p className="mt-3 text-[10px] font-bold text-white/60">Нужна помощь?</p><p className="mt-1 text-[9px] leading-relaxed text-white/28">Откройте документацию или напишите в поддержку.</p><button type="button" className="mt-3 flex items-center gap-1.5 text-[9px] font-bold text-brand">Документация <ExternalLink aria-hidden="true" className="h-3 w-3" /></button></div>
    </aside>
  );
}

export default function DashboardConcept({ variant }: { variant: DashboardConceptVariant }) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const initialSection = searchParams.get('section');
  const [active, setActive] = useState<SectionKey>(NAV_ITEMS.some((item) => item.key === initialSection) ? initialSection as SectionKey : 'projects');
  const [mobileOpen, setMobileOpen] = useState(false);
  const [toast, setToast] = useState('');
  const concept = VARIANTS[variant];
  const selectedItem = useMemo(() => NAV_ITEMS.find((item) => item.key === active) ?? NAV_ITEMS[0], [active]);
  const SelectedIcon = selectedItem.icon;

  const select = (key: SectionKey) => {
    setActive(key);
    setMobileOpen(false);
    const params = new URLSearchParams(searchParams.toString());
    params.set('section', key);
    router.replace(`${pathname}?${params.toString()}`, { scroll: false });
  };

  const notify = (message: string) => {
    setToast(message);
    window.setTimeout(() => setToast(''), 1800);
  };

  return (
    <div className="fixed inset-0 z-[100] overflow-hidden bg-[#09090b] text-white">
      <div className="flex h-full min-w-0 flex-col">
        <div className="flex h-14 shrink-0 items-center gap-3 border-b border-white/[0.07] bg-[#0c0c0f] px-3 sm:px-4">
          <Link href="/" className="grid h-9 w-9 place-items-center rounded-lg text-white/35 hover:bg-white/[0.05] hover:text-white" aria-label="Вернуться на сайт"><ArrowLeft aria-hidden="true" className="h-4 w-4" /></Link>
          <div className="hidden min-w-0 sm:block"><div className="flex items-center gap-2"><span className="text-[9px] font-bold uppercase tracking-[0.16em] text-brand">Концепт {concept.number}</span><span className="text-white/15">•</span><span className="text-[10px] font-bold text-white/60">{concept.name}</span></div><p className="mt-0.5 truncate text-[9px] text-white/25">{concept.note}</p></div>
          <div className="ml-auto"><VariantSwitcher active={variant} /></div>
          <button type="button" onClick={() => setMobileOpen((value) => !value)} className="grid h-9 w-9 place-items-center rounded-lg border border-white/[0.08] text-white/50 lg:hidden" aria-label="Открыть разделы" aria-expanded={mobileOpen}>{mobileOpen ? <X aria-hidden="true" className="h-4 w-4" /> : <Menu aria-hidden="true" className="h-4 w-4" />}</button>
        </div>

        <div className="relative flex min-h-0 flex-1">
          {variant === 'command' ? <CommandSidebar active={active} select={select} /> : <StandardSidebar active={active} select={select} variant={variant} />}

          {mobileOpen && <div className="absolute inset-0 z-30 bg-[#0c0c0f] p-4 lg:hidden"><div className="mb-4 flex items-center justify-between"><Logo /><button type="button" onClick={() => setMobileOpen(false)} aria-label="Закрыть меню" className="p-2 text-white/50"><X aria-hidden="true" className="h-5 w-5" /></button></div><nav className="grid gap-2 sm:grid-cols-2">{NAV_ITEMS.map((item) => { const Icon = item.icon; return <button key={item.key} type="button" onClick={() => select(item.key)} className={`flex items-center gap-3 rounded-xl border p-3 text-left text-xs font-bold ${active === item.key ? 'border-brand/30 bg-brand/10 text-brand' : 'border-white/[0.07] bg-white/[0.02] text-white/55'}`}><Icon aria-hidden="true" className="h-4 w-4" />{item.label}</button>; })}</nav></div>}

          <main className={`min-w-0 flex-1 overflow-y-auto ${variant === 'precision' ? 'bg-[#0b0b0d]' : variant === 'command' ? 'bg-[#0a0a0c]' : 'bg-[#0b0b0e]'}`}>
            <div className={`mx-auto w-full ${variant === 'studio' ? 'max-w-[1180px] p-5 sm:p-7 xl:p-9' : 'max-w-[1240px] p-4 sm:p-6 xl:p-8'}`}>
              <div className="mb-4 flex items-center gap-2 text-[10px] text-white/30 lg:hidden"><SelectedIcon aria-hidden="true" className="h-4 w-4 text-brand" />{selectedItem.label}</div>
              <SectionHeader active={active} variant={variant} notify={notify} />
              <div key={active} className="animate-view-in"><GenericSection section={active} notify={notify} /></div>
            </div>
          </main>

          {variant === 'studio' && <ActivityRail />}
        </div>
      </div>

      <div aria-live="polite" className={`pointer-events-none fixed bottom-5 left-1/2 z-[120] -translate-x-1/2 transition-[opacity,transform] duration-200 ${toast ? 'translate-y-0 opacity-100' : 'translate-y-2 opacity-0'}`}>
        <div className="flex items-center gap-2 rounded-xl border border-white/[0.1] bg-[#17171b] px-4 py-3 text-[11px] font-bold text-white shadow-2xl"><Check aria-hidden="true" className="h-4 w-4 text-emerald-300" />{toast || 'Готово'}</div>
      </div>
    </div>
  );
}
