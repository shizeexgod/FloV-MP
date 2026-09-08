'use client';

import React, { useState } from 'react';
import { AlertCircle, Check, Copy, ShieldCheck } from 'lucide-react';
import { useDashboard } from './_ctx';

export function SettingsTab() {
  const { D, user, promoCode, twoFa, setTwoFa, start2fa, confirm2fa, disable2fa } = useDashboard();
  const s = D.sec;
  const [copied, setCopied] = useState<string | null>(null);

  const copy = (t: string) => {
    navigator.clipboard?.writeText(t).catch(() => {});
    setCopied(t);
    setTimeout(() => setCopied((c) => (c === t ? null : c)), 1500);
  };

  const label = 'mb-1.5 block font-mono text-[10px] uppercase tracking-wider text-white/40';

  return (
    <div className="relative grid grid-cols-1 gap-4 animate-fade-in lg:grid-cols-2">
      {/* Account */}
      <div className="glass card-edge rounded-2xl p-5">
        <h3 className="text-[13px] font-semibold text-white">{s.accountTitle}</h3>
        <div className="mt-3 space-y-3">
          {[
            [s.name, user?.username ?? ''],
            [s.email, user?.email ?? ''],
            [s.telegram, user?.telegram ?? '@'],
          ].map(([l, v]) => (
            <label key={l}>
              <span className={label}>{l}</span>
              <input defaultValue={v} className="field h-10 px-3" />
            </label>
          ))}
        </div>
        <button className="btn btn-primary mt-4 h-10 px-4 text-xs">{s.save}</button>
      </div>

      {/* 2FA */}
      <div className="glass card-edge rounded-2xl p-5">
        <div className="flex items-center gap-2">
          <ShieldCheck className="h-4 w-4 text-brand" />
          <h3 className="text-[13px] font-semibold text-white">{s.twoFaTitle}</h3>
        </div>

        {twoFa.loading ? (
          <p className="mt-3 text-[12px] text-white/40">…</p>
        ) : twoFa.setup ? (
          <div className="mt-3 space-y-3">
            <p className="text-[12px] leading-relaxed text-white/50">{s.setupHint}</p>
            <div>
              <span className={label}>{s.secretLabel}</span>
              <div className="flex items-center gap-2">
                <code className="flex-1 break-all rounded-lg border border-white/10 bg-black/30 px-3 py-2 font-mono text-[12px] text-brand">
                  {twoFa.setup.secret}
                </code>
                <button onClick={() => copy(twoFa.setup!.secret)} className="btn btn-ghost h-9 w-9 p-0">
                  {copied === twoFa.setup.secret ? <Check className="h-4 w-4 text-ok" /> : <Copy className="h-4 w-4" />}
                </button>
              </div>
            </div>
            <div>
              <span className={label}>{s.uriLabel}</span>
              <code className="block break-all rounded-lg border border-white/10 bg-black/30 px-3 py-2 font-mono text-[10.5px] text-white/45">
                {twoFa.setup.otpauthUri}
              </code>
            </div>
            <label>
              <span className={label}>{s.codeLabel}</span>
              <input
                value={twoFa.code}
                onChange={(e) => setTwoFa((st) => ({ ...st, code: e.target.value.replace(/\D/g, '').slice(0, 6) }))}
                placeholder="000000"
                inputMode="numeric"
                className="field h-10 px-3 font-mono tracking-[0.3em]"
              />
            </label>
            {twoFa.err && (
              <div className="flex items-center gap-2 rounded-lg border border-err/30 bg-err/10 px-3 py-2 text-[12px] text-err">
                <AlertCircle className="h-3.5 w-3.5" /> {twoFa.err}
              </div>
            )}
            <div className="flex gap-2">
              <button onClick={confirm2fa} disabled={twoFa.busy || twoFa.code.length !== 6} className="btn btn-primary h-10 px-4 text-xs">
                {s.confirm}
              </button>
              <button
                onClick={() => setTwoFa((st) => ({ ...st, setup: null, code: '', err: '' }))}
                className="btn btn-ghost h-10 px-4 text-xs"
              >
                {s.cancel}
              </button>
            </div>
          </div>
        ) : twoFa.enabled ? (
          <div className="mt-3 space-y-3">
            <p className="text-[12px] text-ok">{s.twoFaOnDesc}</p>
            <label>
              <span className={label}>{s.codeLabel}</span>
              <input
                value={twoFa.code}
                onChange={(e) => setTwoFa((st) => ({ ...st, code: e.target.value.replace(/\D/g, '').slice(0, 6) }))}
                placeholder="000000"
                inputMode="numeric"
                className="field h-10 px-3 font-mono tracking-[0.3em]"
              />
            </label>
            {twoFa.err && (
              <div className="flex items-center gap-2 rounded-lg border border-err/30 bg-err/10 px-3 py-2 text-[12px] text-err">
                <AlertCircle className="h-3.5 w-3.5" /> {twoFa.err}
              </div>
            )}
            <button
              onClick={disable2fa}
              disabled={twoFa.busy || twoFa.code.length !== 6}
              className="btn h-10 border border-err/40 bg-err/10 px-4 text-xs font-semibold text-err transition hover:bg-err/20"
            >
              {s.disable}
            </button>
          </div>
        ) : (
          <div className="mt-3 space-y-3">
            <p className="text-[12px] leading-relaxed text-white/50">{s.twoFaOffDesc}</p>
            <button onClick={start2fa} disabled={twoFa.busy} className="btn btn-primary h-10 px-4 text-xs">
              {s.enable}
            </button>
          </div>
        )}
      </div>

      {/* Referral */}
      <div className="glass card-edge rounded-2xl p-5">
        <h3 className="text-[13px] font-semibold text-white">{s.refTitle}</h3>
        <div className="mt-3">
          <span className={label}>{s.refCode}</span>
          <div className="flex items-center gap-2">
            <code className="flex-1 rounded-lg border border-white/10 bg-black/30 px-3 py-2 font-mono text-[13px] font-bold text-brand">
              {promoCode}
            </code>
            <button onClick={() => copy(promoCode)} className="btn btn-ghost h-9 px-3 text-[11px]">
              {copied === promoCode ? s.copied : s.copy}
            </button>
          </div>
        </div>
        <div className="mt-3 grid grid-cols-3 gap-2 text-center">
          {[
            [s.refInvited, '0'],
            [s.refEarned, '0 ₽'],
            [s.refShare, '20%'],
          ].map(([l, v]) => (
            <div key={l} className="rounded-lg border border-white/[0.07] bg-white/[0.02] py-2">
              <div className="text-[13px] font-semibold text-white">{v}</div>
              <div className="font-mono text-[9px] uppercase text-white/35">{l}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Sessions */}
      <div className="glass card-edge overflow-hidden rounded-2xl">
        <div className="border-b border-white/[0.07] px-4 py-2.5 text-[12px] text-white/50">{s.sessionsTitle}</div>
        <div className="divide-y divide-white/[0.05]">
          {s.sessions.map((row: [string, string, string, string, boolean], i: number) => (
            <div key={i} className="flex items-center gap-3 px-4 py-3 text-[12px]">
              <div className="min-w-0">
                <div className="truncate text-white/80">{row[0]}</div>
                <div className="truncate font-mono text-[10px] text-white/35">
                  {row[1]} · {row[2]}
                </div>
              </div>
              <span className="ml-auto text-[11px] text-white/35">{row[3]}</span>
              {row[4] ? (
                <span className="rounded border border-ok/30 px-1.5 py-0.5 text-[10px] text-ok">{s.sessionCurrent}</span>
              ) : (
                <button className="text-[11px] text-err/80 hover:text-err">{s.sessionRevoke}</button>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
