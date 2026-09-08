'use client';

import React from 'react';
import { Radio, Send, Terminal } from 'lucide-react';
import { useDashboard } from './_ctx';

export function ConsoleTab() {
  const {
    consoleInput,
    setConsoleInput,
    consoleLogs,
    setConsoleLogs,
    sseActive,
    toggleSseStream,
    sendConsoleCommand,
    D,
  } = useDashboard();
  return (
        <div className="relative space-y-6 animate-fade-in">
          {/* Header */}
          <div className="glass-panel card-edge flex flex-col gap-4 rounded-3xl p-6 shadow-glass sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <Terminal className="h-5 w-5 text-brand" />
                <h2 className="text-lg font-black text-white">{D.console.title}</h2>
              </div>
              <p className="mt-1 text-xs text-slate-400">
                {D.console.sub}
              </p>
            </div>
            <div className="flex items-center gap-3">
              <span className="inline-flex items-center gap-1.5 rounded-full border border-emeraldx/30 bg-emeraldx/15 px-3 py-1 font-mono text-[11px] font-bold text-emeraldx">
                <span className="h-2 w-2 rounded-full bg-emeraldx animate-pulse" />
                {D.console.agentConnected}
              </span>
              <button
                onClick={toggleSseStream}
                className={`btn h-9 px-3 text-xs font-semibold transition ${
                  sseActive
                    ? 'bg-emeraldx text-ink-950 font-bold'
                    : 'btn-ghost text-slate-300'
                }`}
                title={D.console.sseTitle}
              >
                <Radio className={`h-3.5 w-3.5 ${sseActive ? 'animate-pulse' : ''}`} />
                {sseActive ? D.console.sseOn : D.console.sseOff}
              </button>
              <button
                onClick={() =>
                  setConsoleLogs([
                    {
                      id: Date.now(),
                      time: new Date().toLocaleTimeString('ru-RU'),
                      tag: 'System',
                      text: D.toast.consoleCleared,
                      tone: 'info',
                    },
                  ])
                }
                className="btn btn-ghost h-9 px-3 text-xs"
              >
                {D.console.clear}
              </button>
            </div>
          </div>

          {/* Quick Command Chips */}
          <div className="flex flex-wrap gap-2">
            {[
              { label: D.console.chipReboot, cmd: D.console.chipRebootCmd },
              { label: D.console.chipStatus, cmd: 'status' },
              { label: D.console.chipGc, cmd: 'coreclr gc collect' },
              { label: D.console.chipBattleye, cmd: 'battleye status' },
            ].map((qc) => (
              <button
                key={qc.label}
                onClick={() => setConsoleInput(qc.cmd)}
                className="rounded-xl border border-white/10 bg-white/[0.03] px-3 py-1.5 font-mono text-[11px] text-slate-300 transition hover:border-brand/40 hover:bg-brand/10 hover:text-brand"
              >
                {qc.label}
              </button>
            ))}
          </div>

          {/* Terminal Box */}
          <div className="rounded-3xl border border-white/15 bg-ink-950/90 p-5 shadow-2xl backdrop-blur-xl">
            <div className="flex items-center justify-between border-b border-white/10 pb-3 font-mono text-xs text-slate-500">
              <div className="flex items-center gap-2">
                <span className="h-3 w-3 rounded-full bg-red-500/80" />
                <span className="h-3 w-3 rounded-full bg-amber-500/80" />
                <span className="h-3 w-3 rounded-full bg-emeraldx/80" />
                <span className="ml-2 text-slate-400">flovmp-control-plane://server-agent.flovmp.net</span>
              </div>
              <span className="text-[11px]">TTY-1 (C# CoreCLR)</span>
            </div>

            <div className="mt-4 max-h-[480px] min-h-[340px] space-y-2 overflow-y-auto font-mono text-xs leading-relaxed no-scrollbar">
              {consoleLogs.map((log) => (
                <div key={log.id} className="flex items-start gap-3">
                  <span className="text-slate-600">{log.time}</span>
                  <span
                    className={`rounded px-1.5 py-0.5 text-[10px] uppercase font-bold ${
                      log.tone === 'cmd'
                        ? 'bg-brand/20 text-brand'
                        : log.tone === 'warn'
                        ? 'bg-amber-500/20 text-amber-300'
                        : log.tone === 'error'
                        ? 'bg-red-500/20 text-red-400'
                        : 'bg-white/10 text-cyber'
                    }`}
                  >
                    {log.tag}
                  </span>
                  <span
                    className={
                      log.tone === 'cmd'
                        ? 'font-bold text-white'
                        : log.tone === 'error'
                        ? 'text-red-400'
                        : log.tone === 'warn'
                        ? 'text-amber-300'
                        : 'text-slate-300'
                    }
                  >
                    {log.text}
                  </span>
                </div>
              ))}
            </div>

            {/* Input Bar */}
            <form onSubmit={sendConsoleCommand} className="mt-4 flex items-center gap-2 border-t border-white/10 pt-4">
              <span className="font-mono text-xs font-bold text-brand">txAdmin@flovmp:~$</span>
              <input
                value={consoleInput}
                onChange={(e) => setConsoleInput(e.target.value)}
                placeholder={D.console.placeholder}
                className="flex-1 bg-transparent font-mono text-xs text-white placeholder-slate-600 focus:outline-none"
              />
              <button type="submit" className="btn btn-primary h-8 px-4 text-xs">
                <Send className="h-3.5 w-3.5" />
                {D.console.send}
              </button>
            </form>
          </div>
        </div>
  );
}
