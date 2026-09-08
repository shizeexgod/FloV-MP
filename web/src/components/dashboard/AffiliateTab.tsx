'use client';

import React from 'react';
import { Check, Copy, Percent } from 'lucide-react';
import { useDashboard } from './_ctx';

export function AffiliateTab() {
  const {
    copied,
    copy,
    D,
    promoCode,
  } = useDashboard();
  return (
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge rounded-3xl p-7 shadow-glass sm:p-8">
            <div className="flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
              <div className="max-w-2xl space-y-2">
                <span className="eyebrow text-brand flex items-center gap-2">
                  <Percent className="h-4 w-4" />
                  {D.aff.program}
                </span>
                <h3 className="text-2xl font-black text-white">{D.aff.earn}</h3>
                <p className="text-xs leading-relaxed text-slate-400">
                  {D.aff.descA}{' '}<strong className="text-white">{D.aff.descB}</strong> {D.aff.descC}{' '}<strong className="text-emeraldx">{D.aff.descD}</strong>.
                </p>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <div className="rounded-xl border border-white/10 bg-ink-950/60 px-4 py-2.5 font-mono text-sm font-bold text-brand">
                  {promoCode}
                </div>
                <button
                  onClick={() => copy(promoCode)}
                  className="btn h-11 border border-brand/40 bg-brand/15 px-4 text-xs font-bold text-brand transition hover:bg-brand/25"
                >
                  {copied === promoCode ? <Check className="h-4 w-4 text-emeraldx" /> : <Copy className="h-4 w-4" />}
                  {copied === promoCode ? D.aff.copied : D.aff.copy}
                </button>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-3">
            <div className="glass card-edge rounded-2xl p-6">
              <div className="font-mono text-[10px] uppercase tracking-wider text-slate-500">{D.aff.invited}</div>
              <div className="mt-2 font-mono text-3xl font-black text-white">0</div>
              <div className="mt-2 font-mono text-[11px] text-slate-500">{D.aff.activeRefs}</div>
            </div>
            <div className="glass card-edge rounded-2xl p-6">
              <div className="font-mono text-[10px] uppercase tracking-wider text-slate-500">{D.aff.earned}</div>
              <div className="mt-2 font-mono text-3xl font-black text-emeraldx">0 ₽</div>
              <div className="mt-2 font-mono text-[11px] text-slate-500">{D.aff.available}</div>
            </div>
            <div className="glass card-edge rounded-2xl p-6">
              <div className="font-mono text-[10px] uppercase tracking-wider text-slate-500">{D.aff.percent}</div>
              <div className="mt-2 font-mono text-3xl font-black text-brand">20%</div>
              <div className="mt-2 font-mono text-[11px] text-slate-500">{D.aff.lifetime}</div>
            </div>
          </div>
        </div>
  );
}
