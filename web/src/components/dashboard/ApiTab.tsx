'use client';

import React, { useState } from 'react';
import { Check, Copy, Globe, KeyRound, Link2, Plug, RefreshCw, Settings2 } from 'lucide-react';
import { useDashboard } from './_ctx';

export function ApiTab() {
  const { D, selectedProject, rotatingKey, rotateApiKey, openProjectSettings } = useDashboard();
  const a = D.api;
  const [reveal, setReveal] = useState(false);
  const [copied, setCopied] = useState<string | null>(null);

  const copy = (t: string) => {
    navigator.clipboard?.writeText(t).catch(() => {});
    setCopied(t);
    setTimeout(() => setCopied((c) => (c === t ? null : c)), 1500);
  };

  if (!selectedProject) {
    return (
      <div className="relative animate-fade-in glass card-edge rounded-2xl p-10 text-center text-sm text-white/50">
        <Plug className="mx-auto h-8 w-8 text-white/20" />
        <p className="mt-3">{a.noProject}</p>
      </div>
    );
  }

  const base = `https://api.flovmp.dev/v1/projects/${selectedProject.slug}`;
  const key: string = selectedProject.api_key;
  const endpoints: [string, string, string][] = [
    ['GET', `${base}/status`, a.epStatus],
    ['GET', `${base}/players`, a.epPlayers],
    ['POST', `${base}/donate`, a.epDonate],
  ];

  return (
    <div className="relative space-y-4 animate-fade-in">
      <div>
        <h2 className="flex items-center gap-2 text-lg font-black text-white">
          <Plug className="h-5 w-5 text-brand" />
          {a.title}
        </h2>
        <p className="mt-1 text-xs text-slate-400">{a.sub}</p>
      </div>

      {/* Base URL */}
      <div className="glass card-edge rounded-2xl p-5">
        <div className="mb-2 flex items-center gap-2 text-[12px] text-white/50">
          <Globe className="h-3.5 w-3.5" /> {a.baseUrl}
        </div>
        <div className="flex items-center gap-2">
          <code className="flex-1 truncate rounded-lg border border-white/10 bg-black/30 px-3 py-2 font-mono text-[12px] text-white/80">
            {base}
          </code>
          <button onClick={() => copy(base)} className="btn btn-ghost h-9 px-3 text-[11px]">
            {copied === base ? <Check className="h-3.5 w-3.5 text-ok" /> : <Copy className="h-3.5 w-3.5" />}
          </button>
        </div>
      </div>

      {/* Agent API Key */}
      <div className="glass card-edge rounded-2xl p-5">
        <div className="mb-2 flex items-center gap-2 text-[12px] text-white/50">
          <KeyRound className="h-3.5 w-3.5 text-brand" /> {a.agentKey}
        </div>
        <div className="flex items-center gap-2">
          <code className="flex-1 truncate rounded-lg border border-white/10 bg-black/30 px-3 py-2 font-mono text-[12px] text-brand">
            {reveal ? key : `${key.slice(0, 12)}${'•'.repeat(12)}`}
          </code>
          <button onClick={() => setReveal((v) => !v)} className="btn btn-ghost h-9 px-3 text-[11px]">
            {reveal ? a.hide : a.show}
          </button>
          <button onClick={() => copy(key)} className="btn btn-ghost h-9 px-3 text-[11px]">
            {copied === key ? <Check className="h-3.5 w-3.5 text-ok" /> : a.copy}
          </button>
          <button
            onClick={rotateApiKey}
            disabled={rotatingKey}
            className="btn h-9 border border-brand/40 bg-brand/10 px-3 text-[11px] font-semibold text-brand transition hover:bg-brand/20"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${rotatingKey ? 'animate-spin' : ''}`} />
            {rotatingKey ? a.rotating : a.rotate}
          </button>
        </div>
        <p className="mt-2 text-[11.5px] text-white/40">{a.keyHint}</p>
      </div>

      {/* Public endpoints */}
      <div className="glass card-edge rounded-2xl p-5">
        <h3 className="text-[13px] font-semibold text-white">{a.publicTitle}</h3>
        <p className="mt-1 text-[11.5px] leading-relaxed text-white/45">{a.publicSub}</p>
        <div className="mt-3 space-y-2">
          {endpoints.map(([m, url, desc]) => (
            <div key={url} className="rounded-lg border border-white/[0.07] bg-white/[0.02] p-3">
              <div className="flex flex-wrap items-center gap-2">
                <span
                  className={`rounded-md border px-1.5 py-0.5 font-mono text-[10px] font-bold ${
                    m === 'POST' ? 'border-brand/30 text-brand' : 'border-white/15 text-white/70'
                  }`}
                >
                  {m}
                </span>
                <code className="break-all font-mono text-[11.5px] text-white/75">{url}</code>
              </div>
              <div className="mt-1 text-[11px] text-white/40">{desc}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Webhooks summary */}
      <div className="glass card-edge rounded-2xl p-5">
        <h3 className="text-[13px] font-semibold text-white">{a.webhooksTitle}</h3>
        <p className="mt-1 text-[11.5px] leading-relaxed text-white/45">{a.webhooksSub}</p>
        <div className="mt-3 space-y-2">
          {[
            [a.whDiscord, !!selectedProject.discord_webhook_url],
            [a.whTelegram, !!(selectedProject.telegram_webhook_token && selectedProject.telegram_chat_id)],
          ].map(([l, on]) => (
            <div key={l as string} className="flex items-center gap-2.5 rounded-lg border border-white/[0.07] bg-white/[0.02] px-3 py-2 text-[12.5px]">
              <Link2 className="h-3.5 w-3.5 text-white/35" />
              <span className="text-white/75">{l as string}</span>
              <span
                className={`ml-auto rounded-full border px-2 py-0.5 text-[10px] ${
                  on ? 'border-ok/30 text-ok' : 'border-white/15 text-white/40'
                }`}
              >
                {on ? a.whOn : a.whOff}
              </span>
            </div>
          ))}
        </div>
        <button
          onClick={() => openProjectSettings(selectedProject)}
          className="btn btn-ghost mt-3 h-9 px-3.5 text-xs font-semibold"
        >
          <Settings2 className="h-4 w-4 text-brand" />
          {a.openSettings}
        </button>
      </div>
    </div>
  );
}
