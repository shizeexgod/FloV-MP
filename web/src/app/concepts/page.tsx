'use client';

import React from 'react';
import { Reveal } from './_ui';

const CONCEPTS = [
  {
    id: 'saas',
    n: '01',
    name: 'Premium SaaS',
    ref: 'Vercel · Linear · Stripe',
    goal: 'Выглядит как премиальная софтверная компания. Минимализм, воздух, сильная типографика, розовый акцент дозированно.',
    bg: 'linear-gradient(180deg,#08080a,#0d0d10)',
    swatch: ['#08080a', '#e9eaee', '#ff1493'],
    edge: 'rgba(255,255,255,0.10)',
  },
  {
    id: 'gta',
    n: '02',
    name: 'GTA Platform',
    ref: 'RAGE:MP · alt:V',
    goal: 'Будущее GTA V мультиплеера. Атмосфера, витрина проектов, комьюнити, серверный ops-дашборд с живой консолью.',
    bg: 'linear-gradient(180deg,#0b0b0f,#120a14)',
    swatch: ['#0f0f14', '#ff007f', '#8b5cf6'],
    edge: 'rgba(255,0,127,0.35)',
  },
  {
    id: 'hybrid',
    n: '03',
    name: 'Hybrid',
    ref: 'SaaS × Gaming',
    goal: 'Баланс: стеклянные слои, мягкие градиенты pink→purple, современный стартап-дашборд с аналитикой.',
    bg: 'linear-gradient(180deg,#0c0c12,#141018)',
    swatch: ['#13131a', '#ff1493', '#a855f7'],
    edge: 'rgba(168,85,247,0.35)',
  },
];

export default function ConceptsIndex() {
  return (
    <main className="min-h-screen bg-[#08080a] px-5 py-16 text-white sm:px-8 sm:py-24">
      <div className="mx-auto max-w-5xl">
        <Reveal>
          <p className="font-mono text-[11px] font-semibold uppercase tracking-[0.28em] text-[#ff1493]">
            FloV:MP · Design exploration
          </p>
          <h1 className="mt-4 text-4xl font-bold leading-[1.05] tracking-tight sm:text-6xl">
            Три направления
            <br />
            для платформы
          </h1>
          <p className="mt-5 max-w-xl text-[15px] leading-relaxed text-white/55">
            Публичный сайт и дашборд FloV:MP в трёх разных визуальных языках. Каждый концепт — отдельный
            прототип: лендинг + один экран управления. Выберите направление — доведём его до глубины.
          </p>
        </Reveal>

        <div className="mt-14 grid gap-5 sm:grid-cols-3">
          {CONCEPTS.map((c, i) => (
            <Reveal key={c.id} delay={i * 90}>
              <a
                href={`/concepts/${c.id}`}
                className="group flex h-full flex-col overflow-hidden rounded-2xl border border-white/10 transition hover:border-white/25"
                style={{ background: c.bg }}
              >
                <div className="relative h-40 overflow-hidden border-b border-white/10">
                  <div className="absolute inset-0" style={{ background: c.bg }} />
                  <div
                    className="absolute -right-10 -top-10 h-40 w-40 rounded-full blur-2xl transition-transform duration-500 group-hover:scale-125"
                    style={{ background: c.edge }}
                  />
                  <div className="absolute left-5 top-5 font-mono text-4xl font-bold text-white/12">{c.n}</div>
                  <div className="absolute bottom-4 left-5 flex gap-1.5">
                    {c.swatch.map((s) => (
                      <span key={s} className="h-5 w-5 rounded-md border border-white/15" style={{ background: s }} />
                    ))}
                  </div>
                </div>
                <div className="flex flex-1 flex-col p-5">
                  <div className="text-lg font-semibold">{c.name}</div>
                  <div className="mt-1 font-mono text-[11px] uppercase tracking-wider text-white/40">{c.ref}</div>
                  <p className="mt-3 flex-1 text-[13px] leading-relaxed text-white/55">{c.goal}</p>
                  <div className="mt-4 inline-flex items-center gap-1.5 text-[13px] font-semibold text-white/80 transition group-hover:gap-2.5 group-hover:text-white">
                    Открыть концепт →
                  </div>
                </div>
              </a>
            </Reveal>
          ))}
        </div>

        <p className="mt-12 font-mono text-[11px] text-white/30">
          Прототипы. Данные иллюстративные, не подключены к API. Продакшн-сайт (/) не затронут.
        </p>
      </div>
    </main>
  );
}
