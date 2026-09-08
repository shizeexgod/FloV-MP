'use client';

import React from 'react';
import { AlertTriangle, Terminal } from 'lucide-react';
import { useDashboard } from './_ctx';

export function WatchdogTab() {
  const { D } = useDashboard();
  const w = D.wd;
  return (
    <div className="relative space-y-6 animate-fade-in">
      <div>
        <h2 className="flex items-center gap-2 text-lg font-black text-white">
          <AlertTriangle className="h-5 w-5 text-brand" />
          {w.title}
        </h2>
        <p className="mt-1 text-xs text-slate-400">{w.sub}</p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {[
          [w.threshold, w.thresholdVal],
          [w.restarts, w.restartsVal],
          [w.uptime, w.uptimeVal],
        ].map(([l, v]) => (
          <div key={l} className="glass card-edge rounded-2xl p-5">
            <div className="font-mono text-[10px] uppercase tracking-wider text-slate-500">{l}</div>
            <div className="mt-1.5 text-2xl font-black text-white">{v}</div>
          </div>
        ))}
      </div>

      <div className="overflow-hidden rounded-2xl border border-white/[0.08]">
        <div className="border-b border-white/[0.07] px-4 py-2.5 text-[12px] text-white/50">{w.crashLog}</div>
        <table className="w-full text-left text-xs">
          <thead>
            <tr className="border-b border-white/[0.07] font-mono uppercase tracking-wider text-slate-500">
              <th className="px-4 py-2.5 font-semibold">{w.thTime}</th>
              <th className="px-4 py-2.5 font-semibold">{w.thServer}</th>
              <th className="px-4 py-2.5 font-semibold">{w.thReason}</th>
              <th className="px-4 py-2.5 font-semibold">{w.thRestarted}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-white/[0.05] text-slate-300">
            {w.rows.map((r: [string, string, string, boolean], i: number) => (
              <tr key={i} className="transition-colors hover:bg-white/[0.03]">
                <td className="whitespace-nowrap px-4 py-3 font-mono text-slate-500">{r[0]}</td>
                <td className="px-4 py-3 font-mono text-white/80">{r[1]}</td>
                <td className="px-4 py-3">{r[2]}</td>
                <td className="px-4 py-3">
                  <span
                    className={`rounded-full border px-2 py-0.5 text-[10px] ${
                      r[3] ? 'border-emeraldx/30 text-emeraldx' : 'border-red-500/30 text-red-400'
                    }`}
                  >
                    {r[3] ? w.ok : w.failed}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="glass card-edge rounded-2xl p-5">
        <div className="mb-2 flex items-center gap-2 text-[12px] text-white/50">
          <Terminal className="h-3.5 w-3.5" /> {D.wd.dumpTitle}
        </div>
        <pre className="terminal-scroll overflow-x-auto rounded-lg bg-black/40 p-4 font-mono text-[11px] leading-relaxed text-slate-300">
{`Unhandled exception. System.AccessViolationException
  at FloVMP.Gamemode.Econ.PayrollTick(Int32 batch)
  at FloVMP.Core.Scheduler.RunResourceTick(Resource r)
  at FloVMP.Core.Loop.Tick() in Loop.cs:line 214
  -> watchdog: SIGSEGV captured, core dumped /var/crash/econ_core_0912.dmp
  -> autorestart: florida-prod-02 back online in 6.1s`}
        </pre>
      </div>
    </div>
  );
}
