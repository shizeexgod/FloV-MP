'use client';

import React, { useEffect, useState } from 'react';
import { ChevronRight } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { Container, PageHero, Reveal } from '@/components/site';

export default function DocsPage() {
  const t = useT();
  const d = t.docs;
  const sections = d.sections;
  const [active, setActive] = useState(sections[0]?.id ?? '');

  useEffect(() => {
    const els = sections
      .map((s) => document.getElementById(s.id))
      .filter((el): el is HTMLElement => !!el);
    if (!els.length) return;

    const obs = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)[0];
        if (visible) setActive(visible.target.id);
      },
      { rootMargin: '-20% 0px -70% 0px', threshold: 0 },
    );
    els.forEach((el) => obs.observe(el));
    return () => obs.disconnect();
  }, [sections]);

  return (
    <div>
      <PageHero eyebrow={t.nav.docs} title={d.title} sub={d.sub} />

      <Container className="py-14 sm:py-20">
        <div className="grid grid-cols-1 gap-10 lg:grid-cols-[240px_1fr]">
          <aside className="lg:sticky lg:top-28 lg:h-max">
            <div className="mb-3 px-3 font-mono text-[10px] font-semibold uppercase tracking-[0.22em] text-white/35">
              {d.toc}
            </div>
            <nav className="glass no-lift p-2">
              {sections.map((s) => {
                const on = active === s.id;
                return (
                  <a
                    key={s.id}
                    href={`#${s.id}`}
                    className={`group flex items-center gap-2 rounded-lg px-3 py-2.5 text-[13px] font-semibold transition-colors ${
                      on ? 'bg-white/[0.06] text-white' : 'text-white/45 hover:bg-white/[0.04] hover:text-white/80'
                    }`}
                  >
                    <span className={`h-1.5 w-1.5 flex-none rounded-full transition-colors ${on ? 'bg-brand' : 'bg-white/15'}`} />
                    {s.label}
                    <ChevronRight
                      className={`ml-auto h-3.5 w-3.5 transition-opacity ${on ? 'opacity-60' : 'opacity-0 group-hover:opacity-40'}`}
                    />
                  </a>
                );
              })}
            </nav>
          </aside>

          <div className="min-w-0 max-w-2xl space-y-16">
            {sections.map((s, si) => (
              <section key={s.id} id={s.id} className="scroll-mt-28">
                <Reveal>
                  <div className="section-num">{String(si + 1).padStart(2, '0')}</div>
                  <h2 className="mt-3 text-[1.5rem] font-extrabold leading-tight tracking-tight sm:text-[1.9rem]">
                    <span className="h-grad">{s.heading}</span>
                  </h2>
                </Reveal>
                <div className="mt-5 space-y-4">
                  {s.body.map((p, pi) => (
                    <Reveal key={pi} delay={pi * 30}>
                      <p className="text-[14.5px] leading-[1.75] text-white/60">{p}</p>
                    </Reveal>
                  ))}
                </div>
                {si < sections.length - 1 && <hr className="rule mt-14" />}
              </section>
            ))}
          </div>
        </div>
      </Container>
    </div>
  );
}
