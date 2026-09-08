'use client';

import React from 'react';
import { CreditCard, Plus, RefreshCw } from 'lucide-react';
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
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge flex flex-col gap-5 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
            <div>
              <h2 className="flex items-center gap-2 text-lg font-black text-white">
                <CreditCard className="h-5 w-5 text-brand" />
                {D.billing.title}
              </h2>
              <p className="mt-1 text-xs text-slate-400">
                {D.billing.sub}
              </p>
            </div>
            <div className="flex gap-2.5">
              <button onClick={loadInvoices} disabled={loadingInvoices} className="btn btn-ghost h-10 px-3.5 text-xs font-semibold">
                <RefreshCw className={`h-4 w-4 ${loadingInvoices ? 'animate-spin text-brand' : ''}`} />
                {D.refresh}
              </button>
              <button onClick={() => setInvoiceOpen(true)} className="btn btn-primary h-10 px-4 text-xs">
                <Plus className="h-4 w-4" />
                {D.billing.issue}
              </button>
            </div>
          </div>

          <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
            <h3 className="mb-4 flex items-center gap-2 text-base font-bold text-white">
              <CreditCard className="h-4 w-4 text-violetx" />
              {D.billing.historyTitle}
            </h3>
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
