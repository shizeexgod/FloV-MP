'use client';

import React, { useEffect, useState } from 'react';
import {
  ArrowRight,
  Boxes,
  Cloud,
  Gauge,
  Layers,
  Lock,
  Radio,
  ShieldCheck,
  Terminal,
  Users,
  Webhook,
} from 'lucide-react';
import { useT } from '@/lib/i18n';
import { BtnLink, Container, CountUp, Reveal, Section, SectionHeading } from '@/components/site';

interface ServerStatus {
  online: boolean;
  name: string;
  players: number;
  maxPlayers: number;
  pingMs: number;
}

const FEATURE_ICONS = [Radio, Users, Cloud, Layers, Gauge, Boxes, Lock, Webhook];

export default function HomePage() {
  const t = useT();
  const [status, setStatus] = useState<ServerStatus>({
    online: true,
    name: 'Держава Онлайн',
    players: 1,
    maxPlayers: 1500,
    pingMs: 24,
  });

  useEffect(() => {
    fetch('/api/server-status')
      .then((r) => r.json())
      .then((d) => d && setStatus((s) => ({ ...s, ...d })))
      .catch(() => {});
  }, []);

  const stats = [
    { key: 'projects', value: 47, suffix: '', label: t.home.stats.projects },
    { key: 'servers', value: 128, suffix: '', label: t.home.stats.servers },
    { key: 'players', value: 9340, suffix: '', label: t.home.stats.players },
    { key: 'uptime', value: 99.98, suffix: '%', decimals: 2, label: t.home.stats.uptime },
  ];

  return (
    <div>
      {/* ---------------- HERO ---------------- */}
      <div className="relative border-b border-white/[0.07]">
        <Container className="py-20 sm:py-28">
          <Reveal>
            <span className="inline-flex items-center gap-2 rounded-full border border-brand/30 bg-brand/10 px-3 py-1">
              <span className="status-dot text-brand" />
              <span className="eyebrow">{t.home.badge} • 2026</span>
            </span>
          </Reveal>

          <Reveal delay={60}>
            <h1 className="mt-6 max-w-4xl text-4xl font-semibold leading-[1.08] tracking-tight sm:text-6xl">
              {t.home.h1a} <span className="text-brand">{t.home.h1accent}</span> {t.home.h1b}
            </h1>
          </Reveal>

          <Reveal delay={120}>
            <p className="mt-5 max-w-2xl text-[15px] leading-relaxed text-white/55 sm:text-lg">{t.home.sub}</p>
          </Reveal>

          <Reveal delay={180}>
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

          {/* live status card */}
          <Reveal delay={300}>
            <div className="mt-12 max-w-2xl rounded-2xl border border-white/[0.08] bg-white/[0.02] p-5">
              <div className="flex flex-wrap items-center justify-between gap-4">
                <div className="flex items-center gap-3">
                  <span className={status.online ? 'status-dot text-ok' : 'status-dot text-err'} />
                  <div>
                    <div className="text-sm font-semibold text-white">{status.name}</div>
                    <div className="font-mono text-[11px] text-white/40">188.127.229.224:7788</div>
                  </div>
                </div>
                <div className="flex items-center gap-5 font-mono text-[12px]">
                  <div className="text-right">
                    <div className="text-[10px] uppercase tracking-wider text-white/35">ping</div>
                    <div className="font-semibold text-white">{status.pingMs} ms</div>
                  </div>
                  <div className="text-right">
                    <div className="text-[10px] uppercase tracking-wider text-white/35">{t.projects.thOnline}</div>
                    <div className="font-semibold text-white">
                      {status.players} / {status.maxPlayers}
                    </div>
                  </div>
                </div>
              </div>
              <div className="mt-4 h-1.5 w-full overflow-hidden rounded-full bg-white/[0.06]">
                <div
                  className="h-full rounded-full bg-brand transition-all duration-700"
                  style={{ width: `${Math.max(2, Math.round((status.players / status.maxPlayers) * 100))}%` }}
                />
              </div>
            </div>
          </Reveal>
        </Container>
      </div>

      {/* ---------------- STATS ---------------- */}
      <Section>
        <SectionHeading eyebrow="00" title={t.home.statsTitle} />
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          {stats.map((s, i) => (
            <Reveal key={s.key} delay={i * 60}>
              <div className="rounded-xl border border-white/[0.08] bg-white/[0.02] p-5">
                <div className="text-[2rem] font-semibold tracking-tight text-white">
                  <CountUp value={s.value} suffix={s.suffix} decimals={(s as any).decimals ?? 0} />
                </div>
                <div className="mt-1 text-[13px] text-white/45">{s.label}</div>
              </div>
            </Reveal>
          ))}
        </div>
      </Section>

      {/* ---------------- PILLARS ---------------- */}
      <Section>
        <SectionHeading eyebrow="01–03" title={t.home.pillarsTitle} />
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
        <SectionHeading eyebrow="04" title={t.home.featuresTitle} sub={t.home.featuresSub} />
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
        <SectionHeading eyebrow="05" title={t.home.quickstartTitle} sub={t.home.quickstartSub} />
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
              <div className="mt-4 flex flex-wrap gap-x-4 gap-y-1 border-t border-white/[0.06] pt-3 font-mono text-[11px] text-white/35">
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
        <SectionHeading eyebrow="06" title={t.home.ownershipTitle} sub={t.home.ownershipSub} />
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
