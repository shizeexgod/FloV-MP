'use client';

import React, { useState } from 'react';
import { ScrollText, Search } from 'lucide-react';
import { useDashboard } from './_ctx';

const LEVELS = ['ALL', 'INFO', 'OK', 'WARN', 'ERROR', 'CRASH'] as const;

export function LogsTab() {
  const { D } = useDashboard();
  const [q, setQ] = useState('');
  const [lvl, setLvl] = useState<(typeof LEVELS)[number]>('ALL');

  const rows: [string, string, string, string][] = D.logs.rows;
  const filtered = rows.filter(
    (r) =>
      (lvl === 'ALL' || r[1] === lvl) &&
      (q === '' || (r[3] + ' ' + r[2]).toLowerCase().includes(q.toLowerCase()))
  );

  const col = (l: string) =>
    l === 'ERROR' || l === 'CRASH' ? '#e5484d' : l === 'WARN' ? '#d8a13a' : l === 'OK' ? '#3fb984' : '#8a93a1';

  return (
    <div className="relative space-y-4 animate-fade-in">
      <div>
        <h2 className="flex items-center gap-2 text-lg font-black text-white">
          <ScrollText className="h-5 w-5 text-brand" />
          {D.logs.title}
        </h2>
        <p className="mt-1 text-xs text-slate-400">{D.logs.sub}</p>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <div className="flex h-10 flex-1 items-center gap-2 rounded-lg border border-white/10 bg-white/[0.02] px-3">
          <Search className="h-3.5 w-3.5 text-white/35" />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={D.logs.search}
            className="flex-1 bg-transparent text-[12.5px] text-white outline-none placeholder:text-white/25"
          />
        </div>
        {LEVELS.map((l) => (
          <button
            key={l}
            onClick={() => setLvl(l)}
            className={`h-10 rounded-lg px-2.5 font-mono text-[10px] font-bold transition ${
              lvl === l ? 'bg-brand text-[#08080a]' : 'border border-white/10 bg-white/[0.03] text-white/45'
            }`}
          >
            {l}
          </button>
        ))}
      </div>

      <div className="overflow-hidden rounded-2xl border border-white/[0.08]">
        <table className="w-full text-left font-mono text-[11.5px]">
          <tbody className="divide-y divide-white/[0.05] text-slate-300">
            {filtered.map((r, i) => (
              <tr key={i} className="transition-colors hover:bg-white/[0.03]">
                <td className="whitespace-nowrap px-4 py-2.5 text-white/30">{r[0]}</td>
                <td className="px-3 py-2.5" style={{ color: col(r[1]) }}>{r[1]}</td>
                <td className="px-3 py-2.5 text-white/35">{r[2]}</td>
                <td className="px-4 py-2.5 text-white/70">{r[3]}</td>
              </tr>
            ))}
            {filtered.length === 0 && (
              <tr>
                <td className="px-4 py-8 text-center text-white/30" colSpan={4}>
                  {D.logs.notFound}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
