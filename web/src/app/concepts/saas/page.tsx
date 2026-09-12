'use client';

import React, { useState } from 'react';
import {
  ArrowRight,
  ArrowUpRight,
  Boxes,
  Check,
  ChevronRight,
  Circle,
  Command,
  Cpu,
  Database,
  FileCode2,
  Gauge,
  LayoutDashboard,
  MemoryStick,
  Radio,
  Search,
  Server,
  ShieldBan,
  Terminal,
  Timer,
} from 'lucide-react';
import {
  ACTIVITY,
  FEATURES,
  LICENSE,
  MONTH_LABELS,
  PLATFORM_STATS,
  PROJECTS,
  ROADMAP,
  SERIES,
  SERVERS,
  SIDEBAR,
  WEEK_LABELS,
} from '../_data';
import { AreaChart, Bars, ConceptSwitch, CountUp, Reveal } from '../_ui';

const PINK = '#ff1493';
const NAV: { label: string; href: string }[] = [
  { label: 'Home', href: '#top' },
  { label: 'Features', href: '#features' },
  { label: 'Documentation', href: '/docs' },
  { label: 'Projects', href: '#projects' },
  { label: 'Roadmap', href: '#roadmap' },
  { label: 'Pricing', href: '#pricing' },
];
const TG = 'https://t.me/flovmp_dev';

export default function SaaSConcept() {
  const [active, setActive] = useState('Dashboard');

  return (
    <div id="top" className="min-h-screen scroll-smooth bg-[#08080a] font-sans text-[#e9eaee] antialiased [--pink:#ff1493] [&_section]:scroll-mt-16">
      {/* ================= NAV ================= */}
      <header className="sticky top-0 z-50 border-b border-white/[0.07] bg-[#08080a]/85 backdrop-blur-xl">
        <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-5">
          <div className="flex items-center gap-2.5">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/branding/logo.png" alt="" className="h-6 w-6 rounded-md" />
            <span className="text-[15px] font-semibold tracking-tight">FloV:MP</span>
          </div>
          <nav className="hidden items-center gap-6 md:flex">
            {NAV.map((n) => (
              <a key={n.label} href={n.href} className="text-[13px] text-white/55 transition hover:text-white">
                {n.label}
              </a>
            ))}
          </nav>
          <div className="flex items-center gap-2">
            <a href="/concepts/saas/app" className="text-[13px] text-white/55 transition hover:text-white">
              Login
            </a>
            <a
              href="#pricing"
              className="rounded-lg bg-white px-3 py-1.5 text-[13px] font-semibold text-black transition hover:bg-white/90"
            >
              Get License
            </a>
          </div>
        </div>
      </header>

      {/* ================= HERO ================= */}
      <section className="relative overflow-hidden border-b border-white/[0.07]">
        <div className="pointer-events-none absolute left-1/2 top-[-30%] h-[520px] w-[820px] -translate-x-1/2 rounded-full opacity-[0.14] blur-[120px]" style={{ background: PINK }} />
        <div className="mx-auto grid max-w-6xl items-center gap-14 px-5 py-20 lg:grid-cols-[1.05fr_1fr] lg:py-28">
          <Reveal>
            <div className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/[0.03] px-3 py-1 text-[11px] text-white/60">
              <span className="h-1.5 w-1.5 rounded-full" style={{ background: PINK }} />
              Платформа для GTA V проектов · Lifetime
            </div>
            <h1 className="mt-6 text-[2.6rem] font-semibold leading-[1.04] tracking-[-0.03em] sm:text-[3.4rem]">
              Own Your GTA&nbsp;V Project Infrastructure
            </h1>
            <p className="mt-5 max-w-lg text-[15px] leading-relaxed text-white/55">
              Запускайте и управляйте GTA V проектами через единую платформу: мультиплеер, дашборд, SDK
              и интеграции. Аккаунт → проекты → серверы.
            </p>
            <div className="mt-8 flex flex-wrap items-center gap-3">
              <a
                href="#pricing"
                className="inline-flex items-center gap-2 rounded-lg px-4 py-2.5 text-[13px] font-semibold text-black"
                style={{ background: PINK }}
              >
                Buy Lifetime License <ArrowRight className="h-4 w-4" />
              </a>
              <a
                href="/concepts/saas/app"
                className="inline-flex items-center gap-2 rounded-lg border border-white/12 bg-white/[0.03] px-4 py-2.5 text-[13px] font-semibold text-white/80 transition hover:bg-white/[0.06]"
              >
                Открыть дашборд <ArrowUpRight className="h-4 w-4" />
              </a>
            </div>
            <div className="mt-9 flex items-center gap-6 text-[12px] text-white/40">
              <span>24 000 ₽ · разово</span>
              <span className="h-3 w-px bg-white/10" />
              <span>1 лицензия = 1 проект</span>
              <span className="h-3 w-px bg-white/10" />
              <span>Все обновления</span>
            </div>
          </Reveal>

          <Reveal delay={120}>
            <DashboardPreview />
          </Reveal>
        </div>
      </section>

      {/* ================= STATS ================= */}
      <section className="border-b border-white/[0.07]">
        <div className="mx-auto max-w-6xl px-5 py-16">
          <div className="grid gap-x-8 gap-y-10 sm:grid-cols-2 lg:grid-cols-4">
            {PLATFORM_STATS.map((s, i) => (
              <Reveal key={s.key} delay={i * 70}>
                <div className="border-l border-white/12 pl-4">
                  <div className="text-[2.1rem] font-semibold tracking-tight">
                    <CountUp value={s.value} decimals={s.suffix === '%' ? 2 : 0} />
                    {s.suffix}
                  </div>
                  <div className="mt-1 text-[13px] text-white/50">{s.label}</div>
                  <div className="mt-0.5 font-mono text-[11px]" style={{ color: PINK }}>
                    {s.delta}
                  </div>
                </div>
              </Reveal>
            ))}
          </div>

          <div className="mt-14 grid gap-5 lg:grid-cols-3">
            {[
              { t: 'Онлайн за неделю', d: SERIES.onlineWeek, labels: WEEK_LABELS, kind: 'bars' as const },
              { t: 'Рост проектов', d: SERIES.projectGrowth, kind: 'area' as const },
              { t: 'Онлайн за месяц', d: SERIES.onlineMonth, kind: 'area' as const },
            ].map((c, i) => (
              <Reveal key={c.t} delay={i * 80}>
                <div className="rounded-xl border border-white/[0.08] bg-white/[0.015] p-5 text-white/70">
                  <div className="mb-3 flex items-center justify-between">
                    <span className="text-[13px] text-white/60">{c.t}</span>
                    <ArrowUpRight className="h-3.5 w-3.5 text-white/25" />
                  </div>
                  {c.kind === 'bars' ? (
                    <Bars data={c.d} labels={c.labels} color={PINK} height={104} />
                  ) : (
                    <AreaChart data={c.d} color={PINK} height={120} />
                  )}
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ================= PROJECTS ================= */}
      <section id="projects" className="border-b border-white/[0.07]">
        <div className="mx-auto max-w-6xl px-5 py-16">
          <Reveal>
            <div className="flex items-end justify-between">
              <div>
                <h2 className="text-2xl font-semibold tracking-tight">Проекты на FloV:MP</h2>
                <p className="mt-2 text-[14px] text-white/50">Каждый проект объединяет несколько серверов.</p>
              </div>
              <a href="/concepts/saas/app" className="hidden items-center gap-1 text-[13px] text-white/50 hover:text-white sm:flex">
                Открыть дашборд <ChevronRight className="h-4 w-4" />
              </a>
            </div>
          </Reveal>
          <div className="mt-8 divide-y divide-white/[0.06] overflow-hidden rounded-xl border border-white/[0.08]">
            {PROJECTS.map((p, i) => (
              <Reveal key={p.name} delay={i * 60}>
                <div className="flex items-center gap-4 bg-white/[0.012] px-5 py-4 transition hover:bg-white/[0.03]">
                  <div className="grid h-9 w-9 place-items-center rounded-lg border border-white/10 bg-white/[0.04] text-[11px] font-bold text-white/70">
                    {p.name.slice(0, 2)}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 text-[14px] font-medium">
                      {p.name}
                      <span className="rounded border border-white/10 px-1.5 py-0.5 font-mono text-[10px] text-white/40">
                        {p.tag}
                      </span>
                    </div>
                    <div className="mt-0.5 font-mono text-[11px] text-white/35">{p.servers} серверов</div>
                  </div>
                  <div className="hidden text-right sm:block">
                    <div className="text-[13px] font-medium tabular-nums">{p.online.toLocaleString('ru-RU')}</div>
                    <div className="font-mono text-[10px] text-white/35">онлайн · пик {p.peak.toLocaleString('ru-RU')}</div>
                  </div>
                  <span
                    className="inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[11px]"
                    style={{
                      borderColor: p.status === 'online' ? 'rgba(63,185,132,0.3)' : 'rgba(216,161,58,0.3)',
                      color: p.status === 'online' ? '#3fb984' : '#d8a13a',
                    }}
                  >
                    <Circle className="h-1.5 w-1.5 fill-current" />
                    {p.status === 'online' ? 'online' : 'maintenance'}
                  </span>
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ================= FEATURES ================= */}
      <section id="features" className="border-b border-white/[0.07]">
        <div className="mx-auto max-w-6xl px-5 py-16">
          <Reveal>
            <h2 className="text-2xl font-semibold tracking-tight">Что входит в платформу</h2>
          </Reveal>
          <div className="mt-8 grid gap-px overflow-hidden rounded-xl border border-white/[0.08] bg-white/[0.06] sm:grid-cols-2 lg:grid-cols-4">
            {FEATURES.map((f, i) => (
              <Reveal key={f.title} delay={i * 40}>
                <div className="h-full bg-[#0a0a0c] p-5">
                  <div className="grid h-8 w-8 place-items-center rounded-lg border border-white/10 bg-white/[0.03]">
                    {[Boxes, LayoutDashboard, Command, Radio, Database, Search, Server, Gauge][i % 8] &&
                      React.createElement([Boxes, LayoutDashboard, Command, Radio, Database, Search, Server, Gauge][i % 8], {
                        className: 'h-4 w-4 text-white/70',
                      })}
                  </div>
                  <div className="mt-4 text-[14px] font-medium">{f.title}</div>
                  <p className="mt-1.5 text-[12.5px] leading-relaxed text-white/45">{f.desc}</p>
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ================= OWNERSHIP / QUICK START ================= */}
      <section id="ownership" className="border-b border-white/[0.07]">
        <div className="mx-auto max-w-6xl px-5 py-16">
          <Reveal>
            <h2 className="text-2xl font-semibold tracking-tight">Полный контроль над сервером</h2>
            <p className="mt-2 max-w-2xl text-[14px] leading-relaxed text-white/50">
              FloV:MP даёт владельцу 100% контроль: никаких скрытых блокировок и навязанного лора. Любая
              карта — реальная Москва, Дикий Запад XIX века или кастомные острова.
            </p>
          </Reveal>

          <div className="mt-8 grid gap-4 lg:grid-cols-2">
            {[
              {
                icon: FileCode2,
                t: '«Чистый холст» — Blank C# SDK',
                d: 'Пустой проект на C# .NET 8 для кастомных механик со своей уникальной архитектурой.',
              },
              {
                icon: Boxes,
                t: '«RP Стартер» — готовая база',
                d: 'Экономика, админ-система, инвентарь и дименшены интерьеров из коробки.',
              },
            ].map((c, i) => (
              <Reveal key={c.t} delay={i * 70}>
                <div className="h-full rounded-xl border border-white/[0.08] bg-white/[0.015] p-5">
                  <div className="grid h-8 w-8 place-items-center rounded-lg border border-white/10 bg-white/[0.03]">
                    <c.icon className="h-4 w-4 text-white/70" />
                  </div>
                  <div className="mt-4 text-[14px] font-medium">{c.t}</div>
                  <p className="mt-1.5 text-[12.5px] leading-relaxed text-white/45">{c.d}</p>
                </div>
              </Reveal>
            ))}
          </div>

          <div className="mt-4 grid gap-4 lg:grid-cols-[1fr_1fr]">
            <Reveal>
              <div className="h-full rounded-xl border border-white/[0.08] bg-white/[0.015] p-5">
                <div className="flex items-center gap-2 text-[13px] font-medium">
                  <Terminal className="h-4 w-4" style={{ color: PINK }} />
                  Команды администратора из коробки
                </div>
                <div className="mt-4 space-y-1.5 font-mono text-[12px]">
                  {[
                    ['/o <текст>', 'глобальное оповещение всех игроков'],
                    ['/pos', 'координаты X, Y, Z для маппинга'],
                    ['/setdim <ник> <мир>', '0 — улица, 999 — админ-тюрьма, 10000+ — дома'],
                    ['/veh <модель>', 'спавн любого транспорта GTA V'],
                    ['/stats', 'сводка об игроке, пинге и железе'],
                  ].map(([cmd, desc]) => (
                    <div key={cmd} className="flex flex-wrap items-baseline gap-x-2">
                      <span className="text-white/80">{cmd}</span>
                      <span className="text-white/35">— {desc}</span>
                    </div>
                  ))}
                </div>
                <div className="mt-4 flex flex-wrap gap-x-4 gap-y-1 border-t border-white/[0.06] pt-3 font-mono text-[11px] text-white/35">
                  <span>UDP 7788 · alt:V протокол</span>
                  <span>Консоль: F8 в клиенте / веб-консоль</span>
                </div>
              </div>
            </Reveal>
            <Reveal delay={80}>
              <div className="h-full rounded-xl border border-white/[0.08] bg-white/[0.015] p-5">
                <div className="flex items-center gap-2 text-[13px] font-medium">
                  <ShieldBan className="h-4 w-4" style={{ color: PINK }} />
                  Многоуровневая система банов
                </div>
                <div className="mt-4 space-y-1.5 font-mono text-[12px]">
                  {[
                    ['/ban', 'бан аккаунта'],
                    ['/banip', 'бан по IP'],
                    ['/bansc', 'бан лицензии Rockstar Social Club'],
                    ['/hwidban', 'бан по железу ПК (FloV:ID)'],
                    ['/hardban', 'тотальный: Account + IP + SC + HWID + MAC'],
                    ['/unban', 'универсальная разблокировка'],
                  ].map(([cmd, desc]) => (
                    <div key={cmd} className="flex flex-wrap items-baseline gap-x-2">
                      <span className="text-white/80">{cmd}</span>
                      <span className="text-white/35">— {desc}</span>
                    </div>
                  ))}
                </div>
              </div>
            </Reveal>
          </div>
        </div>
      </section>

      {/* ================= ROADMAP ================= */}
      <section id="roadmap" className="border-b border-white/[0.07]">
        <div className="mx-auto max-w-6xl px-5 py-16">
          <Reveal>
            <h2 className="text-2xl font-semibold tracking-tight">Roadmap</h2>
          </Reveal>
          <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {ROADMAP.map((r, i) => (
              <Reveal key={r.q} delay={i * 60}>
                <div className={`rounded-xl border p-4 ${r.state === 'active' ? 'border-white/20 bg-white/[0.03]' : 'border-white/[0.08]'}`}>
                  <div className="flex items-center justify-between">
                    <span className="font-mono text-[11px] text-white/40">{r.q}</span>
                    {r.state === 'done' && <Check className="h-3.5 w-3.5" style={{ color: PINK }} />}
                    {r.state === 'active' && <span className="rounded-full px-1.5 py-0.5 text-[9px] font-bold text-black" style={{ background: PINK }}>NOW</span>}
                  </div>
                  <div className="mt-2 text-[14px] font-medium">{r.title}</div>
                  <ul className="mt-2 space-y-1">
                    {r.items.map((it) => (
                      <li key={it} className="text-[12px] text-white/45">
                        {it}
                      </li>
                    ))}
                  </ul>
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ================= PRICING ================= */}
      <section id="pricing" className="border-b border-white/[0.07]">
        <div className="mx-auto max-w-6xl px-5 py-20">
          <Reveal>
            <div className="mx-auto max-w-md text-center">
              <h2 className="text-2xl font-semibold tracking-tight">Одна лицензия. Навсегда.</h2>
              <p className="mt-2 text-[14px] text-white/50">{LICENSE.note}</p>
            </div>
          </Reveal>
          <Reveal delay={100}>
            <div className="mx-auto mt-10 max-w-md overflow-hidden rounded-2xl border border-white/12 bg-white/[0.02]">
              <div className="border-b border-white/[0.08] p-7">
                <div className="text-[13px] font-medium text-white/55">{LICENSE.name}</div>
                <div className="mt-2 flex items-end gap-2">
                  <span className="text-[3rem] font-semibold leading-none tracking-tight">{LICENSE.price}</span>
                  <span className="pb-1.5 text-[13px] text-white/40">разово</span>
                </div>
              </div>
              <div className="space-y-2.5 p-7">
                {LICENSE.includes.map((x) => (
                  <div key={x} className="flex items-start gap-2.5 text-[13px] text-white/70">
                    <Check className="mt-0.5 h-4 w-4 flex-none" style={{ color: PINK }} />
                    {x}
                  </div>
                ))}
                <a
                  href={TG}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-4 flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-[13px] font-semibold text-black"
                  style={{ background: PINK }}
                >
                  Buy License <ArrowRight className="h-4 w-4" />
                </a>
              </div>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ================= DASHBOARD SCREEN ================= */}
      <section className="bg-[#0a0a0c]">
        <div className="mx-auto max-w-6xl px-5 py-16">
          <Reveal>
            <p className="font-mono text-[11px] uppercase tracking-[0.24em] text-white/35">Dashboard preview</p>
            <h2 className="mt-2 text-2xl font-semibold tracking-tight">Экран управления</h2>
          </Reveal>

          <Reveal delay={120}>
            <div className="mt-8 overflow-hidden rounded-2xl border border-white/[0.09] bg-[#08080a]">
              <div className="flex">
                {/* sidebar */}
                <aside className="hidden w-52 flex-none border-r border-white/[0.07] p-3 lg:block">
                  <div className="flex items-center gap-2 px-2 py-2">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img src="/branding/logo.png" alt="" className="h-5 w-5 rounded" />
                    <span className="text-[13px] font-semibold">FloV:MP</span>
                  </div>
                  <div className="mt-3 space-y-0.5">
                    {SIDEBAR.map((s) => (
                      <button
                        key={s}
                        onClick={() => setActive(s)}
                        className={`flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-left text-[12.5px] transition ${
                          active === s ? 'bg-white/[0.06] text-white' : 'text-white/45 hover:text-white/80'
                        }`}
                      >
                        <span className="h-1 w-1 rounded-full" style={{ background: active === s ? PINK : 'transparent' }} />
                        {s}
                      </button>
                    ))}
                  </div>
                </aside>

                {/* main */}
                <div className="min-w-0 flex-1">
                  <div className="flex items-center justify-between border-b border-white/[0.07] px-5 py-3">
                    <div className="flex items-center gap-2 text-[13px] text-white/50">
                      <span className="text-white/80">Florida V</span>
                      <ChevronRight className="h-3.5 w-3.5" />
                      <span>{active}</span>
                    </div>
                    <div className="flex items-center gap-2 rounded-lg border border-white/10 px-2 py-1 text-[11px] text-white/35">
                      <Command className="h-3 w-3" /> K
                    </div>
                  </div>

                  <div className="p-5">
                    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                      {[
                        { icon: Radio, label: 'Игроков онлайн', value: '9 340', sub: 'пик 12 480' },
                        { icon: Cpu, label: 'CPU (avg)', value: '54%', sub: '4 сервера' },
                        { icon: MemoryStick, label: 'RAM (avg)', value: '61%', sub: '128 / 210 GB' },
                        { icon: Timer, label: 'Uptime', value: '99.98%', sub: '90 дней' },
                      ].map((k) => (
                        <div key={k.label} className="rounded-xl border border-white/[0.08] p-4">
                          <k.icon className="h-4 w-4 text-white/35" />
                          <div className="mt-3 text-[1.5rem] font-semibold tracking-tight">{k.value}</div>
                          <div className="text-[12px] text-white/45">{k.label}</div>
                          <div className="font-mono text-[10px] text-white/30">{k.sub}</div>
                        </div>
                      ))}
                    </div>

                    <div className="mt-4 grid gap-4 lg:grid-cols-[1.4fr_1fr]">
                      <div className="rounded-xl border border-white/[0.08] p-4 text-white/70">
                        <div className="mb-3 flex items-center justify-between text-[12px] text-white/50">
                          <span>Онлайн за месяц</span>
                          <span className="font-mono text-white/30">{MONTH_LABELS.join(' ')}</span>
                        </div>
                        <AreaChart data={SERIES.onlineMonth} color={PINK} height={150} />
                      </div>
                      <div className="rounded-xl border border-white/[0.08] p-4">
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

                    <div className="mt-4 overflow-hidden rounded-xl border border-white/[0.08]">
                      <div className="border-b border-white/[0.07] px-4 py-2.5 text-[12px] text-white/50">Серверы проекта</div>
                      <table className="w-full text-left text-[12px]">
                        <tbody className="divide-y divide-white/[0.05]">
                          {SERVERS.map((s) => (
                            <tr key={s.name} className="text-white/60">
                              <td className="px-4 py-2.5 font-mono text-white/80">{s.name}</td>
                              <td className="px-4 py-2.5 text-white/35">{s.env}</td>
                              <td className="px-4 py-2.5 tabular-nums">{s.online} / {s.slots}</td>
                              <td className="px-4 py-2.5 tabular-nums">CPU {s.cpu}%</td>
                              <td className="px-4 py-2.5 tabular-nums">RAM {s.ram}%</td>
                              <td className="px-4 py-2.5">
                                <span
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
                </div>
              </div>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ================= FOOTER ================= */}
      <footer className="border-t border-white/[0.07]">
        <div className="mx-auto flex max-w-6xl flex-col gap-3 px-5 py-10 text-[12px] text-white/35 sm:flex-row sm:items-center sm:justify-between">
          <span>© 2026 FloV:MP · Premium SaaS concept</span>
          <span className="font-mono">Vercel · Linear · Stripe direction</span>
        </div>
      </footer>

      <ConceptSwitch current="saas" />
      <div className="h-16" />
    </div>
  );
}

/* -------- hero dashboard preview (static) -------- */
function DashboardPreview() {
  return (
    <div className="overflow-hidden rounded-2xl border border-white/[0.09] bg-[#0a0a0c] shadow-[0_40px_80px_-30px_rgba(0,0,0,0.7)]">
      <div className="flex items-center gap-1.5 border-b border-white/[0.07] px-4 py-2.5">
        <span className="h-2.5 w-2.5 rounded-full bg-white/15" />
        <span className="h-2.5 w-2.5 rounded-full bg-white/15" />
        <span className="h-2.5 w-2.5 rounded-full bg-white/15" />
        <span className="ml-3 font-mono text-[10px] text-white/30">app.flovmp.dev/florida-v</span>
      </div>
      <div className="p-4">
        <div className="grid grid-cols-3 gap-3">
          {[
            ['Онлайн', '9 340'],
            ['CPU', '54%'],
            ['Uptime', '99.98%'],
          ].map(([l, v]) => (
            <div key={l} className="rounded-lg border border-white/[0.08] p-3">
              <div className="text-[15px] font-semibold">{v}</div>
              <div className="text-[10px] text-white/40">{l}</div>
            </div>
          ))}
        </div>
        <div className="mt-3 rounded-lg border border-white/[0.08] p-3 text-white/60">
          <AreaChart data={SERIES.onlineWeek} color={PINK} height={92} />
        </div>
        <div className="mt-3 space-y-1.5">
          {ACTIVITY.slice(0, 3).map((a) => (
            <div key={a.time} className="flex gap-2 text-[10.5px]">
              <span className="font-mono text-white/25">{a.time}</span>
              <span className="text-white/45">{a.text}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
