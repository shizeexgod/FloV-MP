'use client';

import React from 'react';
import { AlertTriangle, RotateCcw, Timer } from 'lucide-react';
import { useDashboard } from './_ctx';

export function WatchdogTab() {
  const { D } = useDashboard();
  const w = D.wd;

  return (
    <div className="relative space-y-7 animate-fade-in">
      <div>
        <h2 className="flex items-center gap-2 text-xl font-extrabold text-white">
          <AlertTriangle className="h-5 w-5 text-brand" />
          {w.title}
        </h2>
        <p className="mt-1 text-xs text-slate-400">{w.sub}</p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="card flex items-start gap-4 p-5">
          <Timer className="mt-0.5 h-5 w-5 flex-none text-brand" />
          <div>
            <div className="text-[12px] text-white/45">{w.threshold}</div>
            <div className="mt-1 text-xl font-extrabold text-white">{w.thresholdVal}</div>
          </div>
        </div>
        <div className="card flex items-start gap-4 p-5">
          <RotateCcw className="mt-0.5 h-5 w-5 flex-none text-brand" />
          <div>
            <div className="text-[12px] text-white/45">{w.restarts}</div>
            <div className="mt-1 text-xl font-extrabold text-white">{w.restartsVal}</div>
          </div>
        </div>
      </div>

      <div className="card no-lift grid min-h-[260px] place-items-center p-8 text-center">
        <div>
          <AlertTriangle className="mx-auto h-7 w-7 text-white/25" />
          <p className="mt-3 text-sm text-white/45">{w.noCrashes}</p>
        </div>
      </div>
    </div>
  );
}
