'use client';

import React from 'react';
import { Check, CircleDot, Copy, Download, Eye, EyeOff, Globe, KeyRound, Rocket, Server, Settings2, ShieldCheck, Terminal } from 'lucide-react';
import { Badge } from '@/components/ui';
import { useDashboard, planTone, dateShort } from './_ctx';

export function OverviewTab() {
  const {
    licenses,
    showKeyId,
    setShowKeyId,
    copied,
    setNewLicOpen,
    copy,
    openIpModal,
    D,
    primaryLic,
    isIpBound,
    onboarding,
  } = useDashboard();
  return (
        <div className="relative space-y-8 animate-fade-in">
          {/* Onboarding */}
          <div className="glass card-edge rounded-3xl p-6">
            <div className="mb-4 flex items-center gap-2">
              <Rocket className="h-4 w-4 text-brand" />
              <h3 className="font-mono text-[11px] font-bold uppercase tracking-widest text-brand">
                {D.overview.quickStart}
              </h3>
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {onboarding.map((s) => (
                <div
                  key={s.title}
                  className="flex items-center gap-3 rounded-2xl border border-white/[0.06] bg-white/[0.02] p-4"
                >
                  {s.done ? (
                    <Check className="h-5 w-5 shrink-0 text-emeraldx" />
                  ) : (
                    <CircleDot className={`h-5 w-5 shrink-0 ${s.warn ? 'text-amber-400' : 'text-slate-500'}`} />
                  )}
                  <div className="min-w-0">
                    <div className="text-xs font-bold text-white">{s.title}</div>
                    <div className="truncate text-[11px] text-slate-400">{s.note}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Licenses */}
          <div>
            <div className="mb-5 flex items-end justify-between">
              <div>
                <h2 className="flex items-center gap-2 text-lg font-black text-white">
                  <KeyRound className="h-5 w-5 text-brand" />
                  {D.overview.yourLicenses}
                </h2>
                <p className="mt-1 text-xs text-slate-400">
                  {D.overview.licSub}
                </p>
              </div>
              <span className="font-mono text-xs text-slate-500">{D.overview.total}: {licenses.length}</span>
            </div>

            {licenses.length === 0 ? (
              <div className="glass card-edge rounded-3xl p-12 text-center">
                <KeyRound className="mx-auto h-10 w-10 text-slate-600" />
                <p className="mt-3 font-medium text-slate-300">{D.overview.noLicenses}</p>
                <button onClick={() => setNewLicOpen(true)} className="btn btn-primary mt-4 h-10 px-4 text-xs">
                  {D.overview.createFirst}
                </button>
              </div>
            ) : (
              <div className="space-y-5">
                {licenses.map((lic) => {
                  const expired = new Date(lic.expires_at).getTime() < Date.now();
                  const active = lic.is_active && !expired;
                  const revealed = showKeyId === lic.id;
                  return (
                    <div key={lic.id} className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-7">
                      <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                        <div className="min-w-0 flex-1 space-y-3">
                          <div className="flex flex-wrap items-center gap-2.5">
                            <h3 className="text-[17px] font-bold text-white">{lic.server_name}</h3>
                            <Badge tone={planTone(lic.plan)}>{D.overview.planLabel}: {lic.plan}</Badge>
                            <span
                              className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 font-mono text-[10px] font-bold uppercase ${
                                active
                                  ? 'border-emeraldx/30 bg-emeraldx/15 text-emeraldx'
                                  : 'border-red-500/30 bg-red-500/15 text-red-400'
                              }`}
                            >
                              <span className={`h-1.5 w-1.5 rounded-full ${active ? 'bg-emeraldx animate-pulse' : 'bg-red-400'}`} />
                              {active ? D.overview.active : D.overview.suspended}
                            </span>
                          </div>

                          {/* key */}
                          <div className="flex max-w-xl items-center gap-2">
                            <div className="flex flex-1 items-center justify-between gap-2 rounded-xl border border-white/10 bg-ink-950/60 px-4 py-2.5 font-mono text-sm text-slate-200">
                              <span className="tracking-wider">
                                {revealed ? lic.license_key : `${lic.license_key.slice(0, 4)}-••••-••••-••••`}
                              </span>
                              <button
                                onClick={() => setShowKeyId(revealed ? null : lic.id)}
                                className="text-slate-500 transition hover:text-white"
                                title={revealed ? D.overview.hideKey : D.overview.showKey}
                              >
                                {revealed ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                              </button>
                            </div>
                            <button
                              onClick={() => copy(lic.license_key)}
                              className="btn btn-ghost h-[42px] w-[42px] shrink-0 p-0"
                              title={D.overview.copyKey}
                            >
                              {copied === lic.license_key ? (
                                <Check className="h-4 w-4 text-emeraldx" />
                              ) : (
                                <Copy className="h-4 w-4" />
                              )}
                            </button>
                          </div>

                          <div className="flex flex-wrap items-center gap-x-5 gap-y-2 pt-1 font-mono text-[11px] text-slate-400">
                            <span className="flex items-center gap-1.5">
                              <Globe className="h-3.5 w-3.5 text-brand" />
                              IP: <strong className="text-white">{lic.bound_ip === '0.0.0.0' ? D.overview.ipAny : lic.bound_ip}</strong>
                            </span>
                            <span className="flex items-center gap-1.5">
                              <Server className="h-3.5 w-3.5 text-cyber" />
                              {D.overview.slotsShort}: <strong className="text-white">{lic.max_players}</strong>
                            </span>
                            <span className="flex items-center gap-1.5">
                              <ShieldCheck className="h-3.5 w-3.5 text-violetx" />
                              {D.overview.expires}: <strong className="text-white">{dateShort(lic.expires_at)}</strong>
                            </span>
                          </div>
                        </div>

                        <div className="flex shrink-0 gap-2.5">
                          <button onClick={() => openIpModal(lic)} className="btn btn-ghost h-10 px-3.5 text-xs font-semibold">
                            <Settings2 className="h-4 w-4 text-brand" />
                            {D.overview.configureIp}
                          </button>
                          <a
                            href="/cdn/FloVMP-Server-x64-Linux.tar.gz"
                            download
                            className="btn btn-ghost h-10 px-3.5 text-xs font-semibold"
                          >
                            <Download className="h-4 w-4 text-emeraldx" />
                            {D.overview.server}
                          </a>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* server.toml guide */}
          <div className="glass card-edge rounded-3xl p-6 sm:p-8">
            <h3 className="flex items-center gap-2 text-base font-bold text-white">
              <Terminal className="h-5 w-5 text-brand" />
              {D.overview.tomlTitle} <code className="rounded bg-white/5 px-1.5 py-0.5 font-mono text-xs text-brand">server.toml</code>
            </h3>
            <ol className="mt-4 space-y-3 text-[13px] text-slate-300">
              <li>
                <strong className="text-white">1.</strong> {D.overview.tomlStep1}
              </li>
              <li>
                <strong className="text-white">2.</strong> {D.overview.tomlStep2} <code className="font-mono text-xs text-brand">server.toml</code> {D.overview.tomlStep2b}
                <pre className="mt-2 overflow-x-auto rounded-xl bg-ink-950/70 p-3 font-mono text-[12px] text-slate-200">
{`[licensing]
key      = "${primaryLic?.license_key || 'FLV-XXXX-XXXX-XXXX'}"
bound_ip = "${isIpBound ? primaryLic!.bound_ip : '188.127.229.224'}"`}
                </pre>
              </li>
              <li>
                <strong className="text-white">3.</strong> {D.overview.tomlStep3a} <code className="font-mono text-xs text-emeraldx">./start.sh</code> {D.overview.tomlStep3b}
              </li>
            </ol>
          </div>
        </div>
  );
}
