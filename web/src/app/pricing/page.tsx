'use client';

import React, { useState } from 'react';
import { Check, ChevronDown, Infinity as InfinityIcon } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { BtnLink, Container, PageHero, Reveal } from '@/components/site';

export default function PricingPage() {
  const t = useT();
  const p = t.pricing;
  const [openFaq, setOpenFaq] = useState<number | null>(0);

  return (
    <div>
      <PageHero eyebrow={t.nav.pricing} title={p.title} sub={p.sub} />

      <Container className="py-16 sm:py-20">
        <Reveal className="mx-auto max-w-md">
          <div className="card card-hover flex h-full flex-col rounded-2xl border-brand/40 bg-brand/[0.05] p-8">
            <span className="inline-flex w-max items-center gap-1.5 rounded-full border border-brand/40 bg-brand/10 px-2.5 py-1 font-mono text-[10px] font-semibold uppercase tracking-wider text-brand">
              <InfinityIcon className="h-3.5 w-3.5" />
              {p.planBadge}
            </span>
            <h3 className="mt-4 text-xl font-extrabold text-white">{p.planName}</h3>
            <p className="mt-1.5 text-[13px] leading-relaxed text-white/50">{p.planTagline}</p>

            <div className="mt-7 flex items-end gap-2.5">
              <span className="text-[2.75rem] font-extrabold leading-none tracking-tight">
                <span className="h-grad">{p.price}</span>
              </span>
              <span className="pb-1.5 text-[12px] text-white/45">{p.priceNote}</span>
            </div>

            <hr className="rule-soft my-7" />

            <ul className="flex-1 space-y-3 text-[13px] leading-relaxed text-white/75">
              {p.includes.map((f) => (
                <li key={f} className="flex items-start gap-2.5">
                  <Check className="mt-0.5 h-4 w-4 flex-none text-brand" />
                  {f}
                </li>
              ))}
            </ul>

            <BtnLink href="/contact" variant="primary" arrow className="mt-8 h-11 text-sm">
              {p.cta}
            </BtnLink>
            <p className="mt-4 text-center text-[11.5px] leading-relaxed text-white/35">{p.detailsNote}</p>
          </div>
        </Reveal>
      </Container>

      <section>
        <Container>
          <hr className="rule" />
        </Container>
        <Container className="py-16 sm:py-20">
          <Reveal className="mb-8">
            <h2 className="text-[1.5rem] font-extrabold tracking-tight">
              <span className="h-grad">{p.faqTitle}</span>
            </h2>
          </Reveal>
          <div className="mx-auto max-w-3xl space-y-3">
            {p.faq.map(([q, a], i) => {
              const open = openFaq === i;
              return (
                <div key={q} className="glass card-hover overflow-hidden rounded-xl">
                  <button
                    onClick={() => setOpenFaq(open ? null : i)}
                    className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left"
                  >
                    <span className="text-[14px] font-semibold text-white">{q}</span>
                    <ChevronDown className={`h-4 w-4 flex-none text-brand transition-transform ${open ? 'rotate-180' : ''}`} />
                  </button>
                  <div className={`grid transition-all duration-300 ${open ? 'grid-rows-[1fr] opacity-100' : 'grid-rows-[0fr] opacity-0'}`}>
                    <div className="overflow-hidden">
                      <p className="px-5 pb-4 text-[13px] leading-relaxed text-white/55">{a}</p>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </Container>
      </section>
    </div>
  );
}
