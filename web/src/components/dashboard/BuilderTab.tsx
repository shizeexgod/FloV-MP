'use client';

import React from 'react';
import { Check, CircleDot, Download, Layers, Rocket, ShieldCheck } from 'lucide-react';
import { Spinner, FieldLabel } from '@/components/ui';
import { useDashboard, COLOR_PRESETS } from './_ctx';

export function BuilderTab() {
  const {
    licenses,
    bProject,
    setBProject,
    bColor,
    setBColor,
    bIp,
    setBIp,
    bPort,
    setBPort,
    building,
    buildStage,
    buildResult,
    buildLauncher,
    D,
    BUILD_STAGES,
  } = useDashboard();
  return (
        <div className="relative grid grid-cols-1 gap-8 animate-fade-in lg:grid-cols-2">
          <div className="glass-panel card-edge rounded-3xl p-7 shadow-glass sm:p-8">
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl border border-cyber/30 bg-cyber/10 text-cyber">
                <Layers className="h-5 w-5" />
              </span>
              <div>
                <h2 className="text-lg font-bold text-white">{D.builder.title}</h2>
                <p className="text-xs text-slate-400">{D.builder.sub}</p>
              </div>
            </div>

            <form onSubmit={buildLauncher} className="mt-6 space-y-5">
              <div>
                <FieldLabel>{D.builder.projectName}</FieldLabel>
                <input
                  required
                  value={bProject}
                  onChange={(e) => setBProject(e.target.value)}
                  placeholder="Florida V"
                  className="field h-11 px-4"
                />
              </div>

              <div>
                <FieldLabel>{D.builder.hexColor}</FieldLabel>
                <div className="flex items-center gap-3">
                  <input
                    type="color"
                    value={bColor}
                    onChange={(e) => setBColor(e.target.value)}
                    className="h-11 w-14 cursor-pointer rounded-xl border border-white/10 bg-ink-950/60"
                  />
                  <input
                    value={bColor}
                    onChange={(e) => setBColor(e.target.value)}
                    className="field h-11 flex-1 px-4 font-mono"
                  />
                </div>
                <div className="mt-2.5 flex flex-wrap gap-1.5">
                  {COLOR_PRESETS.map((p) => (
                    <button
                      key={p.hex}
                      type="button"
                      onClick={() => setBColor(p.hex)}
                      className={`flex items-center gap-1.5 rounded-lg border px-2 py-1 text-[10px] font-semibold transition ${
                        bColor.toLowerCase() === p.hex.toLowerCase()
                          ? 'border-white/30 bg-white/10 text-white'
                          : 'border-white/10 text-slate-400 hover:border-white/20'
                      }`}
                    >
                      <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: p.hex }} />
                      {p.name}
                    </button>
                  ))}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <FieldLabel>{D.builder.serverIp}</FieldLabel>
                  <input required value={bIp} onChange={(e) => setBIp(e.target.value)} className="field h-11 px-4 font-mono" />
                </div>
                <div>
                  <FieldLabel>{D.builder.udpPort}</FieldLabel>
                  <input required value={bPort} onChange={(e) => setBPort(e.target.value)} className="field h-11 px-4 font-mono" />
                </div>
              </div>

              <button
                type="submit"
                disabled={building || licenses.length === 0}
                className="btn btn-primary h-11 w-full text-sm disabled:opacity-50"
              >
                {building ? <Spinner className="h-4 w-4" /> : <ShieldCheck className="h-4 w-4" />}
                {building ? D.builder.compiling : D.builder.compile}
              </button>
            </form>
          </div>

          <div className="glass-panel card-edge flex flex-col rounded-3xl p-7 shadow-glass sm:p-8">
            <h3 className="flex items-center gap-2 text-base font-bold text-white">
              <Rocket className="h-4 w-4 text-brand" />
              {D.builder.statusTitle}
            </h3>

            {building || buildStage > 0 ? (
              <div className="mt-5 space-y-2.5">
                {BUILD_STAGES.map((s, i) => {
                  const state = buildStage > i ? 'done' : buildStage === i ? 'active' : 'idle';
                  return (
                    <div
                      key={s}
                      className={`flex items-center gap-3 rounded-xl border px-4 py-2.5 text-xs transition-all ${
                        state === 'done'
                          ? 'border-emeraldx/20 bg-emeraldx/[0.06] text-emeraldx'
                          : state === 'active'
                          ? 'border-brand/30 bg-brand/[0.08] text-white'
                          : 'border-white/[0.06] bg-white/[0.02] text-slate-500'
                      }`}
                    >
                      {state === 'done' ? (
                        <Check className="h-4 w-4" />
                      ) : state === 'active' ? (
                        <Spinner className="h-4 w-4" />
                      ) : (
                        <CircleDot className="h-4 w-4" />
                      )}
                      {s}
                    </div>
                  );
                })}
              </div>
            ) : null}

            {buildResult ? (
              <div className="mt-5 space-y-4 rounded-2xl border border-emeraldx/25 bg-emeraldx/[0.06] p-5 text-xs animate-fade-in">
                <div className="flex items-center gap-2 font-bold text-emeraldx">
                  <Check className="h-5 w-5" />
                  {D.builder.built} (Build #{buildResult.buildId})
                </div>
                <div className="space-y-1.5 font-mono text-slate-300">
                  <div>{D.builder.bProject}: <strong className="text-white">{buildResult.projectName}</strong></div>
                  <div className="flex items-center gap-1.5">
                    {D.builder.bColor}:
                    <span className="inline-block h-3 w-3 rounded-full" style={{ backgroundColor: buildResult.primaryColor }} />
                    <span style={{ color: buildResult.primaryColor }}>{buildResult.primaryColor}</span>
                  </div>
                  <div>{D.builder.bEndpoint}: <strong className="text-white">{buildResult.config.serverIp}:{buildResult.config.serverPort}</strong></div>
                  <div>{D.builder.bLicense}: <strong className="text-white">{buildResult.config.licenseKey}</strong></div>
                </div>
                <a href={buildResult.downloadUrl} download className="btn h-10 w-full bg-emeraldx text-xs font-bold text-ink-950 transition hover:brightness-110">
                  <Download className="h-4 w-4" />
                  {D.builder.download} {buildResult.projectName}-Setup.exe
                </a>
              </div>
            ) : !building && buildStage === 0 ? (
              <div className="mt-5 rounded-2xl border border-white/[0.06] bg-white/[0.02] p-8 text-center text-xs text-slate-500">
                <Layers className="mx-auto h-10 w-10 text-slate-600" />
                <p className="mt-3 font-semibold text-slate-300">{D.builder.waiting}</p>
                <p className="mt-1">
                  {D.builder.waitingSub}
                </p>
              </div>
            ) : null}

            <div className="mt-6 space-y-2 border-t border-white/[0.08] pt-5 font-mono text-[11px] text-slate-400">
              {D.builder.feat.map((x: string) => (
                <div key={x} className="flex items-center gap-2">
                  <Check className="h-3.5 w-3.5 text-brand" />
                  {x}
                </div>
              ))}
            </div>
          </div>
        </div>
  );
}
