'use client';

import React from 'react';
import { ArrowRight, FolderKanban, Infinity as InfinityIcon } from 'lucide-react';
import { useDashboard } from './_ctx';

/**
 * Единый экран-заглушка для разделов, которым нужен активный проект.
 * Показывается вместо пустых виджетов с прочерками, пока у аккаунта
 * нет ни одного созданного проекта.
 */
export function NoProjectGate() {
  const { D, setNewProjOpen } = useDashboard();
  return (
    <section className="mx-auto max-w-2xl overflow-hidden rounded-[20px] border border-white/[0.085] bg-[#111114] shadow-[0_24px_70px_-42px_rgba(0,0,0,0.9)]">
      <div className="flex flex-col gap-6 p-6 sm:p-7">
        <div className="flex items-start gap-4">
          <span className="grid h-11 w-11 shrink-0 place-items-center rounded-[14px] border border-brand/20 bg-brand/[0.07] text-brand">
            <FolderKanban aria-hidden="true" className="h-[18px] w-[18px]" />
          </span>
          <div className="min-w-0 flex-1">
            <div className="mb-2 flex flex-wrap items-center gap-2">
              <span className="inline-flex items-center gap-1.5 rounded-full border border-brand/25 bg-brand/[0.07] px-2.5 py-1 text-[10px] font-bold uppercase tracking-[0.12em] text-brand">
                <InfinityIcon aria-hidden="true" className="h-3 w-3" />
                Lifetime · 25 000 ₽
              </span>
            </div>
            <h2 className="text-pretty text-lg font-bold tracking-[-0.02em] text-white">{D.gate.title}</h2>
            <p className="mt-2 max-w-xl text-pretty text-[13px] leading-6 text-white/48">{D.gate.desc}</p>
          </div>
        </div>

        <hr className="rule-soft" />

        <button
          type="button"
          onClick={() => setNewProjOpen(true)}
          className="group flex h-11 w-full items-center justify-center gap-2 rounded-xl bg-brand px-4 text-[13px] font-bold text-[#16050c] shadow-[0_12px_28px_-16px_rgba(255,61,138,0.75)] transition-[transform,background-color,box-shadow] duration-200 hover:-translate-y-0.5 hover:bg-brand-hover hover:shadow-[0_16px_34px_-16px_rgba(255,61,138,0.82)] active:translate-y-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand/60 focus-visible:ring-offset-2 focus-visible:ring-offset-[#111114]"
        >
          {D.proj.create}
          <ArrowRight aria-hidden="true" className="h-4 w-4 transition-transform duration-200 group-hover:translate-x-0.5" />
        </button>
      </div>
    </section>
  );
}
