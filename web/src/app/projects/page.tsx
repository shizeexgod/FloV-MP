'use client';

import React from 'react';
import { ArrowRight, Rocket } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { BtnLink, Container, PageHero, Reveal } from '@/components/site';

export default function ProjectsPage() {
  const t = useT();
  const p = t.projects;

  return (
    <div>
      <PageHero eyebrow={t.nav.projects} title={p.title} sub={p.sub} />

      <Container className="py-16 sm:py-24">
        <Reveal>
          <div className="mx-auto max-w-xl rounded-2xl border border-white/[0.08] bg-white/[0.02] p-10 text-center sm:p-12">
            <span className="mx-auto grid h-11 w-11 place-items-center rounded-xl border border-white/10 bg-white/[0.03] text-brand">
              <Rocket className="h-5 w-5" />
            </span>
            <h2 className="mt-5 text-lg font-extrabold text-white sm:text-xl">{p.emptyTitle}</h2>
            <p className="mx-auto mt-3 max-w-md text-[13.5px] leading-relaxed text-white/55">{p.emptyText}</p>
            <div className="mt-7 flex flex-col items-center justify-center gap-3 sm:flex-row">
              <BtnLink href="/pricing" variant="primary" arrow className="h-11 px-6 text-sm">
                {p.emptyCta}
              </BtnLink>
              <BtnLink href="/contact" variant="ghost" className="h-11 px-6 text-sm">
                {t.nav.contact}
                <ArrowRight className="h-4 w-4" />
              </BtnLink>
            </div>
          </div>
        </Reveal>
      </Container>
    </div>
  );
}
