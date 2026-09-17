'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import {
  ArrowRight,
  Check,
  Code2,
  FolderKanban,
  Infinity as InfinityIcon,
  ShieldCheck,
} from 'lucide-react';
import { useDashboard } from './_ctx';

type AccessPeriod = 'month' | 'halfyear' | 'year' | 'lifetime';

export function NoProjectGate() {
  const { D, licenses, setNewProjOpen, setNewProjPlan } = useDashboard();
  const [period, setPeriod] = useState<AccessPeriod>('lifetime');
  const hasActiveLicense = licenses.some((license) => Number(license.is_active) === 1);

  if (hasActiveLicense) {
    return (
      <section className="mx-auto max-w-3xl border-y border-white/[0.08] py-8 sm:py-10">
        <div className="flex flex-col gap-7 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex max-w-xl items-start gap-4">
            <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl border border-brand/20 bg-brand/[0.07] text-brand">
              <FolderKanban aria-hidden="true" className="h-[18px] w-[18px]" />
            </span>
            <div>
              <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-brand">{D.gate.eyebrow}</p>
              <h2 className="mt-2 text-xl font-bold tracking-[-0.025em] text-white">{D.gate.projectTitle}</h2>
              <p className="mt-2 text-[13px] leading-6 text-white/48">{D.gate.projectDesc}</p>
            </div>
          </div>
          <button type="button" onClick={() => setNewProjOpen(true)} className="btn btn-primary h-11 shrink-0 px-5 text-[13px]">
            {D.proj.create}
            <ArrowRight aria-hidden="true" className="h-4 w-4" />
          </button>
        </div>
      </section>
    );
  }

  const lifetime = period === 'lifetime';

  return (
    <section className="mx-auto w-full max-w-5xl">
      <div className="flex flex-col gap-5 border-b border-white/[0.08] pb-6 sm:flex-row sm:items-end sm:justify-between">
        <div className="max-w-2xl">
          <p className="text-[10px] font-bold uppercase tracking-[0.17em] text-brand">{D.gate.eyebrow}</p>
          <h2 className="mt-2 text-2xl font-bold tracking-[-0.035em] text-white sm:text-[1.8rem]">{D.gate.title}</h2>
          <p className="mt-2 text-[13px] leading-6 text-white/48">{D.gate.desc}</p>
        </div>
        <div className="grid grid-cols-4 rounded-xl border border-white/[0.08] bg-white/[0.025] p-1" aria-label={D.gate.title}>
          {D.gate.periods.map((item: { id: AccessPeriod; label: string }) => (
            <button
              key={item.id}
              type="button"
              aria-pressed={period === item.id}
              onClick={() => setPeriod(item.id)}
              className={`min-h-8 rounded-lg px-3 text-[10px] font-bold transition-[color,background-color] duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand/50 ${period === item.id ? 'bg-white/[0.09] text-white' : 'text-white/35 hover:text-white/70'}`}
            >
              {item.label}
            </button>
          ))}
        </div>
      </div>

      <div className="grid gap-4 py-6 lg:grid-cols-[1.08fr_0.92fr]">
        <article className="card-edge flex min-h-[330px] flex-col rounded-2xl border border-brand/25 bg-[#111114] p-6 shadow-[0_24px_70px_-46px_rgba(255,61,138,0.45)] sm:p-7">
          <div className="flex items-start justify-between gap-5">
            <span className="grid h-11 w-11 place-items-center rounded-xl border border-brand/25 bg-brand/[0.075] text-brand">
              {lifetime ? <InfinityIcon aria-hidden="true" className="h-5 w-5" /> : <ShieldCheck aria-hidden="true" className="h-5 w-5" />}
            </span>
            <span className="rounded-lg border border-white/[0.09] px-2.5 py-1 font-mono text-[9px] uppercase tracking-[0.14em] text-white/40">
              {D.gate.periods.find((item: { id: AccessPeriod }) => item.id === period)?.label}
            </span>
          </div>
          <h3 className="mt-6 text-xl font-bold tracking-[-0.025em] text-white">{D.gate.licenseTitle}</h3>
          <p className="mt-2 max-w-lg text-[13px] leading-6 text-white/48">{D.gate.licenseDesc}</p>
          <ul className="mt-5 space-y-2.5">
            {D.gate.licenseFeatures.map((feature: string) => (
              <li key={feature} className="flex items-start gap-2.5 text-[12px] leading-5 text-white/58">
                <Check aria-hidden="true" className="mt-0.5 h-3.5 w-3.5 shrink-0 text-brand" />
                {feature}
              </li>
            ))}
          </ul>
          <div className="mt-auto flex flex-col gap-4 pt-7 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <div className="text-2xl font-extrabold tracking-[-0.035em] text-white">{lifetime ? D.gate.lifetimePrice : D.gate.requestPrice}</div>
              {lifetime ? <div className="mt-1 text-[10px] text-white/30">{D.gate.priceSuffix}</div> : null}
            </div>
            {lifetime ? (
              <button
                type="button"
                onClick={() => {
                  setNewProjPlan('lifetime');
                  setNewProjOpen(true);
                }}
                className="btn btn-primary h-11 px-5 text-[12px]"
              >
                {D.gate.buyLifetime}
                <ArrowRight aria-hidden="true" className="h-4 w-4" />
              </button>
            ) : (
              <Link href="/contact" className="btn btn-ghost h-11 px-5 text-[12px]">
                {D.gate.requestTerms}
                <ArrowRight aria-hidden="true" className="h-4 w-4" />
              </Link>
            )}
          </div>
        </article>

        <article className="flex min-h-[330px] flex-col rounded-2xl border border-white/[0.085] bg-white/[0.018] p-6 sm:p-7">
          <span className="grid h-11 w-11 place-items-center rounded-xl border border-white/[0.1] bg-white/[0.035] text-white/65">
            <Code2 aria-hidden="true" className="h-5 w-5" />
          </span>
          <h3 className="mt-6 text-xl font-bold tracking-[-0.025em] text-white">{D.gate.sourceTitle}</h3>
          <p className="mt-2 text-[13px] leading-6 text-white/48">{D.gate.sourceDesc}</p>
          <ul className="mt-5 space-y-2.5">
            {D.gate.sourceFeatures.map((feature: string) => (
              <li key={feature} className="flex items-start gap-2.5 text-[12px] leading-5 text-white/55">
                <Check aria-hidden="true" className="mt-0.5 h-3.5 w-3.5 shrink-0 text-white/35" />
                {feature}
              </li>
            ))}
          </ul>
          <div className="mt-auto pt-7">
            <div className="text-lg font-bold text-white">{D.gate.sourcePrice}</div>
            <Link href="/contact" className="btn btn-ghost mt-4 h-11 w-full justify-between px-4 text-[12px]">
              {D.gate.discussSource}
              <ArrowRight aria-hidden="true" className="h-4 w-4" />
            </Link>
          </div>
        </article>
      </div>

      <div className="flex items-start gap-3 border-t border-white/[0.08] pt-5 text-[11px] leading-5 text-white/35">
        <ShieldCheck aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-brand/70" />
        <div>
          <p>{D.gate.sourceLegal}</p>
          <p className="mt-1 text-white/25">{D.gate.unavailableHint}</p>
        </div>
      </div>
    </section>
  );
}
