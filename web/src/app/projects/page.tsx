'use client';

import React from 'react';
import { ArrowUpRight } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import { Container, PageHero, Reveal } from '@/components/site';

const PROJECTS = [
  {
    name: 'Держава Онлайн',
    site: 'derzhava.online',
    tag: { ru: 'Карта Москвы · RMRP 2025', en: 'Moscow map · RMRP 2025' },
    online: 610,
    peak: 1500,
    servers: 3,
    status: 'online' as const,
  },
  {
    name: 'Florida V',
    site: 'floridav.gg',
    tag: { ru: 'Флагманский RP · экономика, фракции', en: 'Flagship RP · economy, factions' },
    online: 1840,
    peak: 2310,
    servers: 4,
    status: 'online' as const,
  },
  {
    name: 'Sayonara RP',
    site: 'sayonara-rp.com',
    tag: { ru: 'Хардкор RP · медленный старт', en: 'Hardcore RP · slow start' },
    online: 920,
    peak: 1180,
    servers: 2,
    status: 'online' as const,
  },
];

export default function ProjectsPage() {
  const { t, lang } = useI18n();
  return (
    <div>
      <PageHero eyebrow={t.nav.projects} title={t.projects.title} sub={t.projects.sub} />

      <Container className="py-14 sm:py-16">
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {PROJECTS.map((p, i) => (
            <Reveal key={p.name} delay={i * 70}>
              <div className="card card-hover flex h-full flex-col p-6">
                <div className="flex items-center justify-between">
                  <span className="text-[15px] font-semibold text-white">{p.name}</span>
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-ok/30 px-2 py-0.5 text-[11px] text-ok">
                    <span className="h-1.5 w-1.5 rounded-full bg-ok" />
                    {t.common.online}
                  </span>
                </div>
                <p className="mt-1 text-[12px] text-white/40">{p.tag[lang]}</p>

                <div className="mt-5 grid grid-cols-3 gap-2 text-center">
                  {[
                    [p.online.toLocaleString('ru-RU'), t.projects.thOnline],
                    [p.peak.toLocaleString('ru-RU'), t.projects.thPeak],
                    [String(p.servers), t.projects.thServers],
                  ].map(([v, l]) => (
                    <div key={l} className="rounded-lg border border-white/[0.07] bg-white/[0.02] py-2">
                      <div className="text-[14px] font-semibold text-white">{v}</div>
                      <div className="font-mono text-[9px] uppercase tracking-wider text-white/35">{l}</div>
                    </div>
                  ))}
                </div>

                <a
                  href={`https://${p.site}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="btn btn-ghost mt-5 h-9 text-xs"
                >
                  {t.projects.visit} · {p.site}
                  <ArrowUpRight className="h-3.5 w-3.5" />
                </a>
              </div>
            </Reveal>
          ))}
        </div>
      </Container>
    </div>
  );
}
