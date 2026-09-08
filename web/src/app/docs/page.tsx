'use client';

import React, { useEffect, useRef, useState } from 'react';
import {
  Check,
  ChevronRight,
  Cloud,
  Copy,
  Cpu,
  Play,
  Rocket,
  Server,
  ShieldCheck,
  Terminal,
} from 'lucide-react';
import { useT } from '@/lib/i18n';
import { Container, PageHero, Reveal } from '@/components/site';

const SNIPPETS: { id: string; label: string; lang: string; code: string }[] = [
  {
    id: 'csharp',
    label: 'C# · AltV.Net',
    lang: 'C#',
    code: `using FloVMP.Sdk;

var flov = new FloVClient(Environment.GetEnvironmentVariable("FLOVMP_KEY")!);
var lic  = await flov.License.VerifyAsync(serverIp: "188.127.229.224");

if (!lic.Valid) { Alt.Log($"[FloV:MP] {lic.Reason}"); Environment.Exit(1); }
Alt.Log($"[FloV:MP] {lic.Project} — {lic.MaxPlayers} slots, exp {lic.ExpiresAt}");`,
  },
  {
    id: 'node',
    label: 'Node.js · fetch',
    lang: 'JavaScript',
    code: `const ENDPOINT = 'https://flovmp.ru/api/v1/license/verify';

export async function verifyLicense(licenseKey, serverIp) {
  const res = await fetch(ENDPOINT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ licenseKey, serverIp, version: 'v16.4.39-flov', slots: 1500 }),
  });
  const data = await res.json();
  if (!res.ok || !data.valid) throw new Error('license invalid: ' + data.reason);
  // HMAC-SHA256 signature — 64 hex chars
  return data;
}`,
  },
  {
    id: 'nginx',
    label: 'Nginx · FastDL',
    lang: 'NGINX',
    code: `server {
    listen 80;
    server_name 188.127.229.224;

    location /cdn/ {
        alias /var/www/cdn/;
        autoindex off;
        expires 30d;
        add_header Cache-Control "public, max-age=2592000, immutable";
        charset utf-8;
        tcp_nodelay on;
        sendfile on;
    }
}`,
  },
];

function CopyChip({ value }: { value: string }) {
  const [c, setC] = useState(false);
  return (
    <button
      onClick={() => {
        navigator.clipboard?.writeText(value).catch(() => {});
        setC(true);
        setTimeout(() => setC(false), 1400);
      }}
      className="inline-flex items-center gap-1.5 rounded-md border border-white/10 bg-white/[0.03] px-2 py-1 text-[11px] text-white/55 hover:text-white"
    >
      {c ? <Check className="h-3 w-3 text-ok" /> : <Copy className="h-3 w-3" />}
      {c ? 'OK' : 'Copy'}
    </button>
  );
}

export default function DocsPage() {
  const t = useT();
  const [sdkTab, setSdkTab] = useState('csharp');
  const [verifyOut, setVerifyOut] = useState<string | null>(null);
  const [verifyStatus, setVerifyStatus] = useState<number | null>(null);
  const [verifying, setVerifying] = useState(false);

  const [agentLog, setAgentLog] = useState<string[]>([]);
  const esRef = useRef<EventSource | null>(null);

  useEffect(() => {
    return () => esRef.current?.close();
  }, []);

  const runVerify = async () => {
    setVerifying(true);
    setVerifyOut(null);
    setVerifyStatus(null);
    try {
      const res = await fetch('/api/v1/license/verify', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          licenseKey: 'FLV-ENTERPRISE-2026-DERZHAVA',
          serverIp: '188.127.229.224',
          version: 'v16.4.39-flov',
          slots: 1500,
        }),
      });
      const data = await res.json();
      setVerifyStatus(res.status);
      setVerifyOut(JSON.stringify(data, null, 2));
    } catch (err: any) {
      setVerifyOut('Error: ' + err.message);
    } finally {
      setVerifying(false);
    }
  };

  const toggleAgent = () => {
    if (esRef.current) {
      esRef.current.close();
      esRef.current = null;
      setAgentLog((l) => [...l, '— stream closed —']);
      return;
    }
    setAgentLog(['— connecting to /api/v1/agent/stream —']);
    try {
      const es = new EventSource('/api/v1/agent/stream');
      esRef.current = es;
      es.onmessage = (e) => setAgentLog((l) => [...l.slice(-40), e.data]);
      es.onerror = () => {
        setAgentLog((l) => [...l, '— stream error, closing —']);
        es.close();
        esRef.current = null;
      };
    } catch (err: any) {
      setAgentLog((l) => [...l, 'Error: ' + err.message]);
    }
  };

  const snip = SNIPPETS.find((s) => s.id === sdkTab)!;

  const NAV = [
    { id: 'quickstart', label: t.docs.navQuickstart, icon: Rocket },
    { id: 'verify', label: t.docs.navVerify, icon: ShieldCheck },
    { id: 'agent', label: t.docs.navAgent, icon: Cloud },
    { id: 'telemetry', label: t.docs.navTelemetry, icon: Cpu },
    { id: 'sdk', label: t.docs.navSdk, icon: Terminal },
    { id: 'fastdl', label: t.docs.navFastdl, icon: Server },
  ];

  return (
    <div>
      <PageHero eyebrow={t.nav.docs} title={t.docs.title} sub={t.docs.sub} />

      <Container className="py-12 sm:py-14">
        <div className="grid grid-cols-1 gap-8 lg:grid-cols-[220px_1fr]">
          <aside className="lg:sticky lg:top-24 lg:h-max">
            <nav className="card p-2">
              {NAV.map((n) => (
                <a
                  key={n.id}
                  href={`#${n.id}`}
                  className="group flex items-center gap-2.5 rounded-lg px-3 py-2.5 text-[13px] font-medium text-white/50 transition-colors hover:bg-white/5 hover:text-white"
                >
                  <n.icon className="h-4 w-4 text-brand/70 group-hover:text-brand" />
                  {n.label}
                  <ChevronRight className="ml-auto h-3.5 w-3.5 opacity-0 transition-opacity group-hover:opacity-100" />
                </a>
              ))}
            </nav>
          </aside>

          <div className="min-w-0 space-y-16">
            {/* QUICKSTART */}
            <section id="quickstart" className="scroll-mt-24">
              <Reveal>
                <h2 className="text-xl font-semibold tracking-tight">{t.docs.quickstartTitle}</h2>
                <p className="mt-2 max-w-2xl text-[13.5px] leading-relaxed text-white/50">{t.docs.quickstartLead}</p>
              </Reveal>
              <div className="mt-6 space-y-3">
                {t.docs.steps.map(([h, d], i) => (
                  <Reveal key={h} delay={i * 40}>
                    <div className="card p-4">
                      <div className="text-[13px] font-semibold text-white">{h}</div>
                      <p className="mt-1 text-[12.5px] leading-relaxed text-white/50">{d}</p>
                    </div>
                  </Reveal>
                ))}
              </div>
            </section>

            {/* VERIFY */}
            <section id="verify" className="scroll-mt-24">
              <Reveal>
                <div className="flex items-center gap-2">
                  <span className="rounded-md border border-brand/30 bg-brand/10 px-1.5 py-0.5 font-mono text-[10px] font-bold text-brand">POST</span>
                  <code className="font-mono text-[13px] text-white">/api/v1/license/verify</code>
                </div>
                <p className="mt-3 max-w-2xl text-[13.5px] leading-relaxed text-white/50">{t.docs.verifyDesc}</p>
              </Reveal>
              <div className="mt-5 grid grid-cols-1 gap-4 lg:grid-cols-2">
                <div className="card p-4">
                  <div className="mb-2 font-mono text-[10px] uppercase tracking-wider text-white/35">{t.docs.reqTitle}</div>
                  <pre className="terminal-scroll overflow-auto rounded-lg bg-black/40 p-3.5 font-mono text-[12px] leading-relaxed text-white/70">
{`{
  "licenseKey": "FLV-ENTERPRISE-2026-DERZHAVA",
  "serverIp": "188.127.229.224",
  "version": "v16.4.39-flov",
  "slots": 1500
}`}
                  </pre>
                  <button onClick={runVerify} disabled={verifying} className="btn btn-primary mt-3 h-9 w-full text-xs">
                    <Play className="h-3.5 w-3.5" />
                    {verifying ? t.docs.running : t.docs.runVerify}
                  </button>
                </div>
                <div className="overflow-hidden rounded-xl border border-white/[0.08] bg-white/[0.02]">
                  <div className="flex items-center justify-between border-b border-white/[0.07] px-4 py-2.5">
                    <span className="font-mono text-[10px] uppercase tracking-wider text-white/35">{t.docs.resTitle}</span>
                    {verifyStatus !== null && (
                      <span className={`rounded-md border px-1.5 py-0.5 font-mono text-[10px] font-bold ${verifyStatus < 300 ? 'border-ok/30 text-ok' : 'border-err/30 text-err'}`}>
                        HTTP {verifyStatus}
                      </span>
                    )}
                  </div>
                  <pre className="terminal-scroll max-h-[320px] min-h-[170px] overflow-auto p-4 font-mono text-[12px] leading-relaxed text-ok/90">
                    {verifyOut ?? t.docs.verifyHint}
                  </pre>
                </div>
              </div>
            </section>

            {/* AGENT */}
            <section id="agent" className="scroll-mt-24">
              <Reveal>
                <div className="flex items-center gap-2">
                  <span className="rounded-md border border-white/15 bg-white/5 px-1.5 py-0.5 font-mono text-[10px] font-bold text-white/70">SSE</span>
                  <code className="font-mono text-[13px] text-white">/api/v1/agent/stream</code>
                </div>
                <p className="mt-3 max-w-2xl text-[13.5px] leading-relaxed text-white/50">{t.docs.agentDesc}</p>
              </Reveal>
              <div className="mt-5 overflow-hidden rounded-xl border border-white/[0.08] bg-white/[0.02]">
                <div className="flex items-center gap-2 border-b border-white/[0.07] px-4 py-2.5">
                  <Terminal className="h-3.5 w-3.5 text-white/40" />
                  <span className="font-mono text-[11px] text-white/40">agent stream</span>
                  <button onClick={toggleAgent} className="btn btn-ghost ml-auto h-7 px-3 text-[11px]">
                    {esRef.current ? 'Stop' : 'Connect'}
                  </button>
                </div>
                <pre className="terminal-scroll max-h-[240px] min-h-[120px] overflow-auto bg-black/40 p-4 font-mono text-[11.5px] leading-relaxed text-white/60">
                  {agentLog.length ? agentLog.join('\n') : t.docs.agentHint}
                </pre>
              </div>
            </section>

            {/* TELEMETRY */}
            <section id="telemetry" className="scroll-mt-24">
              <Reveal>
                <div className="flex items-center gap-2">
                  <span className="rounded-md border border-brand/30 bg-brand/10 px-1.5 py-0.5 font-mono text-[10px] font-bold text-brand">POST</span>
                  <code className="font-mono text-[13px] text-white">/api/v1/telemetry/heartbeat</code>
                </div>
                <p className="mt-3 max-w-2xl text-[13.5px] leading-relaxed text-white/50">{t.docs.telemetryDesc}</p>
              </Reveal>
              <pre className="terminal-scroll mt-5 overflow-auto rounded-xl border border-white/[0.08] bg-black/40 p-4 font-mono text-[12px] leading-relaxed text-white/70">
{`{
  "licenseKey": "FLV-ENTERPRISE-2026-DERZHAVA",
  "players": 42, "maxPlayers": 1500,
  "tickRate": 60, "memoryMb": 384, "fps": 60
}`}
              </pre>
            </section>

            {/* SDK */}
            <section id="sdk" className="scroll-mt-24">
              <Reveal>
                <h2 className="text-xl font-semibold tracking-tight">{t.docs.navSdk}</h2>
                <p className="mt-2 max-w-2xl text-[13.5px] leading-relaxed text-white/50">{t.docs.sdkDesc}</p>
              </Reveal>
              <div className="mt-5 overflow-hidden rounded-xl border border-white/[0.08] bg-white/[0.02]">
                <div className="flex items-center gap-1 border-b border-white/[0.07] p-2">
                  {SNIPPETS.map((s) => (
                    <button
                      key={s.id}
                      onClick={() => setSdkTab(s.id)}
                      className={`rounded-md px-3 py-1.5 font-mono text-[11px] transition-colors ${
                        sdkTab === s.id ? 'bg-white/10 text-white' : 'text-white/45 hover:text-white/75'
                      }`}
                    >
                      {s.label}
                    </button>
                  ))}
                  <span className="ml-auto pr-1">
                    <CopyChip value={snip.code} />
                  </span>
                </div>
                <pre className="terminal-scroll max-h-[420px] overflow-auto bg-black/40 p-4 font-mono text-[12px] leading-relaxed text-white/75">
                  <code>{snip.code}</code>
                </pre>
              </div>
            </section>

            {/* FASTDL */}
            <section id="fastdl" className="scroll-mt-24">
              <Reveal>
                <h2 className="text-xl font-semibold tracking-tight">{t.docs.navFastdl}</h2>
                <p className="mt-2 max-w-2xl text-[13.5px] leading-relaxed text-white/50">{t.docs.fastdlDesc}</p>
              </Reveal>
              <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-3">
                {t.docs.fastdlFacts.map(([k, v]) => (
                  <div key={k} className="card p-4">
                    <div className="font-mono text-[10px] uppercase tracking-wider text-white/35">{k}</div>
                    <div className="mt-1 text-[13px] font-semibold text-white">{v}</div>
                  </div>
                ))}
              </div>
            </section>
          </div>
        </div>
      </Container>
    </div>
  );
}
