'use client';

import React from 'react';
import { Check, CircleDashed, Loader } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { Container, PageHero, Reveal } from '@/components/site';

export default function RoadmapPage() {
  const t = useT();
  const statusMap: Record<string, { label: string; cls: string; Icon: any }> = {
    done: { label: t.roadmap.statusDone, cls: 'text-ok border-ok/30', Icon: Check },
    active: { label: t.roadmap.statusActive, cls: 'text-brand border-brand/30', Icon: Loader },
    planned: { label: t.roadmap.statusPlanned, cls: 'text-white/40 border-white/15', Icon: CircleDashed },
  };

  return (
    <div>
      <PageHero eyebrow={t.nav.roadmap} title={t.roadmap.title} sub={t.roadmap.sub} />

      <Container className="py-14 sm:py-16">
        <ol className="relative space-y-4 border-l border-white/[0.1] pl-6">
          {t.roadmap.items.map((it, i) => {
            const s = statusMap[it.s];
            return (
              <Reveal key={it.t + i} delay={i * 50} as="li">
                <span className="absolute -left-[7px] mt-1.5 h-3.5 w-3.5 rounded-full border-2 border-ink-950 bg-white/20" />
                <div className="card p-5">
                  <div className="flex flex-wrap items-center gap-3">
                    <span className="font-mono text-[11px] text-white/40">{it.q}</span>
                    <h3 className="text-[15px] font-semibold text-white">{it.t}</h3>
                    <span className={`inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[10px] font-medium ${s.cls}`}>
                      <s.Icon className="h-3 w-3" />
                      {s.label}
                    </span>
                  </div>
                  <p className="mt-2 text-[13px] leading-relaxed text-white/50">{it.d}</p>
                </div>
              </Reveal>
            );
          })}
        </ol>
      </Container>
    </div>
  );
}
