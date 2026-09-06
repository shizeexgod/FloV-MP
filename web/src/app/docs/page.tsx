'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import {
  Code,
  Terminal,
  Server,
  Key,
  Shield,
  Copy,
  Check,
  Play,
  Layers,
  ArrowRight,
  ExternalLink,
  BookOpen,
  Cpu
} from 'lucide-react';

export default function DocsPage() {
  const [activeTab, setActiveTab] = useState<'verify' | 'heartbeat' | 'status' | 'nginx'>('verify');
  const [testResult, setTestResult] = useState<string | null>(null);
  const [testing, setTesting] = useState(false);
  const [copiedCode, setCopiedCode] = useState<string | null>(null);

  const copyCode = (code: string, id: string) => {
    navigator.clipboard.writeText(code);
    setCopiedCode(id);
    setTimeout(() => setCopiedCode(null), 2000);
  };

  const runTestVerify = async () => {
    setTesting(true);
    setTestResult(null);
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
      setTestResult(JSON.stringify(data, null, 2));
    } catch (err: any) {
      setTestResult(`Error: ${err.message}`);
    } finally {
      setTesting(false);
    }
  };

  const csharpSnippet = `using System.Net.Http.Json;

public class LicenseValidator
{
    private static readonly HttpClient _http = new();

    public static async Task<bool> VerifyLicenseAsync(string key, string serverIp)
    {
        var payload = new {
            licenseKey = key,
            serverIp = serverIp,
            version = "v16.4.39-flov",
            slots = 1500
        };

        var res = await _http.PostAsJsonAsync("https://flovmp.ru/api/v1/license/verify", payload);
        if (!res.IsSuccessStatusCode)
        {
            Alt.Log("[FloV:MP] [Security] License verification failed: " + res.StatusCode);
            return false;
        }

        var data = await res.Content.ReadFromJsonAsync<LicenseResponse>();
        Alt.Log($"[FloV:MP] License active: {data.Plan} (Max: {data.MaxPlayers} players)");
        return data.Valid;
    }
}`;

  const nginxSnippet = `server {
    listen 80;
    server_name 188.127.229.224;

    # FastDL параллельная отдача клиентских ресурсов
    location /cdn/ {
        alias /var/www/cdn/;
        autoindex off;
        expires 30d;
        add_header Cache-Control "public, max-age=2592000, immutable";
        charset utf-8;
    }

    # Публичный JSON статус сервера
    location /info {
        default_type application/json;
        charset utf-8;
        alias /var/www/cdn/info.json;
    }
}`;

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
      {/* Top Header */}
      <div className="text-center max-w-3xl mx-auto mb-12">
        <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full border border-brand/40 bg-brand/10 text-brand text-xs font-mono font-bold uppercase tracking-wider mb-4 shadow-neon-pink">
          <BookOpen className="w-3.5 h-3.5" />
          <span>FloV:MP Developer Documentation & SDK</span>
        </div>
        <h1 className="text-3xl sm:text-5xl font-black text-white tracking-tight">
          Документация и API интеграции
        </h1>
        <p className="text-gray-400 text-sm mt-3">
          Всё необходимое для валидации лицензий, отправки телеметрии и настройки Nginx FastDL
        </p>
      </div>

      {/* Tabs */}
      <div className="flex items-center justify-center gap-2 mb-8 flex-wrap">
        <button
          onClick={() => setActiveTab('verify')}
          className={`px-5 py-2.5 rounded-xl text-xs font-bold transition-all flex items-center gap-2 ${
            activeTab === 'verify' ? 'bg-brand text-white shadow-neon-pink' : 'glass-panel text-gray-400 hover:text-white'
          }`}
        >
          <Shield className="w-4 h-4" />
          <span>Проверка лицензии (Verify API)</span>
        </button>

        <button
          onClick={() => setActiveTab('heartbeat')}
          className={`px-5 py-2.5 rounded-xl text-xs font-bold transition-all flex items-center gap-2 ${
            activeTab === 'heartbeat' ? 'bg-brand text-white shadow-neon-pink' : 'glass-panel text-gray-400 hover:text-white'
          }`}
        >
          <Cpu className="w-4 h-4" />
          <span>Телеметрия сервера</span>
        </button>

        <button
          onClick={() => setActiveTab('nginx')}
          className={`px-5 py-2.5 rounded-xl text-xs font-bold transition-all flex items-center gap-2 ${
            activeTab === 'nginx' ? 'bg-brand text-white shadow-neon-pink' : 'glass-panel text-gray-400 hover:text-white'
          }`}
        >
          <Server className="w-4 h-4" />
          <span>FastDL CDN (Nginx)</span>
        </button>
      </div>

      {/* Tab Content 1: Verify API */}
      {activeTab === 'verify' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          <div className="space-y-6">
            <div className="glass-panel p-6 rounded-3xl border border-white/10">
              <h3 className="text-lg font-bold text-white mb-2 flex items-center gap-2">
                <Shield className="w-5 h-5 text-brand" />
                <span>POST /api/v1/license/verify</span>
              </h3>
              <p className="text-xs text-gray-400 leading-relaxed mb-4">
                Эндпоинт верификации, к которому сервер обращается при старте и каждые 15 минут. Проверяет статус ключа, привязку IP-адреса и подписывает ответ криптографическим HMAC-SHA256 токеном.
              </p>

              <div className="space-y-2 text-xs font-mono">
                <div className="text-gray-400">Формат запроса (JSON):</div>
                <pre className="p-3 bg-surface-300 rounded-xl text-gray-200 overflow-x-auto">
{`{
  "licenseKey": "FLV-ENTERPRISE-2026-DERZHAVA",
  "serverIp": "188.127.229.224",
  "version": "v16.4.39-flov",
  "slots": 1500
}`}
                </pre>
              </div>

              <div className="mt-4 pt-4 border-t border-white/10">
                <button
                  onClick={runTestVerify}
                  disabled={testing}
                  className="px-4 py-2 rounded-xl bg-brand hover:bg-brand-hover text-white text-xs font-bold shadow-neon-pink flex items-center gap-2 transition-colors disabled:opacity-50"
                >
                  <Play className="w-3.5 h-3.5" />
                  <span>{testing ? 'Отправка запроса...' : 'Тестировать онлайн (Live Test)'}</span>
                </button>

                {testResult && (
                  <div className="mt-4">
                    <span className="text-[11px] font-mono text-emerald-400 font-bold uppercase">Ответ API:</span>
                    <pre className="mt-1 p-3 bg-surface-300 rounded-xl font-mono text-xs text-emerald-300 overflow-x-auto">
                      {testResult}
                    </pre>
                  </div>
                )}
              </div>
            </div>
          </div>

          <div className="glass-panel p-6 rounded-3xl border border-white/10">
            <div className="flex items-center justify-between pb-3 mb-3 border-b border-white/10">
              <span className="text-xs font-bold text-gray-300 uppercase tracking-wider font-mono">
                Пример интеграции на C# (.NET 8)
              </span>
              <button
                onClick={() => copyCode(csharpSnippet, 'csharp')}
                className="text-xs text-gray-400 hover:text-white flex items-center gap-1"
              >
                {copiedCode === 'csharp' ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                <span>{copiedCode === 'csharp' ? 'Скопировано' : 'Копировать'}</span>
              </button>
            </div>
            <pre className="font-mono text-xs text-gray-200 overflow-x-auto leading-relaxed">
              {csharpSnippet}
            </pre>
          </div>
        </div>
      )}

      {/* Tab Content 2: Heartbeat */}
      {activeTab === 'heartbeat' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          <div className="glass-panel p-6 rounded-3xl border border-white/10">
            <h3 className="text-lg font-bold text-white mb-2 flex items-center gap-2">
              <Cpu className="w-5 h-5 text-purple-400" />
              <span>POST /api/v1/telemetry/heartbeat</span>
            </h3>
            <p className="text-xs text-gray-400 leading-relaxed mb-4">
              Игровой сервер отправляет телеметрию каждую минуту. Это позволяет отслеживать производительность рантайма, средний FPS, сетевой тикрейт и нагрузку на память.
            </p>

            <pre className="p-4 bg-surface-300 rounded-xl font-mono text-xs text-gray-200 overflow-x-auto">
{`{
  "licenseKey": "FLV-ENTERPRISE-2026-DERZHAVA",
  "players": 42,
  "maxPlayers": 1500,
  "tickRate": 60,
  "memoryMb": 384,
  "fps": 60
}`}
            </pre>
          </div>

          <div className="glass-panel p-6 rounded-3xl border border-white/10">
            <h4 className="text-sm font-bold text-white mb-3">Преимущества телеметрии FloV:MP</h4>
            <ul className="space-y-3 text-xs text-gray-300">
              <li className="flex items-start gap-2">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 mt-1.5 shrink-0"></span>
                <span>Мгновенный мониторинг утечек памяти в гейммоде (GC / Heap).</span>
              </li>
              <li className="flex items-start gap-2">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 mt-1.5 shrink-0"></span>
                <span>Аналитика пиков онлайна и стабильности сетевых тиков.</span>
              </li>
              <li className="flex items-start gap-2">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 mt-1.5 shrink-0"></span>
                <span>Отображение графиков нагрузки прямо в личном кабинете владельца проекта.</span>
              </li>
            </ul>
          </div>
        </div>
      )}

      {/* Tab Content 3: Nginx */}
      {activeTab === 'nginx' && (
        <div className="glass-panel p-6 rounded-3xl border border-white/10">
          <div className="flex items-center justify-between pb-3 mb-3 border-b border-white/10">
            <div>
              <h3 className="text-base font-bold text-white">Конфигурация FastDL CDN для Nginx</h3>
              <p className="text-xs text-gray-400">
                Раздача клиентских файлов (транспорт, скины, интерьеры) на скорости 10 Гбит/с через HTTP/2
              </p>
            </div>
            <button
              onClick={() => copyCode(nginxSnippet, 'nginx')}
              className="text-xs text-gray-400 hover:text-white flex items-center gap-1"
            >
              {copiedCode === 'nginx' ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
              <span>{copiedCode === 'nginx' ? 'Скопировано' : 'Копировать'}</span>
            </button>
          </div>
          <pre className="font-mono text-xs text-gray-200 overflow-x-auto leading-relaxed">
            {nginxSnippet}
          </pre>
        </div>
      )}
    </div>
  );
}
