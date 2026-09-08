'use client';

import React from 'react';
import { BarChart3 } from 'lucide-react';
import { useDashboard } from './_ctx';

const ONLINE = [0.32, 0.28, 0.24, 0.22, 0.26, 0.34, 0.48, 0.62, 0.71, 0.78, 0.83, 0.86, 0.84, 0.8, 0.77, 0.79, 0.85, 0.92, 0.97, 1.0, 0.95, 0.82, 0.64, 0.45];
const CPU = [0.4, 0.38, 0.36, 0.35, 0.37, 0.42, 0.5, 0.58, 0.63, 0.67, 0.7, 0.72, 0.71, 0.69, 0.66, 0.68, 0.72, 0.79, 0.84, 0.82, 0.75, 0.62, 0.5, 0.44];
const RAM = [0.55, 0.55, 0.54, 0.54, 0.55, 0.57, 0.6, 0.62, 0.64, 0.66, 0.67, 0.68, 0.68, 0.67, 0.66, 0.67, 0.69, 0.71, 0.73, 0.72, 0.7, 0.65, 0.6, 0.57];
const NET = [0.2, 0.18, 0.16, 0.15, 0.17, 0.24, 0.36, 0.5, 0.6, 0.66, 0.72, 0.75, 0.73, 0.7, 0.66, 0.69, 0.75, 0.84, 0.92, 0.96, 0.88, 0.7, 0.5, 0.34];

function Area({ data, color }: { data: number[]; color: string }) {
  const w = 720;
  const h = 140;
  const step = w / (data.length - 1);
  const pts = data.map((v, i) => `${i * step},${h - v * (h - 12) - 6}`);
  const line = `M${pts.join(' L')}`;
  const fill = `${line} L${w},${h} L0,${h} Z`;
  return (
    <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" className="h-36 w-full">
      <defs>
        <linearGradient id={`g${color.replace('#', '')}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity="0.28" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      {[0.25, 0.5, 0.75].map((g) => (
        <line key={g} x1="0" y1={h * g} x2={w} y2={h * g} stroke="rgba(255,255,255,0.05)" strokeDasharray="4 4" />
      ))}
      <path d={fill} fill={`url(#g${color.replace('#', '')})`} />
      <path d={line} fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function Donut({ value, color, label }: { value: number; color: string; label: string }) {
  const r = 34;
  const c = 2 * Math.PI * r;
  return (
    <div className="flex items-center gap-4">
      <svg viewBox="0 0 80 80" className="h-20 w-20 -rotate-90">
        <circle cx="40" cy="40" r={r} fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="9" />
        <circle
          cx="40"
          cy="40"
          r={r}
          fill="none"
          stroke={color}
          strokeWidth="9"
          strokeLinecap="round"
          strokeDasharray={`${value * c} ${c}`}
        />
      </svg>
      <div>
        <div className="text-[15px] font-bold text-white">{Math.round(value * 100)}%</div>
        <div className="text-[12px] font-medium text-white/70">{label}</div>
      </div>
    </div>
  );
}

export function AnalyticsTab() {
  const { D } = useDashboard();
  const a = D.an;
  const charts: [string, number[], string][] = [
    [a.cOnline, ONLINE, '#ff3d8a'],
    [a.cCpu, CPU, '#7c86f5'],
    [a.cRam, RAM, '#3fb984'],
    [a.cNet, NET, '#ff3d8a'],
  ];
  return (
    <div className="relative space-y-4 animate-fade-in">
      <div>
        <h2 className="flex items-center gap-2 text-lg font-black text-white">
          <BarChart3 className="h-5 w-5 text-brand" />
          {a.title}
        </h2>
        <p className="mt-1 text-xs text-slate-400">{a.sub}</p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {[
          [a.peak, a.peakVal],
          [a.avg, a.avgVal],
          [a.sla, a.slaVal],
        ].map(([l, v]) => (
          <div key={l} className="glass card-edge rounded-2xl p-5">
            <div className="text-[12px] text-white/45">{l}</div>
            <div className="mt-1.5 text-2xl font-black text-white">{v}</div>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {charts.map(([t, d, c]) => (
          <div key={t} className="glass card-edge rounded-2xl p-5">
            <div className="mb-3 flex items-center justify-between text-[12px] text-white/45">
              <span>{t}</span>
              <span className="font-mono text-[10px] text-white/25">{a.h24}</span>
            </div>
            <Area data={d} color={c} />
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {[
          [a.gCpu, 0.61, '#ff3d8a'],
          [a.gRam, 0.68, '#7c86f5'],
          [a.gNet, 0.44, '#3fb984'],
        ].map(([l, v, c]) => (
          <div key={l as string} className="glass card-edge rounded-2xl p-5">
            <Donut value={v as number} color={c as string} label={l as string} />
            <div className="mt-2 font-mono text-[11px] text-white/35">{a.gFoot}</div>
          </div>
        ))}
      </div>
    </div>
  );
}
