'use client';

import React from 'react';
import { ArrowRight } from 'lucide-react';
import { Spinner, Modal, FieldLabel } from '@/components/ui';
import { useDashboard } from './_ctx';

export function InvoiceModal() {
  const {
    invoiceOpen,
    setInvoiceOpen,
    invPlan,
    setInvPlan,
    invPeriod,
    setInvPeriod,
    invMethod,
    setInvMethod,
    creatingInvoice,
    createInvoice,
    D,
  } = useDashboard();
  return (
      <Modal
        open={invoiceOpen}
        onClose={() => setInvoiceOpen(false)}
        title={D.modal.invoiceTitle}
        description={D.modal.invoiceDesc}
      >
        <form onSubmit={createInvoice} className="space-y-5">
          <div>
            <FieldLabel>{D.modal.planLabel}</FieldLabel>
            <div className="grid grid-cols-2 gap-3">
              {[
                { id: 'business', name: 'RP', note: `512 ${D.proj.slots} · 14 900 ₽`, tone: 'brand' },
                { id: 'enterprise', name: 'Enterprise', note: `1500+ ${D.proj.slots} · 49 000 ₽`, tone: 'cyber' },
              ].map((p) => (
                <button
                  type="button"
                  key={p.id}
                  onClick={() => setInvPlan(p.id)}
                  className={`rounded-xl border p-3 text-left transition ${
                    invPlan === p.id
                      ? p.tone === 'cyber'
                        ? 'border-cyber/60 bg-cyber/10'
                        : 'border-brand/60 bg-brand/10'
                      : 'border-white/10 bg-white/[0.02]'
                  }`}
                >
                  <div className={`text-xs font-bold ${p.tone === 'cyber' ? 'text-cyber' : 'text-brand'}`}>{p.name}</div>
                  <div className="mt-1 font-mono text-[11px] text-slate-400">{p.note}</div>
                </button>
              ))}
            </div>
          </div>

          <div>
            <FieldLabel>{D.modal.periodLabel}</FieldLabel>
            <div className="grid grid-cols-3 gap-2">
              {(
                [
                  ['monthly', D.modal.pMonth],
                  ['halfYear', D.modal.pHalf],
                  ['year', D.modal.pYear],
                ] as const
              ).map(([id, label]) => (
                <button
                  type="button"
                  key={id}
                  onClick={() => setInvPeriod(id)}
                  className={`rounded-xl border px-3 py-2 font-mono text-[11px] font-bold transition ${
                    invPeriod === id ? 'border-brand/60 bg-brand/10 text-white' : 'border-white/10 text-slate-400'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          <div>
            <FieldLabel>{D.modal.methodLabel}</FieldLabel>
            <div className="grid grid-cols-3 gap-2">
              {(
                [
                  ['card', D.modal.mCard],
                  ['sbp', D.modal.mSbp],
                  ['crypto', 'USDT'],
                ] as const
              ).map(([id, label]) => (
                <button
                  type="button"
                  key={id}
                  onClick={() => setInvMethod(id)}
                  className={`rounded-xl border px-3 py-2 text-xs font-semibold transition ${
                    invMethod === id ? 'border-brand/60 bg-brand/10 text-white' : 'border-white/10 text-slate-400'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
            <button type="button" onClick={() => setInvoiceOpen(false)} className="px-4 py-2 text-xs text-slate-400 transition hover:text-white">
              {D.cancel}
            </button>
            <button type="submit" disabled={creatingInvoice} className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50">
              {creatingInvoice ? <Spinner className="h-4 w-4" /> : <ArrowRight className="h-4 w-4" />}
              {D.modal.issueInvoice}
            </button>
          </div>
        </form>
      </Modal>
  );
}
