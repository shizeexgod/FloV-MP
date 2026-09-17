'use client';

import React from 'react';
import { Activity, Cpu, Gauge, HardDrive, RefreshCw, Users, Zap } from 'lucide-react';
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
  const hasLatest = Boolean(latest);
  const chartData = telemetry.slice(-24);
  const chartMax = Math.max(1, ...chartData.map((point) => point.players));
  const chartPoints = chartData.map((point, index) => ({
    x: chartData.length === 1 ? 0 : (index / (chartData.length - 1)) * 800,
    y: 148 - (point.players / chartMax) * 128,
  }));
  const polyline = chartPoints.map((point) => `${point.x},${point.y}`).join(' ');
  const areaPath = chartPoints.length > 1
    ? `M ${chartPoints.map((point) => `${point.x} ${point.y}`).join(' L ')} L 800 160 L 0 160 Z`
    : '';
  const peakPlayers = chartData.length ? Math.max(...chartData.map((point) => point.players)) : null;

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
                <span className="text-white">{primaryLic?.bound_ip || '—'}</span>
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
            <MetricCard icon={Cpu} label="Tick Rate" value={hasLatest ? String(latest!.tick_rate) : '—'} unit={hasLatest ? 'Hz' : ''} foot={D.tele.tickFoot} />
            <MetricCard icon={Gauge} label="Server FPS" value={hasLatest ? String(latest!.fps) : '—'} unit={hasLatest ? 'FPS' : ''} foot={D.tele.fpsFoot} />
            <MetricCard icon={HardDrive} label="CoreCLR RAM" value={hasLatest ? String(latest!.memory_mb) : '—'} unit={hasLatest ? 'MB' : ''} foot={D.tele.ramFoot} />
            <MetricCard icon={Users} accent label={D.tele.playersLabel} value={hasLatest ? String(latest!.players) : '—'} unit={hasLatest ? `/ ${latest!.max_players}` : ''} foot={D.tele.playersFoot} />
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
              {peakPlayers !== null ? (
                <div className="font-mono text-xs text-white/45">
                  {D.tele.peak}: <strong className="text-brand">{peakPlayers}</strong>
                </div>
              ) : null}
            </div>

            {chartPoints.length > 1 ? (
            <div className="h-44 w-full" aria-label={D.tele.curveTitle}>
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

                <path
                  d={areaPath}
                  fill="url(#curveGradient)"
                />
                <polyline
                  points={polyline}
                  fill="none"
                  stroke="#ff3d8a"
                  strokeWidth="3"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
            </div>
            ) : (
              <p className="py-14 text-center text-xs text-white/35">{D.tele.noPackets}</p>
            )}
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
