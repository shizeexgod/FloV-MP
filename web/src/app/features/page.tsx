'use client';

import React from 'react';
import { Check, Cloud, Code2, ShieldCheck, Wrench } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { BtnLink, Container, PageHero, Reveal } from '@/components/site';

const GROUP_ICONS = [Code2, Cloud, ShieldCheck, Wrench];

export default function FeaturesPage() {
  const t = useT();
  return (
    <div>
      <PageHero eyebrow={t.nav.features} title={t.features.title} sub={t.features.sub} />

      {t.features.groups.map((g, gi) => {
        const Icon = GROUP_ICONS[gi % GROUP_ICONS.length];
        return (
          <section key={g.t}>
            <Container className="py-14 sm:py-20">
              <Reveal className="mb-8 flex items-center gap-3">
                <span className="grid h-9 w-9 place-items-center rounded-lg border border-white/10 bg-white/[0.03] text-brand">
                  <Icon className="h-4 w-4" />
                </span>
                <h2 className="text-[1.4rem] font-extrabold tracking-tight">
                  <span className="h-grad">{g.t}</span>
                </h2>
              </Reveal>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                {g.items.map(([title, desc], i) => (
                  <Reveal key={title} delay={i * 50}>
                    <div className="card card-hover h-full p-5">
                      <div className="flex items-start gap-3">
                        <Check className="mt-0.5 h-4 w-4 flex-none text-brand" />
                        <div>
                          <h3 className="text-[14px] font-semibold text-white">{title}</h3>
                          <p className="mt-1.5 text-[12.5px] leading-relaxed text-white/50">{desc}</p>
                        </div>
                      </div>
                    </div>
                  </Reveal>
                ))}
              </div>
            </Container>
            <Container>
              <hr className="rule" />
            </Container>
          </section>
        );
      })}

      <Container className="py-16">
        <Reveal>
          <div className="rounded-2xl border border-white/[0.08] bg-white/[0.02] p-8 text-center sm:p-12">
            <h2 className="text-xl font-semibold tracking-tight sm:text-2xl">{t.home.ctaTitle}</h2>
            <div className="mt-6 flex flex-col items-center justify-center gap-3 sm:flex-row">
              <BtnLink href="/pricing" variant="primary" arrow className="h-11 px-6 text-sm">
                {t.common.getLicense}
              </BtnLink>
              <BtnLink href="/docs" variant="ghost" className="h-11 px-6 text-sm">
                {t.common.docs}
              </BtnLink>
            </div>
          </div>
        </Reveal>
      </Container>
    </div>
  );
}
