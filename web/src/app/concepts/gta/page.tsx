'use client';

import React, { useState } from 'react';
import { Activity, CircleDot, Play, Power, RotateCw, Terminal, Users } from 'lucide-react';
import {
  CONSOLE_LINES,
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
import { Bars, ConceptSwitch, CountUp, LineChart, Reveal } from '../_ui';

const PINK = '#ff007f';
const VIOLET = '#8b5cf6';
const NAV = ['Home', 'Features', 'Documentation', 'Projects', 'Roadmap', 'Pricing'];

export default function GtaConcept() {
  const [tab, setTab] = useState('Console');

  return (
    <div className="min-h-screen bg-[#0f0f14] font-sans text-[#e9eaee] antialiased">
      <style
        dangerouslySetInnerHTML={{
          __html: `
          .gta-grid{background-image:linear-gradient(rgba(255,255,255,.03) 1px,transparent 1px),linear-gradient(90deg,rgba(255,255,255,.03) 1px,transparent 1px);background-size:44px 44px}
          .gta-cta{background-image:linear-gradient(100deg,${PINK},${VIOLET})}
          .gta-tick::before,.gta-tick::after{content:'';position:absolute;width:8px;height:8px;border-color:rgba(255,255,255,.25)}
          .gta-tick::before{left:-1px;top:-1px;border-left:1px solid;border-top:1px solid}
          .gta-tick::after{right:-1px;bottom:-1px;border-right:1px solid;border-bottom:1px solid}
        `,
        }}
      />

      {/* ================= NAV ================= */}
      <header className="sticky top-0 z-50 border-b border-white/10 bg-[#0f0f14]/90 backdrop-blur-xl">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-5">
          <div className="flex items-center gap-3">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/branding/logo.jpg" alt="" className="h-7 w-7 rounded-md" />
            <span className="font-mono text-[15px] font-bold tracking-tight">
              FloV<span style={{ color: PINK }}>:MP</span>
            </span>
          </div>
          <nav className="hidden items-center gap-7 md:flex">
            {NAV.map((n) => (
              <a key={n} href="#" className="font-mono text-[12px] uppercase tracking-wider text-white/50 transition hover:text-white">
                {n}
              </a>
            ))}
          </nav>
          <div className="flex items-center gap-3">
            <a href="#" className="font-mono text-[12px] uppercase tracking-wider text-white/50 hover:text-white">
              Login
            </a>
            <a href="#pricing" className="gta-cta rounded-md px-4 py-2 text-[12px] font-bold uppercase tracking-wider text-white">
              Get License
            </a>
          </div>
        </div>
      </header>

      {/* ================= HERO ================= */}
      <section className="relative overflow-hidden border-b border-white/10">
        <div className="absolute inset-0">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/branding/hero-showcase.png" alt="" className="h-full w-full object-cover opacity-25" />
          <div className="absolute inset-0 bg-gradient-to-r from-[#0f0f14] via-[#0f0f14]/85 to-[#0f0f14]/40" />
          <div className="absolute inset-0 bg-gradient-to-t from-[#0f0f14] via-transparent to-[#0f0f14]/60" />
        </div>
        <div className="gta-grid absolute inset-0 opacity-40" />
        <div className="pointer-events-none absolute -left-20 top-10 h-72 w-72 rounded-full opacity-30 blur-[110px]" style={{ background: PINK }} />
        <div className="pointer-events-none absolute right-10 bottom-0 h-72 w-72 rounded-full opacity-25 blur-[120px]" style={{ background: VIOLET }} />

        <div className="relative mx-auto max-w-6xl px-5 py-24 lg:py-32">
          <Reveal>
            <div className="inline-flex items-center gap-2 border border-white/15 bg-black/40 px-3 py-1 font-mono text-[11px] uppercase tracking-widest text-white/60 backdrop-blur">
              <CircleDot className="h-3 w-3" style={{ color: PINK }} /> Alternative multiplayer platform
            </div>
            <h1 className="mt-6 max-w-3xl text-[2.8rem] font-extrabold uppercase leading-[0.98] tracking-tight sm:text-[4.2rem]">
              Own your GTA&nbsp;V
              <br />
              <span className="bg-gradient-to-r from-[#ff007f] to-[#8b5cf6] bg-clip-text text-transparent">
                project infrastructure
              </span>
            </h1>
            <p className="mt-6 max-w-xl text-[15px] leading-relaxed text-white/60">
              Мультиплеер, дашборд, SDK и интеграции в одной платформе. Свой сетевой стек, свои серверы,
              своя экосистема — без мастер-листов и рисков блокировки.
            </p>
            <div className="mt-9 flex flex-wrap gap-3">
              <a href="#pricing" className="gta-cta inline-flex items-center gap-2 rounded-md px-5 py-3 text-[13px] font-bold uppercase tracking-wider text-white">
                Buy Lifetime License
              </a>
              <a href="#" className="inline-flex items-center gap-2 rounded-md border border-white/15 bg-black/30 px-5 py-3 text-[13px] font-bold uppercase tracking-wider text-white/80 backdrop-blur transition hover:bg-black/50">
                Documentation
              </a>
            </div>
          </Reveal>

          <Reveal delay={140}>
            <div className="mt-16 grid max-w-3xl grid-cols-2 gap-px overflow-hidden rounded-lg border border-white/10 bg-white/10 sm:grid-cols-4">
              {PLATFORM_STATS.map((s) => (
                <div key={s.key} className="bg-[#0f0f14] px-4 py-5">
                  <div className="font-mono text-[1.7rem] font-bold" style={{ color: PINK }}>
                    <CountUp value={s.value} decimals={s.suffix === '%' ? 2 : 0} />
                    {s.suffix}
                  </div>
                  <div className="mt-1 font-mono text-[10px] uppercase tracking-wider text-white/40">{s.label}</div>
                </div>
              ))}
            </div>
          </Reveal>
        </div>
      </section>

      {/* ================= PROJECTS SHOWCASE ================= */}
      <section className="border-b border-white/10">
        <div className="mx-auto max-w-6xl px-5 py-20">
          <Reveal>
            <div className="flex items-end justify-between">
              <div>
                <div className="font-mono text-[11px] uppercase tracking-widest" style={{ color: PINK }}>
                  Live network
                </div>
                <h2 className="mt-2 text-3xl font-extrabold uppercase tracking-tight">Проекты на FloV:MP</h2>
              </div>
              <div className="font-mono text-[12px] text-white/40">{PROJECTS.length} проектов · 128 серверов</div>
            </div>
          </Reveal>

          <div className="mt-10 grid gap-5 sm:grid-cols-2">
            {PROJECTS.map((p, i) => (
              <Reveal key={p.name} delay={i * 70}>
                <div className="gta-tick group relative overflow-hidden rounded-lg border border-white/10 bg-[#13131a]">
                  <div className="relative h-36 overflow-hidden">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img src={p.banner} alt="" className="h-full w-full object-cover opacity-70 transition duration-500 group-hover:scale-105 group-hover:opacity-90" />
                    <div className="absolute inset-0 bg-gradient-to-t from-[#13131a] to-transparent" />
                    <div className="absolute left-3 top-3 flex items-center gap-1.5 border border-white/15 bg-black/60 px-2 py-0.5 font-mono text-[10px] uppercase tracking-wider backdrop-blur">
                      <span
                        className="h-1.5 w-1.5 rounded-full"
                        style={{ background: p.status === 'online' ? '#3fb984' : '#d8a13a' }}
                      />
                      {p.status}
                    </div>
                  </div>
                  <div className="flex items-center justify-between p-4">
                    <div>
                      <div className="text-[15px] font-bold">{p.name}</div>
                      <div className="font-mono text-[10px] uppercase tracking-wider text-white/40">{p.tag}</div>
                    </div>
                    <div className="text-right">
                      <div className="font-mono text-[15px] font-bold tabular-nums" style={{ color: p.accent }}>
                        {p.online.toLocaleString('ru-RU')}
                      </div>
                      <div className="font-mono text-[10px] text-white/35">онлайн</div>
                    </div>
                  </div>
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ================= FEATURES ================= */}
      <section className="border-b border-white/10">
        <div className="mx-auto max-w-6xl px-5 py-20">
          <Reveal>
            <h2 className="text-3xl font-extrabold uppercase tracking-tight">Возможности</h2>
          </Reveal>
          <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {FEATURES.map((f, i) => (
              <Reveal key={f.title} delay={i * 40}>
                <div className="gta-tick relative border border-white/10 bg-[#13131a] p-5">
                  <div className="font-mono text-[11px] text-white/25">{String(i + 1).padStart(2, '0')}</div>
                  <div className="mt-2 text-[15px] font-bold">{f.title}</div>
                  <p className="mt-2 text-[12.5px] leading-relaxed text-white/45">{f.desc}</p>
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ================= ROADMAP ================= */}
      <section className="border-b border-white/10">
        <div className="mx-auto max-w-6xl px-5 py-20">
          <Reveal>
            <h2 className="text-3xl font-extrabold uppercase tracking-tight">Roadmap</h2>
          </Reveal>
          <div className="mt-10 space-y-px overflow-hidden rounded-lg border border-white/10 bg-white/10">
            {ROADMAP.map((r, i) => (
              <Reveal key={r.q} delay={i * 60}>
                <div className="flex flex-col gap-3 bg-[#13131a] px-5 py-4 sm:flex-row sm:items-center">
                  <div className="w-24 flex-none font-mono text-[12px]" style={{ color: r.state === 'active' ? PINK : 'rgba(255,255,255,.4)' }}>
                    {r.q}
                  </div>
                  <div className="w-40 flex-none text-[14px] font-bold">{r.title}</div>
                  <div className="flex flex-wrap gap-2">
                    {r.items.map((it) => (
                      <span key={it} className="border border-white/10 px-2 py-0.5 font-mono text-[10px] text-white/45">
                        {it}
                      </span>
                    ))}
                  </div>
                  {r.state === 'done' && <span className="ml-auto font-mono text-[10px] text-[#3fb984]">SHIPPED</span>}
                  {r.state === 'active' && <span className="ml-auto font-mono text-[10px]" style={{ color: PINK }}>IN PROGRESS</span>}
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* ================= PRICING ================= */}
      <section id="pricing" className="border-b border-white/10">
        <div className="mx-auto max-w-6xl px-5 py-24">
          <div className="mx-auto max-w-lg">
            <Reveal>
              <div className="gta-tick relative border border-white/15 bg-[#13131a] p-8">
                <div className="font-mono text-[11px] uppercase tracking-widest text-white/40">{LICENSE.name}</div>
                <div className="mt-3 flex items-end gap-3">
                  <span className="font-mono text-[3.4rem] font-extrabold leading-none" style={{ color: PINK }}>
                    {LICENSE.price}
                  </span>
                  <span className="pb-2 font-mono text-[12px] text-white/40">/ one-time</span>
                </div>
                <p className="mt-3 text-[13px] text-white/50">{LICENSE.note}</p>
                <div className="my-6 h-px bg-white/10" />
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  {LICENSE.includes.map((x) => (
                    <div key={x} className="flex items-start gap-2 text-[12.5px] text-white/65">
                      <span className="mt-1 h-1.5 w-1.5 flex-none rounded-full" style={{ background: VIOLET }} />
                      {x}
                    </div>
                  ))}
                </div>
                <a href="#" className="gta-cta mt-7 flex items-center justify-center gap-2 rounded-md px-5 py-3 text-[13px] font-bold uppercase tracking-wider text-white">
                  Buy License
                </a>
              </div>
            </Reveal>
          </div>
        </div>
      </section>

      {/* ================= DASHBOARD / OPS SCREEN ================= */}
      <section className="bg-[#0b0b0f]">
        <div className="mx-auto max-w-6xl px-5 py-20">
          <Reveal>
            <div className="font-mono text-[11px] uppercase tracking-widest text-white/35">Server operations</div>
            <h2 className="mt-2 text-3xl font-extrabold uppercase tracking-tight">Управление сервером</h2>
          </Reveal>

          <Reveal delay={120}>
            <div className="mt-9 overflow-hidden rounded-lg border border-white/10 bg-[#0f0f14]">
              <div className="flex">
                <aside className="hidden w-48 flex-none border-r border-white/10 py-3 lg:block">
                  {SIDEBAR.map((s) => (
                    <button
                      key={s}
                      onClick={() => setTab(s)}
                      className={`flex w-full items-center gap-2 px-4 py-2 text-left font-mono text-[11px] uppercase tracking-wider transition ${
                        tab === s ? 'bg-white/[0.05] text-white' : 'text-white/40 hover:text-white/70'
                      }`}
                      style={tab === s ? { boxShadow: `inset 2px 0 0 ${PINK}` } : undefined}
                    >
                      {s}
                    </button>
                  ))}
                </aside>

                <div className="min-w-0 flex-1">
                  {/* control bar */}
                  <div className="flex flex-wrap items-center gap-2 border-b border-white/10 px-4 py-3">
                    <span className="font-mono text-[12px] text-white/50">florida-prod-01</span>
                    <span className="flex items-center gap-1.5 border border-[#3fb984]/30 px-2 py-0.5 font-mono text-[10px] uppercase text-[#3fb984]">
                      <span className="h-1.5 w-1.5 rounded-full bg-[#3fb984]" /> online
                    </span>
                    <div className="ml-auto flex gap-2">
                      {[
                        [Play, 'Start'],
                        [RotateCw, 'Restart'],
                        [Power, 'Stop'],
                      ].map(([Icon, label]: any) => (
                        <button
                          key={label}
                          className="inline-flex items-center gap-1.5 border border-white/12 bg-white/[0.03] px-2.5 py-1 font-mono text-[10px] uppercase tracking-wider text-white/70 transition hover:bg-white/[0.08]"
                        >
                          <Icon className="h-3 w-3" /> {label}
                        </button>
                      ))}
                    </div>
                  </div>

                  <div className="grid gap-4 p-4 lg:grid-cols-[1.3fr_1fr]">
                    {/* console */}
                    <div className="overflow-hidden rounded-md border border-white/10 bg-black/50">
                      <div className="flex items-center gap-2 border-b border-white/10 px-3 py-2 font-mono text-[11px] text-white/45">
                        <Terminal className="h-3.5 w-3.5" /> live console
                      </div>
                      <div className="max-h-[240px] space-y-1 overflow-auto p-3 font-mono text-[11px] leading-relaxed">
                        {CONSOLE_LINES.map((l, i) => (
                          <div
                            key={i}
                            className={
                              l.includes('[WARN')
                                ? 'text-[#d8a13a]'
                                : l.includes('[ OK')
                                ? 'text-[#3fb984]'
                                : 'text-white/55'
                            }
                          >
                            {l}
                          </div>
                        ))}
                        <div className="flex items-center gap-2 pt-1 text-white/70">
                          <span style={{ color: PINK }}>flovmp&gt;</span>
                          <span className="inline-block h-3.5 w-1.5 animate-pulse bg-white/50" />
                        </div>
                      </div>
                    </div>

                    {/* right column */}
                    <div className="space-y-4">
                      <div className="grid grid-cols-2 gap-3">
                        {[
                          { icon: Users, label: 'Online', value: '1 180 / 1500' },
                          { icon: Activity, label: 'Tick', value: '58.9 Hz' },
                          { icon: null, label: 'CPU', value: '61%' },
                          { icon: null, label: 'RAM', value: '68%' },
                        ].map((k) => (
                          <div key={k.label} className="border border-white/10 bg-[#13131a] p-3">
                            <div className="font-mono text-[10px] uppercase tracking-wider text-white/40">{k.label}</div>
                            <div className="mt-1 font-mono text-[1.2rem] font-bold">{k.value}</div>
                          </div>
                        ))}
                      </div>
                      <div className="border border-white/10 bg-[#13131a] p-3">
                        <div className="mb-2 font-mono text-[10px] uppercase tracking-wider text-white/40">Онлайн / неделя</div>
                        <Bars data={SERIES.onlineWeek} color={PINK} labels={WEEK_LABELS} height={96} />
                      </div>
                    </div>
                  </div>

                  {/* servers list */}
                  <div className="border-t border-white/10">
                    <div className="px-4 py-2 font-mono text-[11px] uppercase tracking-wider text-white/40">
                      Florida V · серверы
                    </div>
                    <table className="w-full text-left font-mono text-[11px]">
                      <tbody className="divide-y divide-white/[0.06]">
                        {SERVERS.map((s) => (
                          <tr key={s.name} className="text-white/55">
                            <td className="px-4 py-2.5 text-white/85">{s.name}</td>
                            <td className="px-4 py-2.5 uppercase text-white/35">{s.env}</td>
                            <td className="px-4 py-2.5 tabular-nums">{s.online}/{s.slots}</td>
                            <td className="px-4 py-2.5 tabular-nums">{s.cpu}%</td>
                            <td className="px-4 py-2.5 tabular-nums">{s.ram}%</td>
                            <td className="px-4 py-2.5 uppercase" style={{ color: s.status === 'online' ? '#3fb984' : s.status === 'deploying' ? '#d8a13a' : '#e5484d' }}>
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
        </div>
      </section>

      <footer className="border-t border-white/10">
        <div className="mx-auto flex max-w-6xl flex-col gap-3 px-5 py-10 font-mono text-[11px] uppercase tracking-wider text-white/35 sm:flex-row sm:items-center sm:justify-between">
          <span>© 2026 FloV:MP · GTA Platform concept</span>
          <span>RAGE:MP · alt:V direction</span>
        </div>
      </footer>

      <ConceptSwitch current="gta" />
      <div className="h-16" />
    </div>
  );
}
