'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import {
  Zap,
  Server,
  Shield,
  Radio,
  Users,
  HardDrive,
  Cpu,
  ArrowRight,
  CheckCircle2,
  Copy,
  Check,
  Download,
  Flame,
  Layers,
  Code,
  Terminal,
  Activity,
  Award,
  Sparkles,
  ChevronRight,
  Send,
  HelpCircle,
  ExternalLink
} from 'lucide-react';

interface ServerStatus {
  online: boolean;
  name: string;
  players: number;
  maxPlayers: number;
  pingMs: number;
  host: string;
  port: number;
  version: string;
}

export default function HomePage() {
  const [billingPeriod, setBillingPeriod] = useState<'month' | 'halfYear' | 'year'>('month');
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

  const [copied, setCopied] = useState(false);

  useEffect(() => {
    fetch('/api/server-status')
      .then((r) => r.json())
      .then((data) => {
        if (data) setStatus(data);
      })
      .catch(() => {});
  }, []);

  const copyIp = () => {
    navigator.clipboard.writeText(`${status.host}:${status.port}`);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  // Pricing calculations based on billing period
  const getPrices = () => {
    switch (billingPeriod) {
      case 'halfYear':
        return {
          pro: '12 900 ₽',
          proPeriod: '/ месяц (скидка 15%)',
          enterprise: '42 000 ₽',
          enterprisePeriod: '/ лицензия',
        };
      case 'year':
        return {
          pro: '9 900 ₽',
          proPeriod: '/ месяц (скидка 33%)',
          enterprise: '36 000 ₽',
          enterprisePeriod: '/ лицензия',
        };
      default:
        return {
          pro: '14 900 ₽',
          proPeriod: '/ месяц',
          enterprise: '49 000 ₽',
          enterprisePeriod: '/ лицензия',
        };
    }
  };

  const prices = getPrices();

  return (
    <div className="relative overflow-hidden">
      {/* Glow Backdrops */}
      <div className="absolute top-0 left-1/2 -translate-x-1/2 w-full max-w-7xl h-[650px] pointer-events-none">
        <div className="absolute -top-32 left-1/4 w-[500px] h-[500px] bg-brand/20 rounded-full blur-[140px]"></div>
        <div className="absolute top-10 right-1/4 w-[450px] h-[450px] bg-purple-600/15 rounded-full blur-[140px]"></div>
      </div>

      {/* Hero Section */}
      <section className="relative pt-16 pb-24 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto text-center">
        {/* Top Tagline Badge */}
        <div className="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-brand/40 bg-brand/10 text-brand text-xs font-bold uppercase tracking-wider mb-8 shadow-neon-pink">
          <Flame className="w-4 h-4 text-brand" />
          <span>SaaS Платформа и Мультиплеерный Движок • 2026</span>
        </div>

        <h1 className="text-4xl sm:text-6xl lg:text-7xl font-black tracking-tight text-white leading-tight max-w-5xl mx-auto">
          Автономный мультиплеерный движок для вашего{' '}
          <span className="text-transparent bg-clip-text bg-gradient-to-r from-brand via-pink-400 to-purple-400">
            RP-проекта в GTA V
          </span>
        </h1>

        <p className="mt-6 text-lg sm:text-xl text-gray-300 max-w-3xl mx-auto leading-relaxed">
          <strong className="text-white font-semibold">FloV:MP</strong> обеспечивает полную независимость от Take-Two и закрытых мастер-серверов. C# .NET 8 ядро, нативный WebRTC 3D Voice, синхронизация NPC и архитектура под 1500+ игроков.
        </p>

        {/* Action Buttons (icsnotify style: Direct Connect + What it does) */}
        <div className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4">
          <Link
            href="/auth/register"
            className="w-full sm:w-auto px-8 py-4 rounded-2xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white font-bold text-base shadow-neon-pink flex items-center justify-center gap-3 transition-all transform hover:-translate-y-0.5"
          >
            <span>Подключить FloV:MP</span>
            <ArrowRight className="w-5 h-5" />
          </Link>

          <a
            href="#services"
            className="w-full sm:w-auto px-8 py-4 rounded-2xl glass-panel border border-white/10 hover:border-brand/40 text-gray-200 font-semibold text-base flex items-center justify-center gap-2 transition-all hover:bg-white/5"
          >
            <span>Что умеет движок</span>
          </a>
        </div>

        {/* 3 Value Pillars (01, 02, 03) like icsnotify.ru */}
        <div className="mt-16 grid grid-cols-1 md:grid-cols-3 gap-6 max-w-5xl mx-auto text-left">
          <div className="glass-panel p-6 rounded-2xl border border-white/10 relative overflow-hidden group hover:border-brand/40 transition-colors">
            <div className="text-3xl font-black font-mono text-brand/40 mb-3 group-hover:text-brand transition-colors">
              01
            </div>
            <h3 className="text-base font-bold text-white mb-2">Полная автономия</h3>
            <p className="text-xs text-gray-400 leading-relaxed">
              Сервер стартует и работает полностью независимо на вашем собственном VDS без внешних мастер-серверов и рисков блокировки.
            </p>
          </div>

          <div className="glass-panel p-6 rounded-2xl border border-white/10 relative overflow-hidden group hover:border-brand/40 transition-colors">
            <div className="text-3xl font-black font-mono text-brand/40 mb-3 group-hover:text-brand transition-colors">
              02
            </div>
            <h3 className="text-base font-bold text-white mb-2">Нативный 3D Voice WebRTC</h3>
            <p className="text-xs text-gray-400 leading-relaxed">
              Пространственный звук, радиостанции фракций, мегафоны и звуковые зоны без TeamSpeak и сторонних клиентских плагинов.
            </p>
          </div>

          <div className="glass-panel p-6 rounded-2xl border border-white/10 relative overflow-hidden group hover:border-brand/40 transition-colors">
            <div className="text-3xl font-black font-mono text-brand/40 mb-3 group-hover:text-brand transition-colors">
              03
            </div>
            <h3 className="text-base font-bold text-white mb-2">Любой масштаб и онлайн</h3>
            <p className="text-xs text-gray-400 leading-relaxed">
              Современное C# .NET 8 ядро, асинхронный MariaDB пул и архитектура без GC-пауз, готовая к нагрузке до 1500+ игроков.
            </p>
          </div>
        </div>

        {/* Live VDS Node Status Ticker */}
        <div className="mt-12 max-w-2xl mx-auto p-4 rounded-2xl glass-panel border border-white/10 flex flex-wrap items-center justify-between gap-4 text-left shadow-glass">
          <div className="flex items-center gap-3">
            <span className="relative flex h-3 w-3">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-3 w-3 bg-emerald-500"></span>
            </span>
            <div>
              <div className="text-sm font-bold text-white flex items-center gap-2">
                <span>{status.name}</span>
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-mono">
                  ONLINE
                </span>
              </div>
              <div className="text-xs text-gray-400 font-mono">
                {status.host}:{status.port} • UDP Core v16.4-custom
              </div>
            </div>
          </div>

          <div className="flex items-center gap-4">
            <div className="text-right">
              <div className="text-[10px] text-gray-400 uppercase tracking-wider font-mono">Игроки онлайн</div>
              <div className="text-sm font-bold text-white font-mono">
                {status.players} / {status.maxPlayers}
              </div>
            </div>
            <button
              onClick={copyIp}
              className="p-2.5 rounded-xl bg-surface-300 border border-white/10 hover:border-brand/40 text-gray-300 hover:text-white transition-colors"
              title="Скопировать IP сервера"
            >
              {copied ? <Check className="w-4 h-4 text-emerald-400" /> : <Copy className="w-4 h-4" />}
            </button>
          </div>
        </div>
      </section>

      {/* About Section (О платформе - like icsnotify.ru) */}
      <section id="about" className="py-20 bg-surface-300/40 border-y border-white/5 px-4 sm:px-6 lg:px-8">
        <div className="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-2 gap-12 items-center">
          <div>
            <div className="text-xs font-bold text-brand uppercase tracking-widest mb-2 font-mono">
              О платформе
            </div>
            <h2 className="text-3xl sm:text-4xl font-black text-white mb-6">
              Суверенная среда для RP-проектов нового поколения
            </h2>
            <div className="space-y-4 text-sm text-gray-300 leading-relaxed">
              <p>
                <strong className="text-white">FloV:MP</strong> — это независимый мультиплеерный движок и технологическая SaaS-платформа для GTA V. В 2026 году индустрия столкнулась с беспрецедентными закрытиями: Take-Two закрыла RAGE:MP и отключила alt:V, оставив разработчиков без выбора.
              </p>
              <p>
                Мы создали полностью автономный рантайм, который не обращается к серверам правообладателя, не зависит от сторонней модерации и даёт создателям RP-проектов 100% контроль над кодом, базой данных и брендингом лаунчера.
              </p>
              <p>
                На базе движка FloV:MP уже развернут флагманский проект <strong className="text-brand">«Держава Онлайн»</strong> с картой Москвы, 8-уровневой системой администрирования, продвинутой экономикой и готовым лаунчером.
              </p>
            </div>

            <div className="mt-8 flex items-center gap-4">
              <Link
                href="/auth/register"
                className="px-6 py-3 rounded-xl bg-brand hover:bg-brand-hover text-white text-xs font-bold shadow-neon-pink transition-all flex items-center gap-2"
              >
                <span>Получить лицензию разработчика</span>
                <ArrowRight className="w-4 h-4" />
              </Link>
            </div>
          </div>

          {/* Visual Showcase Render with Codex Logo */}
          <div className="relative rounded-3xl overflow-hidden border border-white/10 shadow-glass group">
            <div className="absolute inset-0 bg-gradient-to-t from-surface-400 via-transparent to-transparent z-10"></div>
            <img
              src="/branding/hero-showcase.png"
              alt="FloV:MP Architecture"
              className="w-full h-auto object-cover group-hover:scale-105 transition-transform duration-700"
            />
            <div className="absolute bottom-6 left-6 right-6 z-20 flex items-center justify-between">
              <div>
                <span className="text-xs uppercase tracking-widest text-brand font-mono font-bold">
                  FloV:MP Engine v16.4.39
                </span>
                <h4 className="text-base font-bold text-white">Модуль ядра C# CoreCLR</h4>
              </div>
              <span className="px-3 py-1 rounded-full bg-surface-300/80 backdrop-blur-md border border-white/10 text-xs font-mono text-gray-300">
                100% Автономно
              </span>
            </div>
          </div>
        </div>
      </section>

      {/* Services / Features (6 Cards like icsnotify.ru) */}
      <section id="services" className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
        <div className="text-center max-w-3xl mx-auto mb-16">
          <h2 className="text-xs font-bold text-brand uppercase tracking-widest mb-2 font-mono">
            Возможности движка
          </h2>
          <p className="text-3xl sm:text-4xl font-black text-white">
            Полный стек технологий для масштабирования
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
          {/* Card 1 */}
          <div className="glass-panel glass-panel-hover p-8 rounded-3xl border border-white/10">
            <div className="w-12 h-12 rounded-2xl bg-brand/20 border border-brand/40 flex items-center justify-center text-brand mb-6 shadow-neon-pink">
              <Radio className="w-6 h-6" />
            </div>
            <h3 className="text-xl font-bold text-white mb-3">Встроенный 3D Voice Chat</h3>
            <p className="text-gray-400 text-sm leading-relaxed">
              Нативный WebRTC + кодек Opus без внешних программ (TeamSpeak). Пространственное 3D-позиционирование, рации фракций, мегафоны и звуковые зоны с эффектом эха.
            </p>
          </div>

          {/* Card 2 */}
          <div className="glass-panel glass-panel-hover p-8 rounded-3xl border border-white/10">
            <div className="w-12 h-12 rounded-2xl bg-blue-500/20 border border-blue-500/40 flex items-center justify-center text-blue-400 mb-6">
              <Users className="w-6 h-6" />
            </div>
            <h3 className="text-xl font-bold text-white mb-3">Синхронизация NPC и Трафика</h3>
            <p className="text-gray-400 text-sm leading-relaxed">
              Полноценная синхронизация педов и трафика. Живой город с ботами, квестовые NPC, боевые противники в рейдах и охрана государственных объектов.
            </p>
          </div>

          {/* Card 3 */}
          <div className="glass-panel glass-panel-hover p-8 rounded-3xl border border-white/10">
            <div className="w-12 h-12 rounded-2xl bg-purple-500/20 border border-purple-500/40 flex items-center justify-center text-purple-400 mb-6">
              <Cpu className="w-6 h-6" />
            </div>
            <h3 className="text-xl font-bold text-white mb-3">C# .NET 8 CoreCLR Ядро</h3>
            <p className="text-gray-400 text-sm leading-relaxed">
              Гейммод на современном .NET 8. Строгая типизация, пул асинхронных соединений MariaDB/MySQL, 0% GC-пауз в сетевых тиках и максимальная скорость выполнения.
            </p>
          </div>

          {/* Card 4 */}
          <div className="glass-panel glass-panel-hover p-8 rounded-3xl border border-white/10">
            <div className="w-12 h-12 rounded-2xl bg-emerald-500/20 border border-emerald-500/40 flex items-center justify-center text-emerald-400 mb-6">
              <HardDrive className="w-6 h-6" />
            </div>
            <h3 className="text-xl font-bold text-white mb-3">Потоковый FastDL CDN</h3>
            <p className="text-gray-400 text-sm leading-relaxed">
              Мгновенная загрузка ресурсов, кастомных автомобилей, скинов и карт через параллельный HTTP/FastDL Nginx кэш без нагрузки на игровой UDP-порт.
            </p>
          </div>

          {/* Card 5 */}
          <div className="glass-panel glass-panel-hover p-8 rounded-3xl border border-white/10">
            <div className="w-12 h-12 rounded-2xl bg-amber-500/20 border border-amber-500/40 flex items-center justify-center text-amber-400 mb-6">
              <Shield className="w-6 h-6" />
            </div>
            <h3 className="text-xl font-bold text-white mb-3">Античит & Защита Инвентаря</h3>
            <p className="text-gray-400 text-sm leading-relaxed">
              Серверный контроль всех транзакций, движения и оружия. Проверка хэшей клиентских файлов при старте лаунчера и защита от инъекций памяти.
            </p>
          </div>

          {/* Card 6 */}
          <div className="glass-panel glass-panel-hover p-8 rounded-3xl border border-white/10">
            <div className="w-12 h-12 rounded-2xl bg-cyan-500/20 border border-cyan-500/40 flex items-center justify-center text-cyan-400 mb-6">
              <Layers className="w-6 h-6" />
            </div>
            <h3 className="text-xl font-bold text-white mb-3">NUI & HTML5/React Интерфейсы</h3>
            <p className="text-gray-400 text-sm leading-relaxed">
              Любой интерфейс создается на привычном веб-стеке (React, Vue, Tailwind). Мощный CEF-движок гарантирует 60+ FPS в инвентаре, худе и автосалоне.
            </p>
          </div>
        </div>
      </section>

      {/* Tech Stack & Code Showcase */}
      <section id="tech" className="py-20 bg-surface-300/40 border-y border-white/5 px-4 sm:px-6 lg:px-8">
        <div className="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-2 gap-12 items-center">
          <div>
            <div className="text-xs font-bold text-brand uppercase tracking-widest mb-2 font-mono">
              Стек & Инфраструктура
            </div>
            <h2 className="text-3xl sm:text-4xl font-black text-white mb-6">
              Конфигурация, готовая к производству
            </h2>
            <p className="text-gray-300 leading-relaxed mb-6 text-sm">
              Интеграция с платформой FloV:MP занимает считанные минуты. Движок валидирует лицензионный ключ через защищенный HTTPS API, привязывается к выделенному IP вашего VDS и запускает сетевой стек.
            </p>

            <div className="space-y-3 font-mono text-xs text-gray-300">
              <div className="flex items-center gap-2 p-3 rounded-xl bg-surface-300 border border-white/5">
                <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                <span>Сетевой рантайм: <strong>altv-server v16.4.39 (unhooked)</strong></span>
              </div>
              <div className="flex items-center gap-2 p-3 rounded-xl bg-surface-300 border border-white/5">
                <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                <span>Серверный рантайм: <strong>Microsoft.NETCore.App 8.0.x</strong></span>
              </div>
              <div className="flex items-center gap-2 p-3 rounded-xl bg-surface-300 border border-white/5">
                <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                <span>База данных: <strong>MariaDB 10.6+ / MySQL 8.0</strong></span>
              </div>
              <div className="flex items-center gap-2 p-3 rounded-xl bg-surface-300 border border-white/5">
                <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                <span>FastDL: <strong>Nginx HTTP/2 Static Alias</strong></span>
              </div>
            </div>
          </div>

          {/* Code Window */}
          <div className="glass-panel p-6 rounded-3xl border border-white/10 shadow-glass">
            <div className="flex items-center justify-between pb-4 mb-4 border-b border-white/10 font-mono text-xs text-gray-400">
              <div className="flex items-center gap-2">
                <span className="w-3 h-3 rounded-full bg-red-500/80"></span>
                <span className="w-3 h-3 rounded-full bg-yellow-500/80"></span>
                <span className="w-3 h-3 rounded-full bg-emerald-500/80"></span>
                <span className="ml-2 text-gray-300">server.toml</span>
              </div>
              <span className="text-brand">C# .NET 8</span>
            </div>
            <pre className="font-mono text-xs text-gray-300 overflow-x-auto leading-relaxed">
{`# Конфигурация автономного сервера FloV:MP
name = "Держава Онлайн"
host = "0.0.0.0"
port = 7788
players = 1500
modules = ["csharp-module"]
resources = ["flovmp-gamemode"]

# Валидация лицензии в SaaS-реестре FloV:MP
[licensing]
key = "FLV-ENTERPRISE-2026-DERZHAVA"
auth_endpoint = "https://flovmp.ru/api/v1/license/verify"
bound_ip = "188.127.229.224"

# Раздача кастомного транспорта и скинов через FastDL
[cdn]
useExternalCDN = true
cdnUrl = "http://188.127.229.224/cdn"`}
            </pre>
          </div>
        </div>
      </section>

      {/* 6 Steps Process (01-06 timeline exactly like icsnotify.ru) */}
      <section id="process" className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
        <div className="text-center max-w-3xl mx-auto mb-16">
          <h2 className="text-xs font-bold text-brand uppercase tracking-widest mb-2 font-mono">
            Пошаговый процесс
          </h2>
          <p className="text-3xl sm:text-4xl font-black text-white">
            Запуск FloV:MP в 6 шагов
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {/* Step 1 */}
          <div className="glass-panel p-6 rounded-2xl border border-white/10 relative">
            <span className="text-2xl font-black font-mono text-brand mb-2 block">01</span>
            <h4 className="text-base font-bold text-white mb-2">Заявка и регистрация</h4>
            <p className="text-xs text-gray-400 leading-relaxed">
              Создайте аккаунт на портале и моментально получите стартовый лицензионный ключ разработчика.
            </p>
          </div>

          {/* Step 2 */}
          <div className="glass-panel p-6 rounded-2xl border border-white/10 relative">
            <span className="text-2xl font-black font-mono text-brand mb-2 block">02</span>
            <h4 className="text-base font-bold text-white mb-2">Привязка IP сервера</h4>
            <p className="text-xs text-gray-400 leading-relaxed">
              В личном кабинете укажите публичный IPv4-адрес вашего VDS/Dedicated сервера для безопасной привязки.
            </p>
          </div>

          {/* Step 3 */}
          <div className="glass-panel p-6 rounded-2xl border border-white/10 relative">
            <span className="text-2xl font-black font-mono text-brand mb-2 block">03</span>
            <h4 className="text-base font-bold text-white mb-2">Скачивание дистрибутива</h4>
            <p className="text-xs text-gray-400 leading-relaxed">
              Скачайте чистый серверный архив движка под Ubuntu Linux или Windows Server прямо из личного кабинета.
            </p>
          </div>

          {/* Step 4 */}
          <div className="glass-panel p-6 rounded-2xl border border-white/10 relative">
            <span className="text-2xl font-black font-mono text-brand mb-2 block">04</span>
            <h4 className="text-base font-bold text-white mb-2">Настройка server.toml</h4>
            <p className="text-xs text-gray-400 leading-relaxed">
              Вставьте полученный лицензионный ключ в конфигурационный файл и настройте FastDL CDN URL.
            </p>
          </div>

          {/* Step 5 */}
          <div className="glass-panel p-6 rounded-2xl border border-white/10 relative">
            <span className="text-2xl font-black font-mono text-brand mb-2 block">05</span>
            <h4 className="text-base font-bold text-white mb-2">Подключение гейммода</h4>
            <p className="text-xs text-gray-400 leading-relaxed">
              Используйте готовый C# .NET 8 гейммод (Держава RP) либо портируйте свою логику с RAGE:MP/FiveM по нашему гайду.
            </p>
          </div>

          {/* Step 6 */}
          <div className="glass-panel p-6 rounded-2xl border border-white/10 relative">
            <span className="text-2xl font-black font-mono text-brand mb-2 block">06</span>
            <h4 className="text-base font-bold text-white mb-2">Запуск и приём игроков</h4>
            <p className="text-xs text-gray-400 leading-relaxed">
              Выполните команду старта — сервер проходит онлайн-верификацию и готов к приёму игроков на порту 7788 UDP.
            </p>
          </div>
        </div>
      </section>

      {/* Pricing Section with Periods (like icsnotify.ru: Месяц / 6 месяцев / Год) */}
      <section id="pricing" className="py-20 bg-surface-300/40 border-y border-white/5 px-4 sm:px-6 lg:px-8">
        <div className="max-w-7xl mx-auto">
          <div className="text-center max-w-3xl mx-auto mb-12">
            <h2 className="text-xs font-bold text-brand uppercase tracking-widest mb-2 font-mono">
              Тарифные планы
            </h2>
            <p className="text-3xl sm:text-4xl font-black text-white">
              Прозрачные условия без скрытых платежей
            </p>

            {/* Period Switcher (icsnotify style) */}
            <div className="mt-8 inline-flex p-1.5 rounded-2xl bg-surface-300 border border-white/10">
              <button
                onClick={() => setBillingPeriod('month')}
                className={`px-4 py-2 rounded-xl text-xs font-bold transition-all ${
                  billingPeriod === 'month' ? 'bg-brand text-white shadow-neon-pink' : 'text-gray-400 hover:text-white'
                }`}
              >
                1 месяц
              </button>
              <button
                onClick={() => setBillingPeriod('halfYear')}
                className={`px-4 py-2 rounded-xl text-xs font-bold transition-all ${
                  billingPeriod === 'halfYear' ? 'bg-brand text-white shadow-neon-pink' : 'text-gray-400 hover:text-white'
                }`}
              >
                6 месяцев (-15%)
              </button>
              <button
                onClick={() => setBillingPeriod('year')}
                className={`px-4 py-2 rounded-xl text-xs font-bold transition-all ${
                  billingPeriod === 'year' ? 'bg-brand text-white shadow-neon-pink' : 'text-gray-400 hover:text-white'
                }`}
              >
                1 год (-33%)
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
            {/* Trial Plan (Indie) */}
            <div className="glass-panel p-8 rounded-3xl border border-white/10 flex flex-col justify-between">
              <div>
                <div className="text-xs font-bold text-gray-400 uppercase tracking-widest mb-2 font-mono">
                  Trial / Разработка
                </div>
                <h3 className="text-2xl font-bold text-white mb-2">Инди</h3>
                <p className="text-gray-400 text-sm mb-6">
                  Для создания и тестирования нового сервера небольшой командой разработчиков.
                </p>
                <div className="text-4xl font-black text-white font-mono mb-6">
                  Бесплатно <span className="text-sm font-normal text-gray-400">· 30 дней</span>
                </div>

                <ul className="space-y-3 text-sm text-gray-300 mb-8">
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-emerald-400" />
                    <span>До 128 игроков онлайн</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-emerald-400" />
                    <span>Автономные бинарники движка</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-emerald-400" />
                    <span>C# Gamemode SDK</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-emerald-400" />
                    <span>Привязка 1 IP-адреса сервера</span>
                  </li>
                </ul>
              </div>

              <Link
                href="/auth/register"
                className="w-full py-3.5 rounded-xl border border-white/20 text-white font-semibold text-sm hover:bg-white/5 text-center transition-colors"
              >
                Начать бесплатно
              </Link>
            </div>

            {/* Pro Plan (RP Проект - Featured) */}
            <div className="glass-panel p-8 rounded-3xl border-2 border-brand relative flex flex-col justify-between shadow-neon-pink">
              <div className="absolute -top-3 left-1/2 -translate-x-1/2 px-3.5 py-1 rounded-full bg-brand text-white text-[11px] font-bold uppercase tracking-wider">
                Популярный выбор
              </div>

              <div>
                <div className="text-xs font-bold text-brand uppercase tracking-widest mb-2 font-mono">
                  Pro / Готовый Проект
                </div>
                <h3 className="text-2xl font-bold text-white mb-2">RP Проект</h3>
                <p className="text-gray-400 text-sm mb-6">
                  Для запуска полноценного проекта с готовыми игровыми системами.
                </p>
                <div className="text-4xl font-black text-white font-mono mb-6">
                  {prices.pro} <span className="text-sm font-normal text-gray-400">{prices.proPeriod}</span>
                </div>

                <ul className="space-y-3 text-sm text-gray-300 mb-8">
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-brand" />
                    <span>До 512 игроков онлайн</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-brand" />
                    <span>C# Гейммод Держава Онлайн</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-brand" />
                    <span>3D Voice WebRTC + Синхронизация NPC</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-brand" />
                    <span>Готовая MariaDB схема + 8-ранговая админка</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-brand" />
                    <span>FastDL CDN конфигурация</span>
                  </li>
                </ul>
              </div>

              <Link
                href="/auth/register"
                className="w-full py-3.5 rounded-xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white font-bold text-sm text-center shadow-neon-pink transition-all"
              >
                Подключить RP Проект
              </Link>
            </div>

            {/* Max / Enterprise Plan */}
            <div className="glass-panel p-8 rounded-3xl border border-white/10 flex flex-col justify-between">
              <div>
                <div className="text-xs font-bold text-cyan-neon uppercase tracking-widest mb-2 font-mono">
                  Enterprise / Франшиза
                </div>
                <h3 className="text-2xl font-bold text-white mb-2">Enterprise</h3>
                <p className="text-gray-400 text-sm mb-6">
                  Для крупных проектов: брендированный лаунчер, миграция «под ключ».
                </p>
                <div className="text-4xl font-black text-white font-mono mb-6">
                  {prices.enterprise} <span className="text-sm font-normal text-gray-400">{prices.enterprisePeriod}</span>
                </div>

                <ul className="space-y-3 text-sm text-gray-300 mb-8">
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-cyan-neon" />
                    <span>1500+ игроков (максимальный лимит)</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-cyan-neon" />
                    <span>Свой брендированный лаунчер (лого, цвета, сборщик)</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-cyan-neon" />
                    <span>Услуга переноса базы с RAGE:MP / FiveM</span>
                  </li>
                  <li className="flex items-center gap-2">
                    <Check className="w-4 h-4 text-cyan-neon" />
                    <span>Выделенная поддержка & SLA 99.9%</span>
                  </li>
                </ul>
              </div>

              <Link
                href="/auth/register"
                className="w-full py-3.5 rounded-xl border border-cyan-500/40 text-cyan-300 hover:bg-cyan-500/10 font-semibold text-sm text-center transition-colors"
              >
                Заказать Enterprise
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* CTA Section (like icsnotify.ru) */}
      <section id="contacts" className="py-20 px-4 sm:px-6 lg:px-8 max-w-5xl mx-auto text-center">
        <div className="glass-panel p-10 sm:p-14 rounded-3xl border border-brand/40 shadow-neon-pink relative overflow-hidden">
          <div className="absolute top-0 right-0 w-64 h-64 bg-brand/15 rounded-full blur-3xl pointer-events-none"></div>

          <h2 className="text-3xl sm:text-4xl font-black text-white mb-4">
            Готовы запустить независимый RP-сервер?
          </h2>
          <p className="text-gray-300 text-base max-w-2xl mx-auto mb-8 leading-relaxed">
            Напишите нам — расскажем, как FloV:MP поможет вашему проекту оставаться стабильным и автономным, а также поможем перенести наработки с других мультиплееров.
          </p>

          <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <a
              href="https://t.me/flovmp_dev"
              target="_blank"
              rel="noopener noreferrer"
              className="w-full sm:w-auto px-8 py-4 rounded-xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white font-bold text-sm shadow-neon-pink flex items-center justify-center gap-2 transition-all"
            >
              <Send className="w-4 h-4" />
              <span>Написать в Telegram</span>
            </a>

            <Link
              href="/auth/register"
              className="w-full sm:w-auto px-8 py-4 rounded-xl glass-panel border border-white/10 hover:border-white/20 text-white font-bold text-sm flex items-center justify-center gap-2 transition-all"
            >
              <span>Создать аккаунт на портале</span>
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}
