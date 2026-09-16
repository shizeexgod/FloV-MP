'use client';

import React from 'react';
import { ArrowRight, Check, CreditCard } from 'lucide-react';
import { Spinner, Modal, FieldLabel } from '@/components/ui';
import { useDashboard } from './_ctx';

export function InvoiceModal() {
  const {
    invoiceOpen,
    setInvoiceOpen,
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
          <div className="rounded-2xl border border-brand/25 bg-brand/[0.06] p-4">
            <div className="flex items-start gap-3">
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-brand/10 text-brand">
                <CreditCard aria-hidden="true" className="h-4 w-4" />
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-3">
                  <strong className="text-sm text-white">FloV:MP Lifetime</strong>
                  <strong className="whitespace-nowrap text-sm text-brand">{D.billing.lifetimePrice}</strong>
                </div>
                <p className="mt-1 text-[11px] leading-relaxed text-white/45">{D.modal.lifetimeNote}</p>
              </div>
            </div>
            <div className="mt-4 grid gap-2 sm:grid-cols-2">
              {D.billing.included.map((item: string) => (
                <span key={item} className="flex items-center gap-2 text-[11px] text-white/55">
                  <Check aria-hidden="true" className="h-3.5 w-3.5 shrink-0 text-brand" /> {item}
                </span>
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

          <div className="flex flex-col-reverse gap-2 border-t border-white/[0.08] pt-4 sm:flex-row sm:justify-end">
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
