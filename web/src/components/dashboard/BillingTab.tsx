'use client';

import React from 'react';
import { Check, CreditCard, RefreshCw } from 'lucide-react';
import { Spinner, Badge } from '@/components/ui';
import { useDashboard, dateShort } from './_ctx';

export function BillingTab() {
  const {
    invoices,
    loadingInvoices,
    payingId,
    setInvoiceOpen,
    loadInvoices,
    payInvoice,
    D,
  } = useDashboard();
  return (
        <div className="relative space-y-5 animate-fade-in">
          <div className="grid gap-4 xl:grid-cols-[0.85fr_1.15fr]">
            <section className="card card-edge border-brand/25 bg-brand/[0.055] p-6">
              <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-brand">{D.billing.lifetimeEyebrow}</p>
              <h2 className="mt-3 text-xl font-extrabold text-white">{D.billing.title}</h2>
              <p className="mt-2 max-w-md text-xs leading-relaxed text-white/45">{D.billing.lifetimeNote}</p>
              <div className="mt-7 text-3xl font-extrabold text-white tabular-nums">{D.billing.lifetimePrice}</div>
              <button onClick={() => setInvoiceOpen(true)} className="btn btn-primary mt-5 h-10 w-full px-4 text-xs sm:w-auto">
                <CreditCard aria-hidden="true" className="h-4 w-4" />
                {D.billing.issue}
              </button>
            </section>

            <section className="card card-edge p-6">
              <h3 className="text-sm font-bold text-white/80">{D.billing.sub}</h3>
              <div className="mt-5 grid gap-3 sm:grid-cols-2">
                {D.billing.included.map((item: string) => (
                  <div key={item} className="flex items-center gap-3 rounded-xl border border-white/[0.07] bg-white/[0.02] p-3 text-xs font-semibold text-white/60">
                    <span className="grid h-7 w-7 shrink-0 place-items-center rounded-lg bg-brand/10 text-brand">
                      <Check aria-hidden="true" className="h-3.5 w-3.5" />
                    </span>
                    {item}
                  </div>
                ))}
              </div>
            </section>
          </div>

          <div className="glass-panel card-edge rounded-2xl p-5 sm:p-6">
            <div className="mb-4 flex items-center justify-between gap-3">
              <h3 className="flex items-center gap-2 text-sm font-bold text-white">
                <CreditCard aria-hidden="true" className="h-4 w-4 text-brand" />
                {D.billing.historyTitle}
              </h3>
              <button onClick={loadInvoices} disabled={loadingInvoices} className="btn btn-ghost h-9 px-3 text-xs font-semibold">
                <RefreshCw aria-hidden="true" className={`h-3.5 w-3.5 ${loadingInvoices ? 'animate-spin text-brand' : ''}`} />
                {D.refresh}
              </button>
            </div>
            {invoices.length === 0 ? (
              <div className="py-10 text-center text-xs text-slate-500">
                <p>{D.billing.noInvoices}</p>
                <button onClick={() => setInvoiceOpen(true)} className="btn btn-primary mt-3 h-9 px-4 text-xs">
                  {D.billing.issueShort}
                </button>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[720px] text-left text-xs">
                  <thead>
                    <tr className="border-b border-white/[0.08] font-mono uppercase tracking-wider text-slate-500">
                      <th className="pb-3 pr-3 font-semibold">{D.billing.thNo}</th>
                      <th className="pb-3 pr-3 font-semibold">{D.billing.thPlan}</th>
                      <th className="pb-3 pr-3 font-semibold">{D.billing.thAmount}</th>
                      <th className="pb-3 pr-3 font-semibold">{D.billing.thMethod}</th>
                      <th className="pb-3 pr-3 font-semibold">{D.billing.thCreated}</th>
                      <th className="pb-3 pr-3 font-semibold">{D.proj.thStatus}</th>
                      <th className="pb-3 pr-3 text-right font-semibold">{D.billing.thAction}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/[0.05] text-slate-300">
                    {invoices.map((inv) => {
                      const paid = inv.status === 'paid';
                      return (
                        <tr key={inv.id} className="transition-colors hover:bg-white/[0.03]">
                          <td className="py-3 pr-3 font-mono font-bold text-white">#{inv.id}</td>
                          <td className="py-3 pr-3 font-mono uppercase text-brand">{inv.plan}</td>
                          <td className="py-3 pr-3 font-mono text-sm font-bold text-white">
                            {Number(inv.amount_rub).toLocaleString('ru-RU')} ₽
                          </td>
                          <td className="py-3 pr-3 font-mono capitalize text-slate-400">{inv.payment_method}</td>
                          <td className="py-3 pr-3 font-mono text-slate-400">{dateShort(inv.created_at)}</td>
                          <td className="py-3 pr-3">
                            <Badge tone={paid ? 'emerald' : 'amber'}>{paid ? D.billing.paid : D.billing.pending}</Badge>
                          </td>
                          <td className="py-3 pr-3 text-right">
                            {paid ? (
                              <span className="text-[11px] text-slate-600">{D.billing.closed}</span>
                            ) : (
                              <button
                                onClick={() => payInvoice(inv.id)}
                                disabled={payingId === inv.id}
                                className="btn h-8 bg-emeraldx px-3 text-[11px] font-bold text-ink-950 transition hover:brightness-110 disabled:opacity-50"
                              >
                                {payingId === inv.id ? <Spinner className="h-3.5 w-3.5" /> : D.billing.pay}
                              </button>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
  );
}
