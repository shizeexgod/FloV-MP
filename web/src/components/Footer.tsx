'use client';

import React from 'react';
import Link from 'next/link';
import { Cpu, Database, Radio, Send, ShieldCheck } from 'lucide-react';
import { useT } from '@/lib/i18n';

const SPECS = [
  { icon: Cpu, label: 'CoreCLR .NET 8' },
  { icon: Database, label: 'MariaDB 10.6+' },
  { icon: Radio, label: 'UDP 7788 / WebRTC' },
  { icon: ShieldCheck, label: 'FastDL CDN HTTP/2' },
];

export default function Footer() {
  const t = useT();
  const L = t.footer.links;

  const COLUMNS = [
    {
      title: t.footer.colProduct,
      links: [
        { label: L.features, href: '/features' },
        { label: L.pricing, href: '/pricing' },
        { label: L.dashboard, href: '/dashboard' },
        { label: L.launcher, href: '/dashboard' },
      ],
    },
    {
      title: t.footer.colResources,
      links: [
        { label: L.docs, href: '/docs' },
        { label: L.quickstart, href: '/docs' },
        { label: L.api, href: '/docs' },
        { label: L.status, href: '/projects' },
      ],
    },
    {
      title: t.footer.colCompany,
      links: [
        { label: L.projects, href: '/projects' },
        { label: L.roadmap, href: '/roadmap' },
        { label: L.contact, href: '/contact' },
        { label: L.terms, href: '/legal/terms' },
      ],
    },
  ];

  return (
    <footer className="border-t border-white/[0.08] bg-ink-950/60">
      <div className="mx-auto max-w-6xl px-5 py-14 sm:px-6">
        <div className="grid grid-cols-1 gap-10 md:grid-cols-[1.5fr_1fr_1fr_1fr]">
          <div>
            <div className="flex items-center gap-2.5">
              <span className="block h-9 w-9 overflow-hidden rounded-lg border border-white/10 bg-ink-800">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src="/branding/logo.jpg" alt="FloV:MP" className="h-full w-full object-cover" />
              </span>
              <span className="text-[16px] font-semibold text-white">
                FloV<span className="text-brand">:MP</span>
              </span>
            </div>
            <p className="mt-4 max-w-sm text-[13px] leading-relaxed text-white/50">{t.footer.tagline}</p>
            <div className="mt-5 inline-flex items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-3 py-2 font-mono text-[11px] text-white/50">
              <span className="status-dot text-ok" />
              {t.footer.nodeStatus}&nbsp;188.127.229.224:7788
            </div>
          </div>

          {COLUMNS.map((col) => (
            <div key={col.title}>
              <h4 className="font-mono text-[11px] font-semibold uppercase tracking-widest text-white">{col.title}</h4>
              <ul className="mt-4 space-y-2.5">
                {col.links.map((l) => (
                  <li key={l.label}>
                    <Link href={l.href} className="text-[13px] text-white/50 transition-colors hover:text-brand">
                      {l.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="mt-12 flex flex-col items-center gap-4 border-t border-white/[0.06] pt-8 sm:flex-row sm:justify-between">
          <p className="text-xs text-white/40">© 2026 FloV:MP Engine. {t.common.allRightsReserved}</p>
          <div className="flex flex-wrap items-center justify-center gap-x-4 gap-y-2">
            {SPECS.map((s) => (
              <span key={s.label} className="flex items-center gap-1.5 text-[11px] font-medium text-white/40">
                <s.icon className="h-3.5 w-3.5 text-white/30" />
                {s.label}
              </span>
            ))}
          </div>
          <a
            href="https://t.me/flovmp_dev"
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-1.5 text-xs font-medium text-white/50 transition-colors hover:text-brand"
          >
            <Send className="h-3.5 w-3.5" />
            Telegram
          </a>
        </div>
      </div>
    </footer>
  );
}
