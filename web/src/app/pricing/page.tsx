'use client';

import React, { useState } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { BtnLink, Container, PageHero, Reveal } from '@/components/site';

type Period = 'month' | 'half' | 'year';
const MULT: Record<Period, number> = { month: 1, half: 0.85, year: 0.7 };

function scalePrice(base: string, period: Period): string {
  const m = base.match(/([\d\s.,]+)/);
  if (!m) return base;
  const num = parseFloat(m[1].replace(/\s/g, '').replace(',', '.'));
  if (!isFinite(num) || num === 0) return base;
  const scaled = Math.round((num * MULT[period]) / 10) * 10;
  return base.replace(m[1].trim(), scaled.toLocaleString('ru-RU'));
}

export default function PricingPage() {
  const t = useT();
  const [period, setPeriod] = useState<Period>('month');
  const [openFaq, setOpenFaq] = useState<number | null>(0);

  const periods: { id: Period; label: string; off?: string }[] = [
    { id: 'month', label: t.pricing.periodMonth },
    { id: 'half', label: t.pricing.periodHalf, off: '−15%' },
    { id: 'year', label: t.pricing.periodYear, off: '−30%' },
  ];

  return (
    <div>
      <PageHero eyebrow={t.nav.pricing} title={t.pricing.title} sub={t.pricing.sub} />

      <Container className="py-14 sm:py-16">
        <Reveal className="mb-10 flex justify-center">
          <div className="inline-flex rounded-xl border border-white/10 bg-white/[0.03] p-1">
            {periods.map((p) => (
              <button
                key={p.id}
                onClick={() => setPeriod(p.id)}
                className={`rounded-lg px-3.5 py-2 text-xs font-semibold transition-colors ${
                  period === p.id ? 'bg-brand text-[#0a0a0c]' : 'text-white/50 hover:text-white'
                }`}
              >
                {p.label}
                {p.off ? (
                  <span
                    className={`ml-1.5 rounded px-1 py-0.5 font-mono text-[9px] ${
                      period === p.id ? 'bg-black/15' : 'bg-ok/15 text-ok'
                    }`}
                  >
                    {p.off}
                  </span>
                ) : null}
              </button>
            ))}
          </div>
        </Reveal>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          {t.pricing.plans.map((plan, i) => {
            const featured = plan.id === 'pro';
            return (
              <Reveal key={plan.id} delay={i * 70}>
                <div
                  className={`flex h-full flex-col rounded-2xl border p-7 ${
                    featured ? 'border-brand/40 bg-brand/[0.04]' : 'border-white/[0.08] bg-white/[0.02]'
                  }`}
                >
                  {featured ? (
                    <span className="mb-3 inline-flex w-max rounded-full bg-brand px-2.5 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-[#0a0a0c]">
                      {t.pricing.popular}
                    </span>
                  ) : null}
                  <h3 className="text-lg font-semibold text-white">{plan.name}</h3>
                  <p className="mt-1 text-[13px] text-white/45">{plan.tagline}</p>
                  <div className="mt-6 flex items-end gap-2">
                    <span className="text-3xl font-semibold tracking-tight text-white">
                      {plan.id === 'starter' ? plan.priceMonth : scalePrice(plan.priceMonth, period)}
                    </span>
                    <span className="pb-1 text-[12px] text-white/40">{plan.note}</span>
                  </div>
                  <ul className="mt-6 flex-1 space-y-2.5 text-[13px] text-white/70">
                    {plan.features.map((f) => (
                      <li key={f} className="flex items-start gap-2.5">
                        <Check className={`mt-0.5 h-4 w-4 flex-none ${featured ? 'text-brand' : 'text-ok'}`} />
                        {f}
                      </li>
                    ))}
                  </ul>
                  <BtnLink
                    href={plan.id === 'starter' ? '/auth/register' : '/contact'}
                    variant={featured ? 'primary' : 'ghost'}
                    className="mt-7 h-11 text-sm"
                  >
                    {plan.id === 'starter' ? t.pricing.ctaFree : t.pricing.cta}
                  </BtnLink>
                </div>
              </Reveal>
            );
          })}
        </div>
      </Container>

      <section className="border-t border-white/[0.07]">
        <Container className="py-16">
          <Reveal className="mb-8">
            <h2 className="text-xl font-semibold tracking-tight">{t.pricing.faqTitle}</h2>
          </Reveal>
          <div className="mx-auto max-w-3xl space-y-3">
            {t.pricing.faq.map(([q, a], i) => {
              const open = openFaq === i;
              return (
                <div key={q} className="overflow-hidden rounded-xl border border-white/[0.08] bg-white/[0.02]">
                  <button
                    onClick={() => setOpenFaq(open ? null : i)}
                    className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left"
                  >
                    <span className="text-[14px] font-medium text-white">{q}</span>
                    <ChevronDown className={`h-4 w-4 flex-none text-brand transition-transform ${open ? 'rotate-180' : ''}`} />
                  </button>
                  <div className={`grid transition-all duration-300 ${open ? 'grid-rows-[1fr] opacity-100' : 'grid-rows-[0fr] opacity-0'}`}>
                    <div className="overflow-hidden">
                      <p className="px-5 pb-4 text-[13px] leading-relaxed text-white/50">{a}</p>
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
