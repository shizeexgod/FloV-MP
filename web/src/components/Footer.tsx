import React from 'react';
import Link from 'next/link';
import { Cpu, Database, Radio, Send, ShieldCheck } from 'lucide-react';

const SPECS = [
  { icon: Cpu, label: 'CoreCLR .NET 8' },
  { icon: Database, label: 'MariaDB 10.6+' },
  { icon: Radio, label: 'UDP 7788 / WebRTC' },
  { icon: ShieldCheck, label: 'FastDL CDN HTTP/2' },
];

const COLUMNS = [
  {
    title: 'Платформа',
    links: [
      { label: 'Сетевой протокол UDP', href: '/#architecture' },
      { label: 'C# CoreCLR Gamemode', href: '/#features' },
      { label: 'WebRTC 3D Voice', href: '/#features' },
      { label: 'Синхронизация NPC / трафика', href: '/#features' },
    ],
  },
  {
    title: 'Клиентам',
    links: [
      { label: 'Получить лицензионный ключ', href: '/auth/register' },
      { label: 'Личный кабинет', href: '/dashboard' },
      { label: 'Тарифные планы', href: '/#pricing' },
      { label: 'Документация API', href: '/docs' },
    ],
  },
  {
    title: 'Экосистема',
    links: [
      { label: 'Держава Онлайн (RP)', href: '/#platform' },
      { label: 'Сборщик лаунчера', href: '/dashboard' },
      { label: 'FastDL CDN Mirror', href: '/docs' },
      { label: 'Миграция с RAGE:MP / FiveM', href: '/#faq' },
    ],
  },
];

export default function Footer() {
  return (
    <footer className="relative mt-24 border-t border-white/[0.08] bg-ink-950/60">
      <div className="mx-auto max-w-7xl px-4 py-14 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 gap-10 md:grid-cols-[1.4fr_1fr_1fr_1fr]">
          <div>
            <div className="flex items-center gap-3">
              <span className="block h-10 w-10 overflow-hidden rounded-xl border border-white/15 bg-ink-800">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src="/branding/logo.jpg" alt="FloV:MP" className="h-full w-full object-cover" />
              </span>
              <span className="text-lg font-black text-white">
                FloV<span className="text-brand">:MP</span>
              </span>
            </div>
            <p className="mt-4 max-w-sm text-[13px] leading-relaxed text-slate-400">
              Независимый мультиплеерный движок нового поколения для GTA V. Полная автономия сетевого
              стека, C# .NET 8 ядро, синхронизация NPC и встроенный 3D Voice — без мастер-серверов и
              рисков блокировки.
            </p>
            <div className="mt-5 inline-flex items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 font-mono text-[11px] text-slate-400">
              <span className="status-dot text-ok" />
              Node&nbsp;188.127.229.224:7788
            </div>
          </div>

          {COLUMNS.map((col) => (
            <div key={col.title}>
              <h4 className="font-mono text-[11px] font-bold uppercase tracking-widest text-white">
                {col.title}
              </h4>
              <ul className="mt-4 space-y-2.5">
                {col.links.map((l) => (
                  <li key={l.label}>
                    <Link
                      href={l.href}
                      className="text-[13px] text-slate-400 transition-colors hover:text-brand"
                    >
                      {l.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="mt-12 flex flex-col items-center gap-4 border-t border-white/[0.06] pt-8 sm:flex-row sm:justify-between">
          <p className="text-xs text-slate-500">
            © 2026 FloV:MP Engine. Технологическая SaaS-платформа. Все права защищены.
          </p>
          <div className="flex flex-wrap items-center justify-center gap-x-4 gap-y-2">
            {SPECS.map((s) => (
              <span key={s.label} className="flex items-center gap-1.5 text-[11px] font-medium text-slate-500">
                <s.icon className="h-3.5 w-3.5 text-slate-600" />
                {s.label}
              </span>
            ))}
          </div>
          <a
            href="https://t.me/flovmp_dev"
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-1.5 text-xs font-semibold text-slate-400 transition-colors hover:text-brand"
          >
            <Send className="h-3.5 w-3.5" />
            Telegram-поддержка
          </a>
        </div>
      </div>
    </footer>
  );
}
