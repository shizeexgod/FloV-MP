'use client';

import React, { useState } from 'react';
import {
  BookOpen,
  ChevronRight,
  Cpu,
  Play,
  Server,
  Shield,
  Terminal,
} from 'lucide-react';
import { AuroraBlobs, Badge, CopyButton, Spinner } from '@/components/ui';

const NAV = [
  { id: 'verify', label: 'Верификация лицензии', icon: Shield },
  { id: 'sdk', label: 'SDK интеграции', icon: Terminal },
  { id: 'heartbeat', label: 'Телеметрия сервера', icon: Cpu },
  { id: 'fastdl', label: 'FastDL CDN (Nginx)', icon: Server },
];

const SDK_TABS: { id: string; label: string; lang: string; code: string }[] = [
  {
    id: 'csharp',
    label: 'C# SDK',
    lang: 'C# · AltV.Net',
    code: `using System.Net.Http.Json;
using AltV.Net;

public sealed class LicenseValidator
{
    private static readonly HttpClient Http = new();
    private const string Endpoint = "https://flovmp.ru/api/v1/license/verify";

    public static async Task<bool> VerifyAsync(string key, string serverIp)
    {
        var payload = new
        {
            licenseKey = key,
            serverIp,
            version = "v16.4.39-flov",
            slots = 1500
        };

        var res = await Http.PostAsJsonAsync(Endpoint, payload);
        if (!res.IsSuccessStatusCode)
        {
            Alt.Log($"[FloV:MP] [Security] verify failed: {res.StatusCode}");
            return false;
        }

        var data = await res.Content.ReadFromJsonAsync<LicenseResponse>();
        Alt.Log($"[FloV:MP] {data!.Plan} — {data.MaxPlayers} слотов");
        return data.Valid;
    }
}`,
  },
  {
    id: 'node',
    label: 'Node.js',
    lang: 'JavaScript · fetch',
    code: `const ENDPOINT = 'https://flovmp.ru/api/v1/license/verify';

export async function verifyLicense(licenseKey, serverIp) {
  const res = await fetch(ENDPOINT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      licenseKey,
      serverIp,
      version: 'v16.4.39-flov',
      slots: 1500,
    }),
  });

  const data = await res.json();
  if (!res.ok || !data.valid) {
    console.error('[FloV:MP] license invalid:', data.reason);
    process.exit(1);
  }

  // HMAC-SHA256 подпись ответа — 64 hex-символа
  console.log('[FloV:MP] plan:', data.plan, '· sig:', data.signature.slice(0, 12) + '…');
  return data;
}`,
  },
  {
    id: 'nginx',
    label: 'Nginx FastDL',
    lang: 'NGINX config',
    code: `server {
    listen 80;
    server_name 188.127.229.224;

    # Параллельная отдача клиентских ресурсов (транспорт, скины, интерьеры)
    location /cdn/ {
        alias /var/www/cdn/;
        autoindex off;
        expires 30d;
        add_header Cache-Control "public, max-age=2592000, immutable";
        charset utf-8;
        tcp_nodelay on;
        sendfile on;
    }

    # Публичный JSON-статус сервера для портала
    location /info {
        default_type application/json;
        charset utf-8;
        alias /var/www/cdn/info.json;
    }
}`,
  },
];

export default function DocsPage() {
  const [sdkTab, setSdkTab] = useState('csharp');
  const [testResult, setTestResult] = useState<string | null>(null);
  const [testStatus, setTestStatus] = useState<number | null>(null);
  const [testing, setTesting] = useState(false);

  const runVerify = async () => {
    setTesting(true);
    setTestResult(null);
    setTestStatus(null);
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
      setTestStatus(res.status);
      setTestResult(JSON.stringify(data, null, 2));
    } catch (err: any) {
      setTestResult(`Error: ${err.message}`);
    } finally {
      setTesting(false);
    }
  };

  const activeSdk = SDK_TABS.find((t) => t.id === sdkTab)!;

  return (
    <div className="relative px-4 py-14 sm:px-6 lg:px-8">
      <AuroraBlobs />
      <div className="relative mx-auto max-w-7xl">
        {/* Header */}
        <div className="mx-auto max-w-3xl text-center">
          <span className="mx-auto inline-flex items-center gap-2 rounded-full border border-brand/35 bg-brand/10 px-3.5 py-1.5 shadow-neon-pink">
            <BookOpen className="h-3.5 w-3.5 text-brand" />
            <span className="eyebrow text-brand">Developer Documentation &amp; SDK</span>
          </span>
          <h1 className="mt-5 text-3xl font-black text-white sm:text-5xl">Документация и API интеграции</h1>
          <p className="mt-3 text-sm text-slate-400">
            Всё для валидации лицензий, отправки телеметрии и настройки Nginx FastDL.
          </p>
        </div>

        <div className="mt-14 grid grid-cols-1 gap-8 lg:grid-cols-[220px_1fr]">
          {/* Sidebar */}
          <aside className="lg:sticky lg:top-24 lg:h-max">
            <nav className="glass card-edge rounded-2xl p-2">
              {NAV.map((n) => (
                <a
                  key={n.id}
                  href={`#${n.id}`}
                  className="group flex items-center gap-2.5 rounded-xl px-3 py-2.5 text-[13px] font-medium text-slate-400 transition-colors hover:bg-white/5 hover:text-white"
                >
                  <n.icon className="h-4 w-4 text-brand/70 group-hover:text-brand" />
                  {n.label}
                  <ChevronRight className="ml-auto h-3.5 w-3.5 opacity-0 transition-opacity group-hover:opacity-100" />
                </a>
              ))}
            </nav>
          </aside>

          {/* Content */}
          <div className="min-w-0 space-y-16">
            {/* Verify */}
            <section id="verify" className="scroll-mt-24">
              <div className="flex items-center gap-2">
                <Badge tone="brand">POST</Badge>
                <code className="font-mono text-sm text-white">/api/v1/license/verify</code>
              </div>
              <p className="mt-4 max-w-2xl text-[14px] leading-relaxed text-slate-400">
                Эндпоинт верификации, к которому сервер обращается при старте и каждые 15 минут.
                Проверяет статус ключа, привязку IPv4 и подписывает ответ криптографическим
                HMAC-SHA256 токеном.
              </p>

              <div className="mt-6 grid grid-cols-1 gap-5 lg:grid-cols-2">
                <div className="glass card-edge rounded-2xl p-5">
                  <div className="mb-2 font-mono text-[11px] uppercase tracking-wider text-slate-500">
                    Тело запроса
                  </div>
                  <pre className="terminal-scroll overflow-auto rounded-xl bg-ink-950/70 p-4 font-mono text-[12px] leading-relaxed text-slate-300">
{`{
  "licenseKey": "FLV-ENTERPRISE-2026-DERZHAVA",
  "serverIp": "188.127.229.224",
  "version": "v16.4.39-flov",
  "slots": 1500
}`}
                  </pre>
                  <button
                    onClick={runVerify}
                    disabled={testing}
                    className="btn btn-primary mt-4 h-10 w-full px-4 text-xs disabled:opacity-50"
                  >
                    {testing ? <Spinner className="h-4 w-4" /> : <Play className="h-3.5 w-3.5" />}
                    {testing ? 'Отправка запроса…' : 'Выполнить тестовую верификацию'}
                  </button>
                </div>

                <div className="glass-panel card-edge overflow-hidden rounded-2xl">
                  <div className="flex items-center justify-between border-b border-white/[0.08] px-4 py-2.5">
                    <span className="font-mono text-[11px] uppercase tracking-wider text-slate-500">
                      Ответ API
                    </span>
                    {testStatus !== null && (
                      <Badge tone={testStatus >= 200 && testStatus < 300 ? 'emerald' : 'red'}>
                        HTTP {testStatus}
                      </Badge>
                    )}
                  </div>
                  <pre className="terminal-scroll max-h-[340px] min-h-[180px] overflow-auto p-4 font-mono text-[12px] leading-relaxed text-emeraldx/90">
                    {testResult ?? '// Нажмите «Выполнить тестовую верификацию»\n// Здесь появится JSON с подписью HMAC-SHA256'}
                  </pre>
                </div>
              </div>
            </section>

            {/* SDK */}
            <section id="sdk" className="scroll-mt-24">
              <h2 className="text-2xl font-black text-white">SDK интеграции</h2>
              <p className="mt-3 max-w-2xl text-[14px] leading-relaxed text-slate-400">
                Готовые сниппеты для серверного гейммода AltV.Net, Node.js и конфигурации FastDL.
              </p>

              <div className="mt-6 glass-panel card-edge overflow-hidden rounded-2xl shadow-glass">
                <div className="flex items-center gap-1 border-b border-white/[0.08] px-3 py-2.5">
                  {SDK_TABS.map((t) => (
                    <button
                      key={t.id}
                      onClick={() => setSdkTab(t.id)}
                      className={`rounded-lg px-3 py-1.5 font-mono text-[11px] transition-colors ${
                        sdkTab === t.id ? 'bg-white/10 text-white' : 'text-slate-500 hover:text-slate-300'
                      }`}
                    >
                      {t.label}
                    </button>
                  ))}
                  <div className="ml-auto flex items-center gap-2 pr-1">
                    <span className="hidden font-mono text-[10px] text-brand sm:block">{activeSdk.lang}</span>
                    <CopyButton value={activeSdk.code} size="sm" />
                  </div>
                </div>
                <pre className="terminal-scroll max-h-[460px] overflow-auto p-5 font-mono text-[12px] leading-relaxed text-slate-300">
                  <code>{activeSdk.code}</code>
                </pre>
              </div>
            </section>

            {/* Heartbeat */}
            <section id="heartbeat" className="scroll-mt-24">
              <div className="flex items-center gap-2">
                <Badge tone="violet">POST</Badge>
                <code className="font-mono text-sm text-white">/api/v1/telemetry/heartbeat</code>
              </div>
              <p className="mt-4 max-w-2xl text-[14px] leading-relaxed text-slate-400">
                Игровой сервер отправляет телеметрию каждую минуту: средний FPS, сетевой тикрейт,
                нагрузку на память и онлайн. Данные строят графики в личном кабинете владельца.
              </p>
              <div className="mt-6 grid grid-cols-1 gap-5 lg:grid-cols-2">
                <pre className="glass card-edge terminal-scroll overflow-auto rounded-2xl p-5 font-mono text-[12px] leading-relaxed text-slate-300">
{`{
  "licenseKey": "FLV-ENTERPRISE-2026-DERZHAVA",
  "players": 42,
  "maxPlayers": 1500,
  "tickRate": 60,
  "memoryMb": 384,
  "fps": 60
}`}
                </pre>
                <ul className="glass card-edge space-y-3 rounded-2xl p-6 text-[13px] text-slate-300">
                  {[
                    'Мгновенный мониторинг утечек памяти в гейммоде (GC / Heap).',
                    'Аналитика пиков онлайна и стабильности сетевых тиков.',
                    'История последних 30 пакетов прямо в дашборде проекта.',
                  ].map((x) => (
                    <li key={x} className="flex items-start gap-2.5">
                      <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-emeraldx" />
                      {x}
                    </li>
                  ))}
                </ul>
              </div>
            </section>

            {/* FastDL */}
            <section id="fastdl" className="scroll-mt-24">
              <h2 className="text-2xl font-black text-white">FastDL CDN (Nginx)</h2>
              <p className="mt-3 max-w-2xl text-[14px] leading-relaxed text-slate-400">
                Раздача клиентских файлов на скорости гигабитного порта через HTTP/2. Переключите
                вкладку «Nginx FastDL» в блоке SDK выше, чтобы скопировать готовый конфиг.
              </p>
              <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
                {[
                  ['Cache-Control', 'immutable, 30 дней'],
                  ['Протокол', 'HTTP/2 + sendfile'],
                  ['Нагрузка на UDP', '0% — канал разгружен'],
                ].map(([k, v]) => (
                  <div key={k} className="glass card-edge rounded-2xl p-5">
                    <div className="font-mono text-[11px] uppercase tracking-wider text-slate-500">{k}</div>
                    <div className="mt-1.5 text-sm font-bold text-white">{v}</div>
                  </div>
                ))}
              </div>
            </section>
          </div>
        </div>
      </div>
    </div>
  );
}
