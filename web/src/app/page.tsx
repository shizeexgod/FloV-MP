'use client';

import React from 'react';
import {
  ArrowRight,
  Boxes,
  Cloud,
  Gauge,
  Layers,
  Mic,
  Plug,
  Rocket,
  ScrollText,
  ShieldCheck,
  Terminal,
} from 'lucide-react';
import { useT } from '@/lib/i18n';
import { BtnLink, Container, Reveal, Section, SectionHeading } from '@/components/site';

// Порядок соответствует t.home.features
const FEATURE_ICONS = [Gauge, Boxes, Mic, ShieldCheck, ScrollText, Cloud, Rocket, Plug];
// idx карточек, растянутых на 2 колонки в bento-сетке (lg+)
const FEATURE_SPAN2 = new Set([0, 5]);

function BrowserFrame({ src, alt, urlLabel, priority = false }: { src: string; alt: string; urlLabel: string; priority?: boolean }) {
  return (
    <div className="browser-frame">
      <div className="browser-frame__bar">
        <span className="browser-frame__dot bg-[#ff5f57]" />
        <span className="browser-frame__dot bg-[#febc2e]" />
        <span className="browser-frame__dot bg-[#28c840]" />
        <span className="ml-3 truncate font-mono text-[11px] text-white/35">{urlLabel}</span>
      </div>
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={src}
        alt={alt}
        width={1440}
        height={805}
        loading={priority ? 'eager' : 'lazy'}
        {...(priority ? { fetchPriority: 'high' as const } : {})}
      />
    </div>
  );
}

export default function HomePage() {
  const t = useT();

  return (
    <div>
      {/* ---------------- HERO ---------------- */}
      <div className="relative overflow-hidden">
        <div className="grid-bg pointer-events-none absolute inset-0 -z-10 h-[640px]" aria-hidden />
        <Container className="py-20 sm:py-28">
          <div className="grid grid-cols-1 items-center gap-14 lg:grid-cols-[1.05fr_1fr]">
            <div>
              <Reveal>
                <h1 className="max-w-xl text-[1.9rem] font-extrabold leading-[1.12] tracking-tight sm:text-[2.9rem] sm:leading-[1.08]">
                  <span className="h-grad">{t.home.h1a} </span>
                  <span className="text-brand">{t.home.h1accent}</span>
                  <span className="h-grad"> {t.home.h1b}</span>
                </h1>
              </Reveal>

              <Reveal delay={80}>
                <p className="mt-5 max-w-xl text-[15px] leading-relaxed text-white/55 sm:text-lg">{t.home.sub}</p>
              </Reveal>

              <Reveal delay={160}>
                <div className="mt-8 flex flex-col gap-3 sm:flex-row">
                  <BtnLink href="/pricing" variant="primary" arrow className="h-12 px-6 text-sm">
                    {t.home.ctaPrimary}
                  </BtnLink>
                  <BtnLink href="/docs" variant="ghost" className="h-12 px-6 text-sm">
                    <Terminal className="h-4 w-4" />
                    {t.home.ctaSecondary}
                  </BtnLink>
                </div>
              </Reveal>

              <Reveal delay={240}>
                <p className="mt-6 font-mono text-[11px] text-white/35">{t.home.trustLine}</p>
              </Reveal>
            </div>

            <Reveal delay={200}>
              <div className="relative">
                <div aria-hidden className="absolute -inset-8 -z-10 rounded-[3rem] bg-brand/15 blur-[80px] sm:-inset-12" />
                <BrowserFrame
                  src="/media/shot-dashboard-console.png"
                  alt={t.home.heroShotAlt}
                  urlLabel="flovmp.ru/dashboard"
                  priority
                />
              </div>
            </Reveal>
          </div>
        </Container>
      </div>

      {/* ---------------- КАК НАЧАТЬ ---------------- */}
      <Section>
        <SectionHeading title={t.home.flowTitle} sub={t.home.flowSub} />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          {t.home.flow.map((f, i) => (
            <Reveal key={f.n} delay={i * 70}>
              <div className="glass no-lift flex h-full items-start gap-3.5 rounded-xl p-5">
                <span className="section-num shrink-0 text-[1.2rem]">{f.n}</span>
                <div>
                  <h3 className="text-[13.5px] font-bold text-white">{f.t}</h3>
                  <p className="mt-1.5 text-[12.5px] leading-relaxed text-white/50">{f.d}</p>
                </div>
              </div>
            </Reveal>
          ))}
        </div>
      </Section>

      {/* ---------------- PILLARS (zigzag: текст — фото) ---------------- */}
      <Section>
        <SectionHeading eyebrow="01" title={t.home.pillarsTitle} />
        <div className="space-y-16 sm:space-y-24">
          {t.home.pillars.map((p, i) => (
            <div
              key={p.n}
              className={`grid grid-cols-1 items-center gap-8 lg:grid-cols-2 lg:gap-14 ${
                i % 2 === 1 ? 'lg:[&>*:first-child]:order-2' : ''
              }`}
            >
              <Reveal>
                <div className="section-num">{p.n}</div>
                <h3 className="mt-3 max-w-sm text-[1.3rem] font-extrabold leading-tight text-white sm:text-[1.55rem]">
                  {p.t}
                </h3>
                <p className="mt-3.5 max-w-sm text-[14px] leading-relaxed text-white/55">{p.d}</p>
              </Reveal>
              <Reveal delay={100}>
                <BrowserFrame src={p.img} alt={p.imgAlt} urlLabel="flovmp.ru/dashboard" />
              </Reveal>
            </div>
          ))}
        </div>
      </Section>

      {/* ---------------- FEATURES (bento) ---------------- */}
      <Section>
        <SectionHeading eyebrow="02" title={t.home.featuresTitle} sub={t.home.featuresSub} />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {t.home.features.map((f, i) => {
            const Icon = FEATURE_ICONS[i % FEATURE_ICONS.length];
            return (
              <Reveal key={f.t} delay={(i % 4) * 60} className={FEATURE_SPAN2.has(i) ? 'lg:col-span-2' : ''}>
                <div className="card card-hover h-full p-5">
                  <div className="grid h-9 w-9 place-items-center rounded-lg border border-white/10 bg-white/[0.03] text-white/70">
                    <Icon className="h-4 w-4" />
                  </div>
                  <h3 className="mt-4 text-[14px] font-semibold text-white">{f.t}</h3>
                  <p className="mt-1.5 text-[12.5px] leading-relaxed text-white/50">{f.d}</p>
                </div>
              </Reveal>
            );
          })}
        </div>
        <Reveal className="mt-8">
          <BtnLink href="/features" variant="ghost" arrow className="h-10 text-xs">
            {t.common.learnMore}
          </BtnLink>
        </Reveal>
      </Section>

      {/* ---------------- FACTS BAND ---------------- */}
      <section className="relative overflow-hidden py-16 sm:py-20">
        <div className="grid-bg pointer-events-none absolute inset-0 -z-10" aria-hidden />
        <div
          aria-hidden
          className="pointer-events-none absolute left-1/2 top-0 -z-10 h-[380px] w-[780px] -translate-x-1/2 rounded-full bg-brand/10 blur-[110px]"
        />
        <Container><hr className="rule mb-16 sm:mb-20" /></Container>
        <Container>
          <Reveal className="mb-10 text-center">
            <h2 className="text-[1.3rem] font-extrabold sm:text-[1.6rem]">
              <span className="h-grad">{t.home.factsTitle}</span>
            </h2>
          </Reveal>
          <div className="grid grid-cols-2 gap-y-8 sm:grid-cols-4">
            {t.home.facts.map(([v, l], i) => (
              <Reveal key={v} delay={i * 60} className="text-center">
                <div className="font-mono text-[1.6rem] font-bold text-white sm:text-[2rem]">{v}</div>
                <div className="mt-1.5 text-[12px] text-white/45">{l}</div>
              </Reveal>
            ))}
          </div>
        </Container>
        <Container><hr className="rule mt-16 sm:mt-20" /></Container>
      </section>

      {/* ---------------- QUICK START ---------------- */}
      <Section>
        <SectionHeading eyebrow="03" title={t.home.quickstartTitle} sub={t.home.quickstartSub} />
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Reveal>
            <div className="card h-full p-6">
              <div className="flex items-center gap-2 text-[13px] font-medium">
                <Terminal className="h-4 w-4 text-brand" />
                {t.home.quickstartCmds}
              </div>
              <div className="mt-4 space-y-2 font-mono text-[12px]">
                {t.home.cmds.map(([c, d]) => (
                  <div key={c} className="flex flex-wrap items-baseline gap-x-2">
                    <span className="text-white/80">{c}</span>
                    <span className="text-white/35">— {d}</span>
                  </div>
                ))}
              </div>
              <hr className="rule-soft mt-4" />
              <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 font-mono text-[11px] text-white/35">
                <span>{t.home.quickstartPort}: UDP 7788</span>
                <span>{t.home.quickstartConsole}: {t.home.quickstartConsoleVal}</span>
              </div>
            </div>
          </Reveal>
          <Reveal delay={80}>
            <div className="card h-full p-6">
              <div className="flex items-center gap-2 text-[13px] font-medium">
                <ShieldCheck className="h-4 w-4 text-brand" />
                {t.home.quickstartBans}
              </div>
              <div className="mt-4 space-y-2 font-mono text-[12px]">
                {t.home.bans.map(([c, d]) => (
                  <div key={c} className="flex flex-wrap items-baseline gap-x-2">
                    <span className="text-white/80">{c}</span>
                    <span className="text-white/35">— {d}</span>
                  </div>
                ))}
              </div>
            </div>
          </Reveal>
        </div>
      </Section>

      {/* ---------------- OWNERSHIP ---------------- */}
      <Section>
        <SectionHeading eyebrow="04" title={t.home.ownershipTitle} sub={t.home.ownershipSub} />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {t.home.ownership.map((o, i) => (
            <Reveal key={o.t} delay={i * 70}>
              <div className="card card-hover h-full p-6">
                <div className="grid h-9 w-9 place-items-center rounded-lg border border-white/10 bg-white/[0.03] text-white/70">
                  {i === 0 ? <Layers className="h-4 w-4" /> : <Boxes className="h-4 w-4" />}
                </div>
                <h3 className="mt-4 text-[14px] font-semibold text-white">{o.t}</h3>
                <p className="mt-1.5 text-[12.5px] leading-relaxed text-white/50">{o.d}</p>
              </div>
            </Reveal>
          ))}
        </div>
      </Section>

      {/* ---------------- CTA ---------------- */}
      <Section bordered={false}>
        <Reveal>
          <div className="rounded-2xl border border-brand/25 bg-brand/[0.05] p-10 text-center sm:p-14">
            <h2 className="mx-auto max-w-xl text-2xl font-semibold tracking-tight sm:text-3xl">{t.home.ctaTitle}</h2>
            <p className="mx-auto mt-3 max-w-lg text-[14px] leading-relaxed text-white/55">{t.home.ctaSub}</p>
            <div className="mt-7 flex flex-col items-center justify-center gap-3 sm:flex-row">
              <BtnLink href="/contact" variant="primary" className="h-12 px-6 text-sm">
                {t.common.contactSupport}
              </BtnLink>
              <BtnLink href="/auth/register" variant="ghost" className="h-12 px-6 text-sm">
                {t.common.register}
                <ArrowRight className="h-4 w-4" />
              </BtnLink>
            </div>
          </div>
        </Reveal>
      </Section>
    </div>
  );
}
