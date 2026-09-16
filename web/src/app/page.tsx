'use client';

import React from 'react';
import {
  ArrowRight,
  Boxes,
  Cloud,
  KeyRound,
  Gauge,
  Layers,
  Mic,
  Plug,
  Rocket,
  ScrollText,
  ServerCog,
  ShieldCheck,
  Terminal,
} from 'lucide-react';
import { useT } from '@/lib/i18n';
import { BtnLink, Container, Reveal, Section, SectionHeading } from '@/components/site';

// Порядок соответствует t.home.features
const FEATURE_ICONS = [Gauge, Boxes, Mic, ShieldCheck, ScrollText, Cloud, Rocket, Plug];

function ProductPreview({ labels, mode = 'projects' }: { labels: any; mode?: 'projects' | 'console' | 'security' }) {
  const rows: string[] = mode === 'projects'
    ? [labels.production, labels.development, labels.test]
    : mode === 'console'
      ? labels.consoleRows
      : ['FloV:ID', 'HMAC-SHA256', labels.ipBinding];

  return (
    <div className="browser-frame">
      <div className="browser-frame__bar">
        <span className="browser-frame__dot bg-white/25" />
        <span className="browser-frame__dot bg-white/15" />
        <span className="browser-frame__dot bg-white/10" />
        <span className="ml-3 truncate font-mono text-[11px] text-white/35">flovmp.ru/dashboard</span>
      </div>
      <div className="grid min-h-[270px] grid-cols-[116px_1fr] bg-[#0d0d10] p-3 sm:min-h-[310px] sm:grid-cols-[150px_1fr] sm:p-4">
        <div className="border-r border-white/[0.07] pr-3 sm:pr-4">
          <div translate="no" className="mb-5 text-[11px] font-extrabold text-white">FloV<span className="text-brand">:MP</span></div>
          {[labels.projects, labels.servers, labels.telemetry, labels.console].map((label, index) => (
            <div key={label} className={`mb-1.5 rounded-lg px-2 py-2 text-[9px] sm:text-[11px] ${index === (mode === 'projects' ? 0 : mode === 'console' ? 3 : 1) ? 'bg-brand/[0.12] text-brand' : 'text-white/35'}`}>
              {label}
            </div>
          ))}
        </div>
        <div className="min-w-0 pl-3 sm:pl-5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <div className="text-[9px] text-white/35 sm:text-[11px]">{labels.workspace}</div>
              <div className="mt-1 text-sm font-extrabold text-white sm:text-base">{labels.project}</div>
            </div>
            <span className="rounded-lg border border-brand/25 px-2 py-1 text-[8px] font-semibold text-brand sm:text-[10px]">{labels.lifetime}</span>
          </div>
          <hr className="rule-soft my-4" />
          <div className="space-y-2.5">
            {rows.map((row, index) => (
              <div key={row} className="group flex items-center justify-between rounded-xl border border-white/[0.07] bg-white/[0.02] px-3 py-3 transition-colors hover:border-white/[0.14] hover:bg-white/[0.04]">
                <div>
                  <div className="text-[10px] font-semibold text-white/80 sm:text-[12px]">{row}</div>
                  <div className="mt-1 font-mono text-[8px] text-white/25 sm:text-[9px]">
                    {mode === 'projects' ? labels.environment : mode === 'console' ? labels.runtime : labels.protection}
                  </div>
                </div>
                <span className={`h-1.5 w-1.5 rounded-full ${index === 0 ? 'bg-brand' : 'bg-white/20'}`} />
              </div>
            ))}
          </div>
          <div className="mt-4 rounded-xl border border-white/[0.07] bg-black/20 p-3 font-mono text-[8px] leading-relaxed text-white/35 sm:text-[10px]">
            {mode === 'console' ? labels.consoleLine : mode === 'security' ? labels.securityLine : labels.projectLine}
          </div>
        </div>
      </div>
    </div>
  );
}

export default function HomePage() {
  const t = useT();

  return (
    <div>
      {/* ---------------- HERO ---------------- */}
      <div className="relative overflow-hidden">
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
                  <BtnLink href="/pricing" variant="primary" arrow className="h-12 px-6 text-sm sm:min-w-[220px]">
                    {t.home.ctaPrimary}
                  </BtnLink>
                  <BtnLink href="/docs" variant="ghost" className="h-12 px-6 text-sm sm:min-w-[220px]">
                    <Terminal aria-hidden="true" className="h-4 w-4" />
                    {t.home.ctaSecondary}
                  </BtnLink>
                </div>
              </Reveal>

              <Reveal delay={240}>
                <p className="mt-6 font-mono text-[11px] text-white/35">{t.home.trustLine}</p>
              </Reveal>
            </div>

            <Reveal delay={200}>
              <div>
                <ProductPreview labels={t.home.preview} />
              </div>
            </Reveal>
          </div>
        </Container>
      </div>

      {/* ---------------- НАЧАЛО РАБОТЫ ---------------- */}
      <Section>
        <SectionHeading title={t.home.flowTitle} sub={t.home.flowSub} />
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {t.home.flow.map((f, i) => (
            <Reveal key={f.n} delay={i * 90}>
              <div className="card group flex h-full min-h-[230px] flex-col p-6 sm:p-7">
                <div className="flex items-center justify-between">
                  <span className="section-num">0{i + 1}</span>
                  {i === 0 ? <KeyRound aria-hidden="true" className="icon-pop h-5 w-5 text-brand" /> : <ServerCog aria-hidden="true" className="icon-pop h-5 w-5 text-brand" />}
                </div>
                <div className="mt-auto pt-10">
                  <h3 className="text-[1.05rem] font-extrabold leading-tight text-white sm:text-[1.2rem]">{f.t}</h3>
                  <p className="mt-2 max-w-md text-[13px] leading-relaxed text-white/55">{f.d}</p>
                  <div className="mt-5">
                    <BtnLink href={f.href} variant={i === 0 ? 'primary' : 'ghost'} className="h-10 px-4 text-xs" arrow>
                      {f.cta}
                    </BtnLink>
                  </div>
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
                <div className="mx-auto w-full max-w-[430px]">
                  <h3 className="text-[1.3rem] font-extrabold leading-tight text-white sm:text-[1.55rem]">
                    {p.t}
                  </h3>
                  <p className="mt-3.5 text-[14px] leading-relaxed text-white/55">{p.d}</p>
                </div>
              </Reveal>
              <Reveal delay={100}>
                <div>
                  <ProductPreview labels={t.home.preview} mode={i === 0 ? 'projects' : i === 1 ? 'console' : 'security'} />
                </div>
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
              <Reveal key={f.t} delay={(i % 4) * 60} className="h-full">
                <div className="card card-hover flex h-full min-h-[210px] flex-col p-5">
                  <Icon aria-hidden="true" className="icon-pop h-5 w-5 text-brand" />
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

      {/* ---------------- QUICK START ---------------- */}
      <Section>
        <SectionHeading eyebrow="03" title={t.home.quickstartTitle} sub={t.home.quickstartSub} />
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Reveal>
            <div className="card h-full p-6">
              <div className="flex items-center gap-2 text-[13px] font-medium">
                <Terminal aria-hidden="true" className="h-4 w-4 text-brand" />
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
                <ShieldCheck aria-hidden="true" className="h-4 w-4 text-brand" />
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
                {i === 0 ? <Layers aria-hidden="true" className="icon-pop h-5 w-5 text-brand" /> : <Boxes aria-hidden="true" className="icon-pop h-5 w-5 text-brand" />}
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
          <div className="card card-hover p-10 text-center sm:p-14">
            <h2 className="mx-auto max-w-xl text-2xl font-semibold tracking-tight sm:text-3xl"><span className="h-grad">{t.home.ctaTitle}</span></h2>
            <p className="mx-auto mt-3 max-w-lg text-[14px] leading-relaxed text-white/55">{t.home.ctaSub}</p>
            <div className="mt-7 flex flex-col items-center justify-center gap-3 sm:flex-row">
              <BtnLink href="/contact" variant="primary" className="h-12 px-6 text-sm sm:min-w-[190px]">
                {t.common.contactSupport}
              </BtnLink>
              <BtnLink href="/auth/register" variant="ghost" className="h-12 px-6 text-sm sm:min-w-[190px]">
                {t.common.register}
                <ArrowRight aria-hidden="true" className="h-4 w-4" />
              </BtnLink>
            </div>
          </div>
        </Reveal>
      </Section>
    </div>
  );
}
