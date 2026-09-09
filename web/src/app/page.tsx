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

export default function HomePage() {
  const t = useT();

  return (
    <div>
      {/* ---------------- HERO ---------------- */}
      <div className="relative">
        <Container className="py-20 sm:py-28">
          <Reveal>
            <h1 className="max-w-4xl text-[1.9rem] font-extrabold leading-[1.12] tracking-tight sm:text-[3rem] sm:leading-[1.08]">
              <span className="h-grad">{t.home.h1a} </span>
              <span className="text-brand">{t.home.h1accent}</span>
              <span className="h-grad"> {t.home.h1b}</span>
            </h1>
          </Reveal>

          <Reveal delay={80}>
            <p className="mt-5 max-w-2xl text-[15px] leading-relaxed text-white/55 sm:text-lg">{t.home.sub}</p>
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
        </Container>
      </div>

      {/* ---------------- PILLARS ---------------- */}
      <Section>
        <SectionHeading eyebrow="01" title={t.home.pillarsTitle} />
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          {t.home.pillars.map((p, i) => (
            <Reveal key={p.n} delay={i * 70}>
              <div className="card card-hover h-full p-6">
                <div className="font-mono text-3xl font-semibold text-white/15">{p.n}</div>
                <h3 className="mt-4 text-[15px] font-semibold text-white">{p.t}</h3>
                <p className="mt-2 text-[13px] leading-relaxed text-white/50">{p.d}</p>
              </div>
            </Reveal>
          ))}
        </div>
      </Section>

      {/* ---------------- FEATURES ---------------- */}
      <Section>
        <SectionHeading eyebrow="02" title={t.home.featuresTitle} sub={t.home.featuresSub} />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {t.home.features.map((f, i) => {
            const Icon = FEATURE_ICONS[i % FEATURE_ICONS.length];
            return (
              <Reveal key={f.t} delay={(i % 4) * 60}>
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
