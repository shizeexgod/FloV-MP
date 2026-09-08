'use client';

import React from 'react';
import { AlertTriangle, Zap } from 'lucide-react';
import { Spinner, Badge, FieldLabel } from '@/components/ui';
import { useDashboard } from './_ctx';

export function TroubleshootTab() {
  const {
    troubleshootText,
    setTroubleshootText,
    diagnosing,
    diagnosticResult,
    runTroubleshoot,
    D,
  } = useDashboard();
  return (
        <div className="relative space-y-8 animate-fade-in">
          <div className="glass-panel card-edge flex flex-col gap-4 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between sm:p-8">
            <div>
              <div className="flex items-center gap-2">
                <Zap className="h-5 w-5 text-brand" />
                <h2 className="text-lg font-black text-white">{D.ai.title}</h2>
              </div>
              <p className="mt-1 text-xs text-slate-400">
                {D.ai.sub}
              </p>
            </div>
          </div>

          {/* Quick presets */}
          <div>
            <div className="mb-2 text-xs font-semibold text-slate-400">{D.ai.presetsLabel}</div>
            <div className="flex flex-wrap gap-2">
              {[
                {
                  name: D.ai.pCoreclr,
                  text: 'FATAL [CoreCLR] System.IO.FileNotFoundException: Could not load file or assembly FloVMP.Gamemode.dll',
                },
                {
                  name: D.ai.pUdp,
                  text: 'ERROR [Network] Failed to bind UDP socket on 0.0.0.0:7788: Address already in use / Firewall block',
                },
                {
                  name: D.ai.pBin,
                  text: 'ERROR [Server] Failed to load altv data file: data/release/data/vehicles.bin missing or corrupted',
                },
                {
                  name: D.ai.pToml,
                  text: 'ERROR [Resource] Failed to parse resource.toml: Invalid TOML syntax at line 14',
                },
              ].map((sample) => (
                <button
                  key={sample.name}
                  onClick={() => {
                    setTroubleshootText(sample.text);
                    void runTroubleshoot(sample.text);
                  }}
                  className="rounded-xl border border-white/10 bg-white/[0.03] px-3 py-1.5 font-mono text-[11px] text-slate-300 transition hover:border-brand/40 hover:bg-brand/10 hover:text-brand"
                >
                  {sample.name}
                </button>
              ))}
            </div>
          </div>

          {/* Input Area */}
          <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8">
            <FieldLabel>{D.ai.inputLabel}</FieldLabel>
            <textarea
              rows={5}
              value={troubleshootText}
              onChange={(e) => setTroubleshootText(e.target.value)}
              placeholder={D.ai.inputPlaceholder}
              className="field w-full p-4 font-mono text-xs text-slate-200"
            />
            <div className="mt-4 flex justify-end">
              <button
                onClick={() => runTroubleshoot()}
                disabled={diagnosing || !troubleshootText.trim()}
                className="btn btn-primary h-10 px-6 text-xs disabled:opacity-50"
              >
                {diagnosing ? <Spinner className="h-4 w-4" /> : <Zap className="h-4 w-4" />}
                {diagnosing ? D.ai.analyzing : D.ai.diagnose}
              </button>
            </div>
          </div>

          {/* Diagnosis Result */}
          {diagnosticResult && (
            <div className="glass-panel card-edge rounded-3xl p-6 shadow-glass sm:p-8 animate-fade-in space-y-6">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div
                    className={`flex h-10 w-10 items-center justify-center rounded-xl font-bold ${
                      diagnosticResult.severity === 'CRITICAL'
                        ? 'bg-red-500/20 text-red-400'
                        : 'bg-amber-500/20 text-amber-400'
                    }`}
                  >
                    <AlertTriangle className="h-5 w-5" />
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-white">{diagnosticResult.title}</h3>
                    <div className="font-mono text-xs text-slate-400">{D.ai.category}: {diagnosticResult.category}</div>
                  </div>
                </div>
                <Badge tone={diagnosticResult.severity === 'CRITICAL' ? 'red' : 'amber'}>
                  {diagnosticResult.severity}
                </Badge>
              </div>

              <div className="rounded-2xl border border-white/10 bg-ink-950/60 p-4">
                <div className="text-xs font-semibold text-slate-400 mb-1">{D.ai.cause}</div>
                <p className="text-sm text-slate-200 leading-relaxed">{diagnosticResult.explanation}</p>
              </div>

              <div>
                <div className="text-xs font-semibold text-brand mb-3 uppercase tracking-wider font-mono">
                  {D.ai.solution}
                </div>
                <div className="space-y-3">
                  {diagnosticResult.actionableFixes.map((step: string, idx: number) => (
                    <div
                      key={idx}
                      className="flex items-start gap-3 rounded-xl border border-white/[0.06] bg-white/[0.02] p-3 text-xs"
                    >
                      <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-brand/10 font-mono font-bold text-brand">
                        {idx + 1}
                      </span>
                      <div className="text-slate-300 font-mono leading-relaxed pt-0.5">{step}</div>
                    </div>
                  ))}
                </div>
              </div>

              {diagnosticResult.docsReference && (
                <div className="pt-2 font-mono text-xs text-slate-400">
                  {D.ai.docs}:{' '}
                  <span className="text-cyber underline cursor-pointer">{diagnosticResult.docsReference}</span>
                </div>
              )}
            </div>
          )}
        </div>
  );
}
