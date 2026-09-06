'use client';

import React, { useState } from 'react';
import {
  ArrowRight,
  ArrowUpRight,
  BarChart3,
  Boxes,
  Check,
  ChevronRight,
  Cpu,
  Database,
  LayoutDashboard,
  MemoryStick,
  Radio,
  Server,
  Sparkles,
  Timer,
  Users,
  Wifi,
} from 'lucide-react';
import {
  ACTIVITY,
  FEATURES,
  LICENSE,
  PLATFORM_STATS,
  PROJECTS,
  ROADMAP,
  SERIES,
  SERVERS,
  SIDEBAR,
  WEEK_LABELS,
} from '../_data';
import { AreaChart, Bars, ConceptSwitch, CountUp, Donut, LineChart, Reveal } from '../_ui';

const PINK = '#ff1493';
const PURPLE = '#a855f7';
const NAV = ['Home', 'Features', 'Documentation', 'Projects', 'Roadmap', 'Pricing'];

export default function HybridConcept() {
  const [active, setActive] = useState('Dashboard');

  return (
    <div className="min-h-screen bg-[#0c0c12] font-sans text-[#e9eaee] antialiased">
      <style
        dangerouslySetInnerHTML={{
          __html: `
          .hy-card{background:linear-gradient(180deg,rgba(255,255,255,.045),rgba(255,255,255,.015));border:1px solid rgba(255,255,255,.09);backdrop-filter:blur(18px) saturate(140%)}
          .hy-grad{background:linear-gradient(115deg,${PINK},${PURPLE})}
          .hy-grad-border{position:relative}
          .hy-grad-border::before{content:'';position:absolute;inset:0;border-radius:inherit;padding:1px;background:linear-gradient(130deg,rgba(255,20,147,.6),rgba(168,85,247,.5),transparent 70%);-webkit-mask:linear-gradient(#000 0 0) content-box,linear-gradient(#000 0 0);-webkit-mask-composite:xor;mask-composite:exclude;pointer-events:none}
        `,
        }}
      />

      {/* ambient */}
      <div className="pointer-events-none fixed inset-0 -z-10">
        <div className="absolute left-[10%] top-[-10%] h-[440px] w-[440px] rounded-full opacity-25 blur-[130px]" style={{ background: PINK }} />
        <div className="absolute right-[5%] top-[20%] h-[420px] w-[420px] rounded-full opacity-20 blur-[140px]" style={{ background: PURPLE }} />
      </div>

      {/* ================= NAV ================= */}
      <header className="sticky top-0 z-50 px-4 pt-4">
        <div className="mx-auto flex max-w-6xl items-center justify-between rounded-2xl border border-white/10 bg-white/[0.04] px-4 py-2.5 backdrop-blur-xl">
          <div className="flex items-center gap-2.5">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/branding/logo.jpg" alt="" className="h-7 w-7 rounded-lg" />
            <span className="text-[15px] font-semibold tracking-tight">FloV:MP</span>
          </div>
          <nav className="hidden items-center gap-6 md:flex">
            {NAV.map((n) => (
              <a key={n} href="#" className="text-[13px] text-white/55 transition hover:text-white">
                {n}
              </a>
            ))}
          </nav>
          <div className="flex items-center gap-2">
            <a href="/concepts/hybrid/app" className="px-2 text-[13px] text-white/55 hover:text-white">
              Login
            </a>
            <a href="/concepts/hybrid/app" className="hy-grad rounded-xl px-3.5 py-1.5 text-[13px] font-semibold text-white shadow-[0_8px_24px_-8px_rgba(255,20,147,0.5)]">
              Open Dashboard
            </a>
          </div>
        </div>
      </header>

      {/* ================= HERO ================= */}
      <section className="mx-auto max-w-6xl px-5 py-20 lg:py-28">
        <div className="grid items-center gap-14 lg:grid-cols-[1.05fr_1fr]">
          <Reveal>
            <div className="inline-flex items-center gap-2 rounded-full border border-white/12 bg-white/[0.04] px-3 py-1 text-[11px] text-white/65 backdrop-blur">
              <Sparkles className="h-3.5 w-3.5" style={{ color: PINK }} />
              SaaS × Gaming platform · Lifetime license
            </div>
            <h1 className="mt-6 text-[2.7rem] font-bold leading-[1.03] tracking-[-0.03em] sm:text-[3.6rem]">
              Own Your GTA&nbsp;V
              <br />
              <span className="bg-gradient-to-r from-[#ff1493] via-[#c942c9] to-[#a855f7] bg-clip-text text-transparent">
                Project Infrastructure
              </span>
            </h1>
            <p className="mt-5 max-w-lg text-[15px] leading-relaxed text-white/60">
              Единая платформа для запуска и управления GTA V проектами: мультиплеер, дашборд, SDK,
              API и интеграции. Аккаунт → проекты → серверы.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <a href="#pricing" className="hy-grad inline-flex items-center gap-2 rounded-xl px-5 py-3 text-[13px] font-semibold text-white shadow-[0_14px_36px_-10px_rgba(255,20,147,0.55)]">
                Buy Lifetime License <ArrowRight className="h-4 w-4" />
              </a>
              <a href="#" className="inline-flex items-center gap-2 rounded-xl border border-white/12 bg-white/[0.04] px-5 py-3 text-[13px] font-semibold text-white/85 backdrop-blur transition hover:bg-white/[0.08]">
                Documentation
              </a>
            </div>
          </Reveal>

          <Reveal delay={130}>
            <div className="hy-grad-border rounded-3xl">
              <div className="hy-card overflow-hidden rounded-3xl p-4 shadow-[0_40px_90px_-40px_rgba(0,0,0,0.8)]">
                <div className="flex items-center justify-between px-1 pb-3 text-[11px] text-white/40">
                  <span className="font-mono">florida-v · overview</span>
                  <span className="flex items-center gap-1"><span className="h-1.5 w-1.5 rounded-full bg-[#3fb984]" /> live</span>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                    <Donut value={0.61} size={92} stroke={9} color={PINK}>
                      <span className="text-[13px] font-bold">61%</span>
                    </Donut>
                    <div className="mt-2 text-[11px] text-white/45">CPU нагрузка</div>
                  </div>
                  <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                    <div className="text-[1.6rem] font-bold tracking-tight">9 340</div>
                    <div className="text-[11px] text-white/45">игроков онлайн</div>
                    <div className="mt-3">
                      <AreaChart data={SERIES.onlineWeek} color={PURPLE} height={54} grid={false} />
                    </div>
                  </div>
                </div>
                <div className="mt-3 rounded-2xl border border-white/10 bg-white/[0.03] p-4 text-white/70">
                  <div className="mb-2 text-[11px] text-white/45">Онлайн · 12 месяцев</div>
                  <AreaChart data={SERIES.onlineMonth} color={PINK} height={96} />
                </div>
              </div>
            </div>
          </Reveal>
        </div>
      </section>

      {/* ================= STATS ================= */}
      <section className="mx-auto max-w-6xl px-5 py-10">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {PLATFORM_STATS.map((s, i) => (
            <Reveal key={s.key} delay={i * 70}>
              <div className="hy-card rounded-2xl p-5">
                <div className="flex items-center justify-between">
                  <span className="text-[12px] text-white/50">{s.label}</span>
                  {[Boxes, Server, Users, Timer][i] &&
                    React.createElement([Boxes, Server, Users, Timer][i], { className: 'h-4 w-4', style: { color: PURPLE } })}
                </div>
                <div className="mt-3 text-[2rem] font-bold tracking-tight">
                  <CountUp value={s.value} decimals={s.suffix === '%' ? 2 : 0} />
                  {s.suffix}
                </div>
                <div className="mt-1 text-[11px]" style={{ color: PINK }}>
                  {s.delta}
                </div>
              </div>
            </Reveal>
          ))}
        </div>

        <div className="mt-6 grid gap-4 lg:grid-cols-3">
          {[
            { t: 'Онлайн за неделю', kind: 'bars' as const },
            { t: 'Рост проектов', kind: 'area' as const },
            { t: 'Сетевой трафик', kind: 'line' as const },
          ].map((c, i) => (
            <Reveal key={c.t} delay={i * 80}>
              <div className="hy-card rounded-2xl p-5 text-white/70">
                <div className="mb-3 flex items-center justify-between">
                  <span className="text-[13px] text-white/60">{c.t}</span>
                  <BarChart3 className="h-3.5 w-3.5 text-white/25" />
                </div>
                {c.kind === 'bars' && <Bars data={SERIES.onlineWeek} labels={WEEK_LABELS} color={PINK} height={110} />}
                {c.kind === 'area' && <AreaChart data={SERIES.projectGrowth} color={PURPLE} height={126} />}
                {c.kind === 'line' && (
                  <LineChart series={[{ data: SERIES.net, color: PINK }, { data: SERIES.cpu, color: PURPLE }]} height={126} />
                )}
              </div>
            </Reveal>
          ))}
        </div>
      </section>

      {/* ================= PROJECTS ================= */}
      <section className="mx-auto max-w-6xl px-5 py-16">
        <Reveal>
          <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Проекты на FloV:MP</h2>
          <p className="mt-2 text-[14px] text-white/50">Один проект — несколько серверов под одной лицензией.</p>
        </Reveal>
        <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {PROJECTS.map((p, i) => (
            <Reveal key={p.name} delay={i * 60}>
              <div className="hy-card group overflow-hidden rounded-2xl">
                <div className="relative h-24 overflow-hidden">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={p.banner} alt="" className="h-full w-full object-cover opacity-60 transition group-hover:scale-105 group-hover:opacity-80" />
                  <div className="absolute inset-0" style={{ background: `linear-gradient(180deg,transparent,rgba(12,12,18,.9)), radial-gradient(120px 60px at 20% 0%, ${p.accent}44, transparent)` }} />
                </div>
                <div className="p-4">
                  <div className="flex items-center justify-between">
                    <span className="text-[14px] font-semibold">{p.name}</span>
                    <span
                      className="h-2 w-2 rounded-full"
                      style={{ background: p.status === 'online' ? '#3fb984' : '#d8a13a' }}
                    />
                  </div>
                  <div className="mt-0.5 text-[11px] text-white/40">{p.tag}</div>
                  <div className="mt-3 flex items-end justify-between">
                    <div>
                      <div className="text-[1.1rem] font-bold" style={{ color: p.accent }}>
                        {p.online.toLocaleString('ru-RU')}
                      </div>
                      <div className="text-[10px] text-white/35">онлайн</div>
                    </div>
                    <div className="text-right text-[10px] text-white/35">{p.servers} серв.</div>
                  </div>
                </div>
              </div>
            </Reveal>
          ))}
        </div>
      </section>

      {/* ================= FEATURES ================= */}
      <section className="mx-auto max-w-6xl px-5 py-16">
        <Reveal>
          <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Что входит в платформу</h2>
        </Reveal>
        <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {FEATURES.map((f, i) => (
            <Reveal key={f.title} delay={i * 40}>
              <div className="hy-card h-full rounded-2xl p-5">
                <div className="grid h-9 w-9 place-items-center rounded-xl" style={{ background: `linear-gradient(135deg, ${PINK}33, ${PURPLE}33)`, border: '1px solid rgba(255,255,255,.1)' }}>
                  {React.createElement([Boxes, LayoutDashboard, Cpu, Wifi, Radio, Database, Server, Users][i % 8], {
                    className: 'h-4 w-4 text-white/85',
                  })}
                </div>
                <div className="mt-4 text-[14px] font-semibold">{f.title}</div>
                <p className="mt-1.5 text-[12.5px] leading-relaxed text-white/45">{f.desc}</p>
              </div>
            </Reveal>
          ))}
        </div>
      </section>

      {/* ================= ROADMAP ================= */}
      <section className="mx-auto max-w-6xl px-5 py-16">
        <Reveal>
          <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Roadmap</h2>
        </Reveal>
        <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {ROADMAP.map((r, i) => (
            <Reveal key={r.q} delay={i * 60}>
              <div className={`hy-card rounded-2xl p-5 ${r.state === 'active' ? 'hy-grad-border' : ''}`}>
                <div className="flex items-center justify-between">
                  <span className="font-mono text-[11px] text-white/45">{r.q}</span>
                  {r.state === 'done' && <Check className="h-4 w-4" style={{ color: PURPLE }} />}
                  {r.state === 'active' && (
                    <span className="hy-grad rounded-full px-2 py-0.5 text-[9px] font-bold text-white">NOW</span>
                  )}
                </div>
                <div className="mt-2 text-[14px] font-semibold">{r.title}</div>
                <ul className="mt-2 space-y-1">
                  {r.items.map((it) => (
                    <li key={it} className="flex items-center gap-1.5 text-[12px] text-white/45">
                      <span className="h-1 w-1 rounded-full bg-white/25" /> {it}
                    </li>
                  ))}
                </ul>
              </div>
            </Reveal>
          ))}
        </div>
      </section>

      {/* ================= PRICING ================= */}
      <section id="pricing" className="mx-auto max-w-6xl px-5 py-20">
        <Reveal>
          <div className="mx-auto max-w-md text-center">
            <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">Одна лицензия. Навсегда.</h2>
            <p className="mt-2 text-[14px] text-white/50">{LICENSE.note}</p>
          </div>
        </Reveal>
        <Reveal delay={100}>
          <div className="hy-grad-border mx-auto mt-10 max-w-md rounded-3xl">
            <div className="hy-card rounded-3xl p-8">
              <div className="flex items-center gap-2 text-[13px] text-white/55">
                <Sparkles className="h-4 w-4" style={{ color: PINK }} /> {LICENSE.name}
              </div>
              <div className="mt-3 flex items-end gap-2">
                <span className="bg-gradient-to-r from-[#ff1493] to-[#a855f7] bg-clip-text text-[3.2rem] font-bold leading-none text-transparent">
                  {LICENSE.price}
                </span>
                <span className="pb-1.5 text-[13px] text-white/40">разово</span>
              </div>
              <div className="mt-6 space-y-2.5">
                {LICENSE.includes.map((x) => (
                  <div key={x} className="flex items-start gap-2.5 text-[13px] text-white/70">
                    <Check className="mt-0.5 h-4 w-4 flex-none" style={{ color: PURPLE }} />
                    {x}
                  </div>
                ))}
              </div>
              <a href="#" className="hy-grad mt-7 flex items-center justify-center gap-2 rounded-xl px-5 py-3 text-[13px] font-semibold text-white shadow-[0_16px_40px_-12px_rgba(255,20,147,0.55)]">
                Buy License <ArrowRight className="h-4 w-4" />
              </a>
            </div>
          </div>
        </Reveal>
      </section>

      {/* ================= DASHBOARD SCREEN ================= */}
      <section className="mx-auto max-w-6xl px-5 py-16">
        <Reveal>
          <p className="font-mono text-[11px] uppercase tracking-[0.24em] text-white/35">Dashboard preview</p>
          <h2 className="mt-2 text-2xl font-bold tracking-tight sm:text-3xl">Экран управления</h2>
        </Reveal>

        <Reveal delay={120}>
          <div className="hy-card mt-8 overflow-hidden rounded-3xl">
            <div className="flex">
              <aside className="hidden w-52 flex-none border-r border-white/[0.08] p-3 lg:block">
                <div className="flex items-center gap-2 px-2 py-2">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src="/branding/logo.jpg" alt="" className="h-5 w-5 rounded" />
                  <span className="text-[13px] font-semibold">FloV:MP</span>
                </div>
                <div className="mt-3 space-y-0.5">
                  {SIDEBAR.map((s) => (
                    <button
                      key={s}
                      onClick={() => setActive(s)}
                      className={`flex w-full items-center gap-2.5 rounded-xl px-3 py-2 text-left text-[12.5px] transition ${
                        active === s ? 'text-white' : 'text-white/45 hover:text-white/80'
                      }`}
                      style={active === s ? { background: `linear-gradient(120deg, ${PINK}22, ${PURPLE}22)`, border: '1px solid rgba(255,255,255,.1)' } : undefined}
                    >
                      {s}
                    </button>
                  ))}
                </div>
              </aside>

              <div className="min-w-0 flex-1 p-5">
                <div className="mb-4 flex items-center gap-2 text-[13px] text-white/50">
                  <span className="text-white/85">Florida V</span>
                  <ChevronRight className="h-3.5 w-3.5" />
                  <span>{active}</span>
                </div>

                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                  {[
                    { icon: Radio, label: 'Игроков онлайн', value: '9 340', accent: PINK },
                    { icon: Cpu, label: 'CPU (avg)', value: '54%', accent: PURPLE },
                    { icon: MemoryStick, label: 'RAM (avg)', value: '61%', accent: PINK },
                    { icon: Timer, label: 'Uptime', value: '99.98%', accent: PURPLE },
                  ].map((k) => (
                    <div key={k.label} className="rounded-2xl border border-white/[0.08] bg-white/[0.02] p-4">
                      <k.icon className="h-4 w-4" style={{ color: k.accent }} />
                      <div className="mt-3 text-[1.5rem] font-bold tracking-tight">{k.value}</div>
                      <div className="text-[12px] text-white/45">{k.label}</div>
                    </div>
                  ))}
                </div>

                <div className="mt-4 grid gap-4 lg:grid-cols-[1.4fr_1fr]">
                  <div className="rounded-2xl border border-white/[0.08] bg-white/[0.02] p-4 text-white/70">
                    <div className="mb-3 flex items-center justify-between text-[12px] text-white/50">
                      <span>Онлайн · CPU · RAM</span>
                      <ArrowUpRight className="h-3.5 w-3.5 text-white/25" />
                    </div>
                    <LineChart
                      series={[
                        { data: SERIES.onlineMonth, color: PINK },
                        { data: SERIES.cpu, color: PURPLE },
                        { data: SERIES.ram, color: '#6b7280' },
                      ]}
                      height={158}
                    />
                  </div>
                  <div className="rounded-2xl border border-white/[0.08] bg-white/[0.02] p-4">
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

                <div className="mt-4 overflow-hidden rounded-2xl border border-white/[0.08]">
                  <div className="border-b border-white/[0.07] px-4 py-2.5 text-[12px] text-white/50">Серверы проекта</div>
                  <table className="w-full text-left text-[12px]">
                    <tbody className="divide-y divide-white/[0.05]">
                      {SERVERS.map((s) => (
                        <tr key={s.name} className="text-white/60">
                          <td className="px-4 py-2.5 font-mono text-white/85">{s.name}</td>
                          <td className="px-4 py-2.5 text-white/35">{s.env}</td>
                          <td className="px-4 py-2.5 tabular-nums">{s.online} / {s.slots}</td>
                          <td className="px-4 py-2.5 tabular-nums">CPU {s.cpu}%</td>
                          <td className="px-4 py-2.5 tabular-nums">RAM {s.ram}%</td>
                          <td
                            className="px-4 py-2.5"
                            style={{ color: s.status === 'online' ? '#3fb984' : s.status === 'deploying' ? '#d8a13a' : '#e5484d' }}
                          >
                            {s.status}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>
        </Reveal>
      </section>

      <footer className="mx-auto max-w-6xl px-5 py-10">
        <div className="flex flex-col gap-3 border-t border-white/10 pt-8 text-[12px] text-white/35 sm:flex-row sm:items-center sm:justify-between">
          <span>© 2026 FloV:MP · Hybrid concept</span>
          <span className="font-mono">SaaS × Gaming direction</span>
        </div>
      </footer>

      <ConceptSwitch current="hybrid" />
      <div className="h-16" />
    </div>
  );
}
