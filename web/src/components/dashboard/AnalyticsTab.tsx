'use client';

import React from 'react';
import { BarChart3 } from 'lucide-react';
import { useDashboard } from './_ctx';

function Area({ data }: { data: number[] }) {
  const width = 720;
  const height = 140;
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = Math.max(1, max - min);
  const points = data.map((value, index) => {
    const x = data.length === 1 ? 0 : (index / (data.length - 1)) * width;
    const y = height - ((value - min) / range) * (height - 24) - 12;
    return { x, y };
  });
  const line = points.map((point) => `${point.x},${point.y}`).join(' ');
  const fill = points.length > 1
    ? `M ${points.map((point) => `${point.x} ${point.y}`).join(' L ')} L ${width} ${height} L 0 ${height} Z`
    : '';

  return (
    <svg viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none" className="h-36 w-full" aria-hidden>
      <defs>
        <linearGradient id="analytics-brand-fill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="var(--brand)" stopOpacity="0.24" />
          <stop offset="100%" stopColor="var(--brand)" stopOpacity="0" />
        </linearGradient>
      </defs>
      {[0.25, 0.5, 0.75].map((grid) => (
        <line key={grid} x1="0" y1={height * grid} x2={width} y2={height * grid} stroke="rgba(255,255,255,0.05)" />
      ))}
      {fill ? <path d={fill} fill="url(#analytics-brand-fill)" /> : null}
      <polyline points={line} fill="none" stroke="var(--brand)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

const average = (values: number[]) => values.reduce((sum, value) => sum + value, 0) / Math.max(1, values.length);

export function AnalyticsTab() {
  const { D, telemetry } = useDashboard();
  const a = D.an;
  const samples = telemetry.slice(-30);
  const charts = [
    { title: a.cOnline, values: samples.map((point) => point.players), unit: '' },
    { title: a.avgTick, values: samples.map((point) => point.tick_rate), unit: 'Hz' },
    { title: a.avgFps, values: samples.map((point) => point.fps), unit: 'FPS' },
    { title: a.avgRam, values: samples.map((point) => point.memory_mb), unit: 'MB' },
  ];

  return (
    <div className="relative space-y-7 animate-fade-in">
      <div>
        <h2 className="flex items-center gap-2 text-xl font-extrabold text-white">
          <BarChart3 className="h-5 w-5 text-brand" />
          {a.title}
        </h2>
        <p className="mt-1 text-xs text-slate-400">{a.sub}</p>
      </div>

      {samples.length === 0 ? (
        <div className="card no-lift grid min-h-[280px] place-items-center p-8 text-center">
          <div>
            <BarChart3 className="mx-auto h-7 w-7 text-white/25" />
            <p className="mt-3 text-sm text-white/45">{a.noData}</p>
          </div>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            {[
              [a.peak, String(Math.max(...samples.map((point) => point.players)))],
              [a.avgTick, `${average(samples.map((point) => point.tick_rate)).toFixed(1)} Hz`],
              [a.avgFps, `${average(samples.map((point) => point.fps)).toFixed(1)} FPS`],
              [a.avgRam, `${Math.round(average(samples.map((point) => point.memory_mb)))} MB`],
            ].map(([label, value]) => (
              <div key={label} className="card p-5">
                <div className="text-[12px] text-white/45">{label}</div>
                <div className="mt-2 text-2xl font-extrabold text-white">{value}</div>
              </div>
            ))}
          </div>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            {charts.map((chart) => (
              <div key={chart.title} className="card p-5">
                <div className="mb-3 flex items-end justify-between gap-4">
                  <span className="text-[13px] font-semibold text-white/70">{chart.title}</span>
                  <span className="font-mono text-[11px] text-white/40">
                    {chart.values[chart.values.length - 1]} {chart.unit}
                  </span>
                </div>
                <Area data={chart.values} />
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
