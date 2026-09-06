'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import {
  ArrowRight,
  BadgeCheck,
  Boxes,
  ChevronDown,
  Copy,
  Check,
  Gauge,
  Lock,
  Radio,
  Send,
  ShieldCheck,
  Sparkles,
  Terminal,
  Users,
} from 'lucide-react';
import { AuroraBlobs, Badge, Section, SectionHeading } from '@/components/ui';

interface ServerStatus {
  online: boolean;
  name: string;
  players: number;
  maxPlayers: number;
  pingMs: number;
  host: string;
  port: number | string;
  version: string;
}

const PILLARS = [
  {
    n: '01',
    title: 'Автономный рантайм CoreCLR',
    body: 'Сервер стартует и работает независимо на вашем VDS. .NET 8 C# для логики, V8 для JS/TS-клиента — без обращения к мастер-серверам и без риска отключения backend.',
  },
  {
    n: '02',
    title: 'Встроенный FastDL CDN',
    body: 'Докачка кэша, транспорта, скинов и карт на скорости гигабитного порта через параллельный HTTP/2. Игровой UDP-канал остаётся разгруженным.',
  },
  {
    n: '03',
    title: 'Защита от детектов и банов',
    body: 'Изолированная экосистема без сторонних мастер-листов. Криптографическая верификация ключей HMAC-SHA256 и серверный контроль всех транзакций.',
  },
];

const FEATURES = [
  {
    icon: Radio,
    tone: 'text-brand',
    ring: 'border-brand/40 bg-brand/10',
    title: '3D Voice чат',
    body: 'Нативный WebRTC + кодек Opus без TeamSpeak. Пространственный звук, рации фракций, мегафоны и звуковые зоны с эхом.',
  },
  {
    icon: Users,
    tone: 'text-cyber',
    ring: 'border-cyber/40 bg-cyber/10',
    title: 'Синхронизация педов и транспорта',
    body: 'Живой город с ботами, квестовые NPC, боевые противники в рейдах и охрана государственных объектов — всё синхронно.',
  },
  {
    icon: ShieldCheck,
    tone: 'text-violetx',
    ring: 'border-violetx/40 bg-violetx/10',
    title: '8-уровневая админ-система',
    body: 'Готовая иерархия прав от хелпера до техдиректора, журналирование действий и панель модерации из коробки.',
  },
  {
    icon: Boxes,
    tone: 'text-amber-400',
    ring: 'border-amber-500/40 bg-amber-500/10',
    title: 'Кастомные DLC, одежда и карты',
    body: 'Стриминг собственного контента: интерьеры, карта Москвы, транспорт и одежда через FastDL без клиентских модов.',
  },
  {
    icon: Lock,
    tone: 'text-emeraldx',
    ring: 'border-emeraldx/40 bg-emeraldx/10',
    title: 'Криптографическая верификация ключей',
    body: 'Каждый ответ валидатора подписан HMAC-SHA256. Привязка к IPv4, срокам и слотам — подделать лицензию невозможно.',
  },
  {
    icon: Gauge,
    tone: 'text-brand',
    ring: 'border-brand/40 bg-brand/10',
    title: 'Античит-фильтры',
    body: 'Проверка хэшей клиентских файлов при старте лаунчера, серверная валидация движения, оружия и экономики, защита от инъекций.',
  },
];

const STEPS = [
  { t: 'Регистрация', d: 'Создайте аккаунт на портале и моментально получите стартовый ключ разработчика Indie на 128 слотов.' },
  { t: 'Привязка IP', d: 'Укажите публичный IPv4-адрес вашего VDS/Dedicated в личном кабинете для безопасной привязки лицензии.' },
  { t: 'Дистрибутив', d: 'Скачайте чистый серверный архив движка под Ubuntu Linux одним файлом прямо из кабинета.' },
  { t: 'server.toml', d: 'Вставьте лицензионный ключ в конфиг, укажите FastDL CDN URL и порт 7788.' },
  { t: 'Гейммод', d: 'Подключите готовый C# .NET 8 гейммод или портируйте свою логику с RAGE:MP / FiveM по гайду.' },
  { t: 'Старт', d: 'Запустите рантайм — сервер проходит онлайн-верификацию и открывает приём игроков.' },
];

const TERMINAL_TABS: { id: string; file: string; lang: string; code: string }[] = [
  {
    id: 'toml',
    file: 'server.toml',
    lang: 'TOML',
    code: `# Конфигурация автономного узла FloV:MP
name      = "Держава Онлайн"
host      = "0.0.0.0"
port      = 7788
players   = 1500
modules   = ["csharp-module"]
resources = ["flovmp-gamemode"]

[licensing]
key           = "FLV-ENTERPRISE-2026-DERZHAVA"
auth_endpoint = "https://flovmp.ru/api/v1/license/verify"
bound_ip      = "188.127.229.224"
recheck_min   = 15

[cdn]
useExternalCDN = true
cdnUrl         = "http://188.127.229.224/cdn"`,
  },
  {
    id: 'cs',
    file: 'LicenseCheck.cs',
    lang: 'C# .NET 8',
    code: `using System.Net.Http.Json;
using AltV.Net;

public static class LicenseCheck
{
    private static readonly HttpClient Http = new();

    public static async Task<bool> VerifyAsync(string key, string ip)
    {
        var res = await Http.PostAsJsonAsync(
            "https://flovmp.ru/api/v1/license/verify",
            new { licenseKey = key, serverIp = ip, slots = 1500 });

        if (!res.IsSuccessStatusCode) return false;

        var data = await res.Content.ReadFromJsonAsync<VerifyResult>();
        Alt.Log($"[FloV:MP] {data!.Plan} — {data.MaxPlayers} слотов, до {data.ExpiresAt}");
        return data.Valid && data.Signature is { Length: 64 };
    }
}`,
  },
  {
    id: 'nginx',
    file: 'nginx-fastdl.conf',
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
    }

    location /info {
        default_type application/json;
        alias /var/www/cdn/info.json;
    }
}`,
  },
];

const FAQ = [
  {
    q: 'На каком языке пишется серверная логика?',
    a: 'Гейм-логика — на C# / .NET 8 через coreclr-module. Клиентский UI (HUD, меню, камеры) — на JS/TS + HTML (NUI). Это позволяет переиспользовать наработки с RAGE:MP и FiveM почти без переписывания.',
  },
  {
    q: 'Голосовой чат встроен или нужен внешний сервис?',
    a: 'Полностью встроен. Нативный WebRTC + Opus прямо в движке: пространственное 3D-позиционирование, рации, мегафоны, звуковые зоны. TeamSpeak и сторонние плагины не нужны.',
  },
  {
    q: 'Как игроки подключаются — нужен ли общий мастер-лист?',
    a: 'Нет. Подключение прямое, через кастомный лаунчер, собранный под ваш проект в личном кабинете (название, цвет, IP:порт). Общего списка серверов не существует — это часть модели защиты.',
  },
  {
    q: 'Насколько сложен перенос проекта с RageMP или FiveM?',
    a: 'Архитектура ресурсов и сущностей близка к привычной. Структуры данных и бизнес-логика переносятся своими словами, MariaDB-схема идёт в комплекте. Для Enterprise доступна услуга миграции «под ключ».',
  },
  {
    q: 'Что будет после патчей Rockstar?',
    a: 'Поддержка совместимости — часть услуги. Обновления offset-слоя и клиентских хэшей выкатываются через FastDL-манифест, лаунчер докачивает их автоматически при следующем запуске.',
  },
];

const PRICING = {
  month: { pro: '14 900 ₽', proNote: '/ месяц', ent: '49 000 ₽', entNote: '/ лицензия', save: null as string | null },
  halfYear: { pro: '12 665 ₽', proNote: '/ месяц · −15%', ent: '41 650 ₽', entNote: '/ лицензия · −15%', save: 'Экономия 15%' },
  year: { pro: '10 430 ₽', proNote: '/ месяц · −30%', ent: '34 300 ₽', entNote: '/ лицензия · −30%', save: 'Экономия 30%' },
};

export default function HomePage() {
  const [period, setPeriod] = useState<'month' | 'halfYear' | 'year'>('month');
  const [tab, setTab] = useState('toml');
  const [openFaq, setOpenFaq] = useState<number | null>(0);
  const [copied, setCopied] = useState(false);
  const [status, setStatus] = useState<ServerStatus>({
    online: true,
    name: 'Держава Онлайн',
    players: 1,
    maxPlayers: 1500,
    pingMs: 24,
    host: '188.127.229.224',
    port: 7788,
    version: 'v16.4.39-flov',
  });

  useEffect(() => {
    fetch('/api/server-status')
      .then((r) => r.json())
      .then((d) => d && setStatus((s) => ({ ...s, ...d })))
      .catch(() => {});
  }, []);

  const copyIp = () => {
    navigator.clipboard?.writeText(`${status.host}:${status.port}`).catch(() => {});
    setCopied(true);
    setTimeout(() => setCopied(false), 1800);
  };

  const price = PRICING[period];
  const activeTab = TERMINAL_TABS.find((t) => t.id === tab)!;
  const fill = Math.min(100, Math.round((status.players / status.maxPlayers) * 100) || 1);

  return (
    <div className="relative">
      {/* ================= HERO ================= */}
      <Section className="pt-16 pb-20 sm:pt-24 sm:pb-28">
        <AuroraBlobs />
        <div className="relative text-center">
          <div className="mx-auto inline-flex items-center gap-2 rounded-full border border-brand/35 bg-brand/10 px-4 py-1.5 shadow-neon-pink">
            <Sparkles className="h-3.5 w-3.5 text-brand" />
            <span className="eyebrow text-brand">SaaS Платформа &amp; Мультиплеерный Движок • 2026</span>
          </div>

          <h1 className="mx-auto mt-7 max-w-4xl text-[2.6rem] font-black leading-[1.05] text-white sm:text-6xl lg:text-[4.25rem]">
            Автономный мультиплеерный движок для вашего{' '}
            <span className="text-gradient-brand glow-pink">RP-проекта в GTA&nbsp;V</span>
          </h1>

          <p className="mx-auto mt-6 max-w-2xl text-[15px] leading-relaxed text-slate-400 sm:text-lg">
            Полная независимость от Take-Two, RAGE и alt:V backend. C# .NET 8 ядро, нативный WebRTC 3D
            Voice, синхронизация NPC и архитектура, спроектированная под 1500+ игроков.
          </p>

          <div className="mt-9 flex flex-col items-center justify-center gap-3 sm:flex-row">
            <Link href="/auth/register" className="btn btn-primary h-12 w-full px-7 text-sm sm:w-auto">
              Подключить проект
              <ArrowRight className="h-4 w-4" />
            </Link>
            <Link href="/docs" className="btn btn-ghost h-12 w-full px-7 text-sm font-semibold sm:w-auto">
              <Terminal className="h-4 w-4" />
              Документация API
            </Link>
          </div>

          {/* Live server status */}
          <div className="mx-auto mt-14 max-w-2xl">
            <div className="glass-panel card-edge rounded-2xl p-5 text-left shadow-glass">
              <div className="flex flex-wrap items-center justify-between gap-4">
                <div className="flex items-center gap-3">
                  <span className={status.online ? 'status-dot text-emeraldx' : 'status-dot text-red-400'} />
                  <div>
                    <div className="flex items-center gap-2 text-sm font-bold text-white">
                      {status.name}
                      <Badge tone={status.online ? 'emerald' : 'red'}>
                        {status.online ? 'Online' : 'Offline'}
                      </Badge>
                    </div>
                    <div className="mt-0.5 font-mono text-[11px] text-slate-500">
                      {status.host}:{status.port} · {status.version}
                    </div>
                  </div>
                </div>

                <div className="flex items-center gap-4">
                  <div className="text-right">
                    <div className="font-mono text-[10px] uppercase tracking-wider text-slate-500">Пинг</div>
                    <div className="font-mono text-sm font-bold text-white">{status.pingMs} ms</div>
                  </div>
                  <div className="text-right">
                    <div className="font-mono text-[10px] uppercase tracking-wider text-slate-500">Игроки</div>
                    <div className="font-mono text-sm font-bold text-white">
                      {status.players} / {status.maxPlayers}
                    </div>
                  </div>
                  <button
                    onClick={copyIp}
                    className="btn btn-ghost h-10 w-10 shrink-0 p-0"
                    title="Скопировать IP"
                  >
                    {copied ? <Check className="h-4 w-4 text-emeraldx" /> : <Copy className="h-4 w-4" />}
                  </button>
                </div>
              </div>

              <div className="mt-4 h-1.5 w-full overflow-hidden rounded-full bg-white/5">
                <div
                  className="h-full rounded-full bg-gradient-to-r from-brand to-violetx transition-all duration-700"
                  style={{ width: `${fill}%` }}
                />
              </div>
              <div className="mt-2 flex items-center justify-between font-mono text-[10px] text-slate-500">
                <span>FastDL CDN активен</span>
                <span>1500 слотов · UDP 7788</span>
              </div>
            </div>
          </div>
        </div>
      </Section>

      {/* ================= PILLARS ================= */}
      <Section className="pb-8">
        <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
          {PILLARS.map((p) => (
            <div key={p.n} className="glass card-edge glass-hover group rounded-2xl p-7">
              <div className="font-mono text-4xl font-black text-white/10 transition-colors group-hover:text-brand/70">
                {p.n}
              </div>
              <h3 className="mt-4 text-base font-bold text-white">{p.title}</h3>
              <p className="mt-2.5 text-[13px] leading-relaxed text-slate-400">{p.body}</p>
            </div>
          ))}
        </div>
      </Section>

      {/* ================= PLATFORM / ABOUT ================= */}
      <Section id="platform" bleed className="py-24">
        <div className="grid grid-cols-1 items-center gap-14 lg:grid-cols-2">
          <div>
            <span className="eyebrow text-brand">О платформе</span>
            <h2 className="mt-3 text-3xl font-black text-white sm:text-[2.6rem] sm:leading-[1.1]">
              Суверенная среда для RP-проектов нового поколения
            </h2>
            <div className="mt-6 space-y-4 text-[14px] leading-relaxed text-slate-400">
              <p>
                В 2026 году индустрия столкнулась с беспрецедентными закрытиями: Take-Two закрыла
                RAGE:MP и отключила backend alt:V. <strong className="text-white">FloV:MP</strong> —
                это полностью автономный рантайм на базе отвязанных бинарников alt:V v16.4.39, который
                не обращается к серверам правообладателя.
              </p>
              <p>
                Создатели RP-проектов получают 100% контроль над кодом, базой данных и брендингом
                лаунчера. Хостинг, FastDL CDN и криптографический реестр лицензий — на нашей стороне.
              </p>
              <p>
                На движке уже развёрнут флагманский проект{' '}
                <strong className="text-brand">«Держава Онлайн»</strong> — карта Москвы, 8-уровневая
                админ-система, экономика и готовый кастомный лаунчер.
              </p>
            </div>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link href="/auth/register" className="btn btn-primary h-11 px-5 text-xs">
                Получить лицензию разработчика
                <ArrowRight className="h-4 w-4" />
              </Link>
              <Link href="/#pricing" className="btn btn-ghost h-11 px-5 text-xs font-semibold">
                Сравнить тарифы
              </Link>
            </div>
          </div>

          <div className="card-edge group relative overflow-hidden rounded-3xl border border-white/10 shadow-glass">
            <div className="absolute inset-0 z-10 bg-gradient-to-t from-ink-950 via-ink-950/10 to-transparent" />
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src="/branding/hero-showcase.png"
              alt="FloV:MP Engine"
              className="aspect-[4/3] w-full object-cover transition-transform duration-700 group-hover:scale-[1.04]"
            />
            <div className="absolute inset-x-5 bottom-5 z-20 flex items-end justify-between">
              <div>
                <div className="eyebrow text-brand">FloV:MP Engine v16.4.39</div>
                <div className="mt-1 text-sm font-bold text-white">Модуль ядра C# CoreCLR</div>
              </div>
              <Badge tone="slate">100% автономно</Badge>
            </div>
          </div>
        </div>
      </Section>

      {/* ================= FEATURES ================= */}
      <Section id="features" className="py-24">
        <SectionHeading
          eyebrow="Возможности движка"
          title="Полный стек технологий для масштабирования"
          sub="Всё, что нужно для запуска зрелого RP-проекта, работает из коробки — без сборки сторонних плагинов."
        />
        <div className="grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3">
          {FEATURES.map((f) => (
            <div key={f.title} className="glass card-edge glass-hover rounded-2xl p-7">
              <div className={`flex h-11 w-11 items-center justify-center rounded-xl border ${f.ring} ${f.tone}`}>
                <f.icon className="h-5 w-5" />
              </div>
              <h3 className="mt-5 text-[17px] font-bold text-white">{f.title}</h3>
              <p className="mt-2.5 text-[13px] leading-relaxed text-slate-400">{f.body}</p>
            </div>
          ))}
        </div>
      </Section>

      {/* ================= ARCHITECTURE TERMINAL ================= */}
      <Section id="architecture" bleed className="py-24">
        <div className="grid grid-cols-1 items-center gap-14 lg:grid-cols-[0.9fr_1.1fr]">
          <div>
            <span className="eyebrow text-cyber">Стек &amp; инфраструктура</span>
            <h2 className="mt-3 text-3xl font-black text-white sm:text-[2.6rem] sm:leading-[1.1]">
              Конфигурация, готовая к производству
            </h2>
            <p className="mt-5 text-[14px] leading-relaxed text-slate-400">
              Интеграция занимает минуты. Движок валидирует ключ по защищённому HTTPS API,
              привязывается к выделенному IP вашего VDS и поднимает сетевой стек.
            </p>
            <ul className="mt-6 space-y-2.5">
              {[
                ['Сетевой рантайм', 'altv-server v16.4.39 (unhooked)'],
                ['Серверный рантайм', 'Microsoft.NETCore.App 8.0.x'],
                ['База данных', 'MariaDB 10.6+ / MySQL 8.0'],
                ['FastDL', 'Nginx HTTP/2 Static Alias'],
              ].map(([k, v]) => (
                <li
                  key={k}
                  className="flex items-center gap-2.5 rounded-xl border border-white/[0.06] bg-white/[0.02] px-3.5 py-2.5 font-mono text-[12px] text-slate-300"
                >
                  <BadgeCheck className="h-4 w-4 shrink-0 text-emeraldx" />
                  <span className="text-slate-500">{k}:</span>
                  <strong className="text-white">{v}</strong>
                </li>
              ))}
            </ul>
          </div>

          <div className="glass-panel card-edge overflow-hidden rounded-2xl shadow-glass">
            <div className="flex items-center gap-2 border-b border-white/[0.08] px-4 py-3">
              <span className="h-3 w-3 rounded-full bg-red-500/80" />
              <span className="h-3 w-3 rounded-full bg-amber-500/80" />
              <span className="h-3 w-3 rounded-full bg-emeraldx/80" />
              <div className="ml-3 flex gap-1 overflow-x-auto no-scrollbar">
                {TERMINAL_TABS.map((t) => (
                  <button
                    key={t.id}
                    onClick={() => setTab(t.id)}
                    className={`rounded-md px-2.5 py-1 font-mono text-[11px] transition-colors ${
                      tab === t.id ? 'bg-white/10 text-white' : 'text-slate-500 hover:text-slate-300'
                    }`}
                  >
                    {t.file}
                  </button>
                ))}
              </div>
              <span className="ml-auto hidden font-mono text-[10px] text-brand sm:block">{activeTab.lang}</span>
            </div>
            <pre className="terminal-scroll max-h-[420px] overflow-auto p-5 font-mono text-[12px] leading-relaxed text-slate-300">
              <code>{activeTab.code}</code>
            </pre>
          </div>
        </div>
      </Section>

      {/* ================= PROCESS ================= */}
      <Section id="process" className="py-24">
        <SectionHeading
          eyebrow="Пошаговый процесс"
          title="6 шагов до старта вашего проекта"
          sub="От регистрации до приёма игроков — по нашему гайду это занимает один вечер."
        />
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {STEPS.map((s, i) => (
            <div key={s.t} className="glass card-edge relative rounded-2xl p-6">
              <div className="flex items-center gap-3">
                <span className="flex h-9 w-9 items-center justify-center rounded-xl border border-brand/30 bg-brand/10 font-mono text-sm font-black text-brand">
                  {String(i + 1).padStart(2, '0')}
                </span>
                <h4 className="text-[15px] font-bold text-white">{s.t}</h4>
              </div>
              <p className="mt-3 text-[13px] leading-relaxed text-slate-400">{s.d}</p>
            </div>
          ))}
        </div>
      </Section>

      {/* ================= PRICING ================= */}
      <Section id="pricing" bleed className="py-24">
        <SectionHeading
          eyebrow="Тарифные планы"
          title="Прозрачные условия без скрытых платежей"
          sub="Стартовая лицензия Indie выдаётся бесплатно сразу после регистрации."
        />

        <div className="mb-12 flex justify-center">
          <div className="inline-flex rounded-2xl border border-white/10 bg-ink-800/60 p-1.5">
            {(
              [
                ['month', '1 месяц'],
                ['halfYear', '6 месяцев'],
                ['year', '1 год'],
              ] as const
            ).map(([key, label]) => (
              <button
                key={key}
                onClick={() => setPeriod(key)}
                className={`relative rounded-xl px-4 py-2 text-xs font-bold transition-all ${
                  period === key ? 'bg-brand text-white shadow-neon-pink' : 'text-slate-400 hover:text-white'
                }`}
              >
                {label}
                {key !== 'month' && (
                  <span
                    className={`ml-1.5 rounded px-1 py-0.5 font-mono text-[9px] ${
                      period === key ? 'bg-white/20 text-white' : 'bg-emeraldx/15 text-emeraldx'
                    }`}
                  >
                    {key === 'halfYear' ? '−15%' : '−30%'}
                  </span>
                )}
              </button>
            ))}
          </div>
        </div>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          {/* Indie */}
          <div className="glass card-edge flex flex-col rounded-3xl p-8">
            <span className="eyebrow text-slate-400">Trial / Разработка</span>
            <h3 className="mt-3 text-2xl font-bold text-white">Инди</h3>
            <p className="mt-2 text-[13px] text-slate-400">
              Для создания и тестирования нового сервера небольшой командой.
            </p>
            <div className="mt-6 font-mono text-4xl font-black text-white">
              0 ₽
              <span className="ml-1 text-sm font-normal text-slate-500">· 30 дней</span>
            </div>
            <ul className="mt-7 flex-1 space-y-3 text-[13px] text-slate-300">
              {['До 128 игроков онлайн', 'Автономные бинарники движка', 'C# Gamemode SDK', 'Привязка 1 IP-адреса'].map((x) => (
                <li key={x} className="flex items-center gap-2.5">
                  <Check className="h-4 w-4 shrink-0 text-emeraldx" />
                  {x}
                </li>
              ))}
            </ul>
            <Link href="/auth/register" className="btn btn-ghost mt-8 h-11 text-sm font-semibold">
              Начать бесплатно
            </Link>
          </div>

          {/* RP Проект — featured */}
          <div className="glass-panel card-edge relative flex flex-col rounded-3xl border-brand/40 p-8 shadow-neon-pink">
            <span className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-brand px-3 py-1 font-mono text-[10px] font-bold uppercase tracking-wider text-white">
              Популярный выбор
            </span>
            <span className="eyebrow text-brand">Pro / Готовый проект</span>
            <h3 className="mt-3 text-2xl font-bold text-white">RP Проект</h3>
            <p className="mt-2 text-[13px] text-slate-400">
              Для запуска полноценного проекта с готовыми игровыми системами.
            </p>
            <div className="mt-6 font-mono text-4xl font-black text-white">
              {price.pro}
              <span className="ml-1 text-sm font-normal text-slate-500">{price.proNote}</span>
            </div>
            <ul className="mt-7 flex-1 space-y-3 text-[13px] text-slate-300">
              {[
                'До 512 игроков онлайн',
                'C# гейммод «Держава Онлайн»',
                '3D Voice WebRTC + синхронизация NPC',
                'MariaDB-схема + 8-ранговая админка',
                'FastDL CDN конфигурация',
              ].map((x) => (
                <li key={x} className="flex items-center gap-2.5">
                  <Check className="h-4 w-4 shrink-0 text-brand" />
                  {x}
                </li>
              ))}
            </ul>
            <Link href="/auth/register" className="btn btn-primary mt-8 h-11 text-sm">
              Подключить RP Проект
            </Link>
          </div>

          {/* Enterprise */}
          <div className="glass card-edge flex flex-col rounded-3xl p-8">
            <span className="eyebrow text-cyber">Enterprise / Франшиза</span>
            <h3 className="mt-3 text-2xl font-bold text-white">Enterprise</h3>
            <p className="mt-2 text-[13px] text-slate-400">
              Для крупных проектов: брендированный лаунчер и миграция «под ключ».
            </p>
            <div className="mt-6 font-mono text-4xl font-black text-white">
              {price.ent}
              <span className="ml-1 text-sm font-normal text-slate-500">{price.entNote}</span>
            </div>
            <ul className="mt-7 flex-1 space-y-3 text-[13px] text-slate-300">
              {[
                '1500+ игроков (максимум)',
                'Свой брендированный лаунчер',
                'Миграция базы с RAGE:MP / FiveM',
                'Выделенная поддержка · SLA 99.9%',
              ].map((x) => (
                <li key={x} className="flex items-center gap-2.5">
                  <Check className="h-4 w-4 shrink-0 text-cyber" />
                  {x}
                </li>
              ))}
            </ul>
            <Link
              href="/auth/register"
              className="btn mt-8 h-11 border border-cyber/40 bg-cyber/10 text-sm font-semibold text-cyber transition hover:bg-cyber/20"
            >
              Заказать Enterprise
            </Link>
          </div>
        </div>
      </Section>

      {/* ================= FAQ ================= */}
      <Section id="faq" className="py-24">
        <SectionHeading eyebrow="Частые вопросы" title="Коротко о главном" />
        <div className="mx-auto max-w-3xl space-y-3">
          {FAQ.map((item, i) => {
            const open = openFaq === i;
            return (
              <div key={item.q} className="glass card-edge overflow-hidden rounded-2xl">
                <button
                  onClick={() => setOpenFaq(open ? null : i)}
                  className="flex w-full items-center justify-between gap-4 px-6 py-5 text-left"
                >
                  <span className="text-[15px] font-semibold text-white">{item.q}</span>
                  <ChevronDown
                    className={`h-4 w-4 shrink-0 text-brand transition-transform duration-300 ${
                      open ? 'rotate-180' : ''
                    }`}
                  />
                </button>
                <div
                  className={`grid transition-all duration-300 ${
                    open ? 'grid-rows-[1fr] opacity-100' : 'grid-rows-[0fr] opacity-0'
                  }`}
                >
                  <div className="overflow-hidden">
                    <p className="px-6 pb-5 text-[13px] leading-relaxed text-slate-400">{item.a}</p>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </Section>

      {/* ================= CTA ================= */}
      <Section className="pb-28">
        <div className="glass-panel card-edge relative overflow-hidden rounded-3xl border-brand/30 p-10 text-center shadow-neon-pink sm:p-16">
          <AuroraBlobs />
          <div className="relative">
            <h2 className="mx-auto max-w-2xl text-3xl font-black text-white sm:text-4xl">
              Подключите свой независимый RP-сервер сегодня
            </h2>
            <p className="mx-auto mt-4 max-w-xl text-[14px] leading-relaxed text-slate-400">
              Расскажем, как FloV:MP сохранит ваш проект стабильным и автономным, и поможем перенести
              наработки с других мультиплееров.
            </p>
            <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
              <a
                href="https://t.me/flovmp_dev"
                target="_blank"
                rel="noopener noreferrer"
                className="btn btn-primary h-12 w-full px-7 text-sm sm:w-auto"
              >
                <Send className="h-4 w-4" />
                Написать в Telegram
              </a>
              <Link href="/auth/register" className="btn btn-ghost h-12 w-full px-7 text-sm font-semibold sm:w-auto">
                Создать аккаунт на портале
              </Link>
            </div>
          </div>
        </div>
      </Section>
    </div>
  );
}
