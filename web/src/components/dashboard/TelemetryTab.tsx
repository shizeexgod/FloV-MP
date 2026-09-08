'use client';

import React from 'react';
import { Activity, CheckCircle2, RefreshCw, Zap } from 'lucide-react';
import { Spinner } from '@/components/ui';
import { useDashboard, MetricCard, timeShort } from './_ctx';

export function TelemetryTab() {
  const {
    telemetry,
    loadingTelemetry,
    sendingHb,
    loadTelemetry,
    sendHeartbeat,
    D,
    primaryLic,
    latest,
  } = useDashboard();
  return (
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge flex flex-col gap-5 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
            <div>
              <h2 className="flex items-center gap-2 text-lg font-black text-white">
                <Activity className="h-5 w-5 text-brand" />
                {D.tele.title}
              </h2>
              <p className="mt-1 font-mono text-[11px] text-slate-400">
                key: <span className="text-brand">{primaryLic?.license_key || '—'}</span> · VDS:{' '}
                <span className="text-white">{primaryLic?.bound_ip || '188.127.229.224'}</span>
              </p>
            </div>
            <div className="flex gap-2.5">
              <button onClick={loadTelemetry} disabled={loadingTelemetry} className="btn btn-ghost h-10 px-3.5 text-xs font-semibold">
                <RefreshCw className={`h-4 w-4 ${loadingTelemetry ? 'animate-spin text-brand' : ''}`} />
                {D.refresh}
              </button>
              <button
                onClick={sendHeartbeat}
                disabled={sendingHb || !primaryLic}
                className="btn h-10 border border-brand/40 bg-brand/15 px-3.5 text-xs font-bold text-brand transition hover:bg-brand/25 disabled:opacity-50"
              >
                {sendingHb ? <Spinner className="h-4 w-4" /> : <Zap className="h-4 w-4" />}
                {D.tele.testHeartbeat}
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
            <MetricCard icon={Cpu} tone="text-emeraldx" ring="border-emeraldx/40 bg-emeraldx/10" label="Tick Rate" value={`${latest?.tick_rate ?? 60}.0`} unit="Hz" foot={D.tele.tickFoot} />
            <MetricCard icon={Gauge} tone="text-brand" ring="border-brand/40 bg-brand/10" label="Server FPS" value={`${latest?.fps ?? 60}.0`} unit="FPS" foot={D.tele.fpsFoot} />
            <MetricCard icon={HardDrive} tone="text-cyber" ring="border-cyber/40 bg-cyber/10" label="CoreCLR RAM" value={`${latest?.memory_mb ?? 248}`} unit="MB" foot={D.tele.ramFoot} />
            <MetricCard icon={Users} tone="text-violetx" ring="border-violetx/40 bg-violetx/10" label={D.tele.playersLabel} value={`${latest?.players ?? 1}`} unit={`/ ${primaryLic?.max_players ?? 1500}`} foot={D.tele.playersFoot} />
          </div>

          {/* 24-Hour Activity Chart & SLA */}
          <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
            <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h3 className="flex items-center gap-2 text-base font-bold text-white">
                  <Activity className="h-5 w-5 text-brand" />
                  {D.tele.curveTitle}
                </h3>
                <p className="mt-0.5 text-xs text-slate-400">
                  {D.tele.curveSub}
                </p>
              </div>
              <div className="flex items-center gap-3 font-mono text-xs">
                <span className="flex items-center gap-1.5 rounded-full border border-emeraldx/30 bg-emeraldx/15 px-3 py-1 text-emeraldx font-bold">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  SLA: 99.98%
                </span>
                <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-slate-300">
                  {D.tele.peak}: <strong className="text-brand">{D.tele.peakVal}</strong>
                </span>
              </div>
            </div>

            {/* SVG Activity Curve */}
            <div className="h-44 w-full">
              <svg className="h-full w-full overflow-visible" viewBox="0 0 800 160" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="curveGradient" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#ff3d8a" stopOpacity="0.4" />
                    <stop offset="100%" stopColor="#ff3d8a" stopOpacity="0.0" />
                  </linearGradient>
                </defs>
                {/* Grid Lines */}
                <line x1="0" y1="40" x2="800" y2="40" stroke="rgba(255,255,255,0.06)" strokeDasharray="4 4" />
                <line x1="0" y1="80" x2="800" y2="80" stroke="rgba(255,255,255,0.06)" strokeDasharray="4 4" />
                <line x1="0" y1="120" x2="800" y2="120" stroke="rgba(255,255,255,0.06)" strokeDasharray="4 4" />

                {/* Filled Area */}
                <path
                  d="M0,130 C70,120 120,140 180,110 C240,70 300,90 360,60 C420,30 480,45 540,25 C600,10 660,35 720,20 C760,10 790,25 800,30 L800,160 L0,160 Z"
                  fill="url(#curveGradient)"
                />
                {/* Stroke Line */}
                <path
                  d="M0,130 C70,120 120,140 180,110 C240,70 300,90 360,60 C420,30 480,45 540,25 C600,10 660,35 720,20 C760,10 790,25 800,30"
                  fill="none"
                  stroke="#ff3d8a"
                  strokeWidth="3"
                  strokeLinecap="round"
                />

                {/* Peak Dot */}
                <circle cx="600" cy="10" r="5" fill="#ff3d8a" className="animate-pulse" />
                <circle cx="600" cy="10" r="10" fill="none" stroke="#ff3d8a" strokeWidth="1.5" strokeOpacity="0.5" />
              </svg>
            </div>

            {/* Time markers */}
            <div className="mt-3 flex justify-between font-mono text-[10px] text-slate-500">
              <span>00:00 ({D.tele.night})</span>
              <span>04:00</span>
              <span>08:00 ({D.tele.morning})</span>
              <span>12:00 ({D.tele.day})</span>
              <span>16:00</span>
              <span>20:00 ({D.tele.prime})</span>
              <span>23:59</span>
            </div>
          </div>

          <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
            <h3 className="mb-4 flex items-center gap-2 text-base font-bold text-white">
              <Activity className="h-4 w-4 text-cyber" />
              {D.tele.historyTitle}
            </h3>
            {telemetry.length === 0 ? (
              <p className="py-10 text-center text-xs text-slate-500">
                {D.tele.noPackets}
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-left font-mono text-xs">
                  <thead>
                    <tr className="border-b border-white/[0.08] uppercase tracking-wider text-slate-500">
                      <th className="pb-3 pr-3 font-semibold">{D.tele.thTime}</th>
                      <th className="pb-3 pr-3 font-semibold">{D.tele.thNodeIp}</th>
                      <th className="pb-3 pr-3 font-semibold">{D.tele.thOnline}</th>
                      <th className="pb-3 pr-3 font-semibold">Tick</th>
                      <th className="pb-3 pr-3 font-semibold">FPS</th>
                      <th className="pb-3 pr-3 font-semibold">{D.tele.thRam}</th>
                      <th className="pb-3 pr-3 font-semibold">{D.proj.thStatus}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/[0.05] text-slate-300">
                    {[...telemetry].reverse().map((pt) => (
                      <tr key={pt.id} className="transition-colors hover:bg-white/[0.03]">
                        <td className="py-2.5 pr-3 text-white">{timeShort(pt.recorded_at)}</td>
                        <td className="py-2.5 pr-3 text-slate-400">{pt.server_ip}</td>
                        <td className="py-2.5 pr-3 font-bold text-brand">{pt.players} / {pt.max_players}</td>
                        <td className="py-2.5 pr-3 text-emeraldx">{pt.tick_rate} Hz</td>
                        <td className="py-2.5 pr-3">{pt.fps}</td>
                        <td className="py-2.5 pr-3 text-cyber">{pt.memory_mb} MB</td>
                        <td className="py-2.5 pr-3">
                          <span className="rounded bg-emeraldx/15 px-2 py-0.5 text-[10px] text-emeraldx">OK</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
  );
}
