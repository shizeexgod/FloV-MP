'use client';

import React from 'react';
import Link from 'next/link';
import { Send } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { LEGAL, lv } from '@/lib/legal';

export default function Footer() {
  const t = useT();
  const L = t.footer.links;

  const productLinks = [
    { label: L.features, href: '/features' },
    { label: L.pricing, href: '/pricing' },
    { label: L.docs, href: '/docs' },
    { label: L.projects, href: '/projects' },
  ];

  const docLinks = [
    { label: L.offer, href: '/legal/offer' },
    { label: L.requisites, href: '/legal/requisites' },
    { label: L.terms, href: '/legal/terms' },
    { label: L.privacy, href: '/legal/privacy' },
  ];

  return (
    <footer className="mt-8 px-3 pb-4 sm:px-4">
      <div className="mx-auto max-w-6xl rounded-2xl border border-white/[0.08] bg-[#131316]/60 px-6 py-12 sm:px-8">
        <div className="grid grid-cols-1 gap-x-8 gap-y-10 md:grid-cols-[1.6fr_1fr_1fr_1fr]">
          {/* Brand + working hours */}
          <div>
            <div className="flex items-center gap-2.5">
              <span className="block h-9 w-9 overflow-hidden rounded-[10px] border border-white/10 bg-ink-800">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src="/branding/logo.png" alt="FloV:MP" className="h-full w-full object-cover" />
              </span>
              <span className="text-[16px] font-extrabold text-white">
                FloV<span className="text-brand">:MP</span>
              </span>
            </div>
            <p className="mt-4 max-w-xs text-[13px] leading-relaxed text-white/50">{t.footer.tagline}</p>

            <h4 className="mt-8 font-mono text-[11px] font-semibold uppercase tracking-widest text-white/45">
              {t.footer.colHours}
            </h4>
            <div className="mt-3 space-y-1.5 text-[13px] text-white/50">
              <p>{t.footer.hoursAuto}</p>
              <p>
                {t.footer.hoursSupportPrefix}
                {lv(LEGAL.supportHours)}
              </p>
            </div>
          </div>

          {/* Контакты */}
          <div>
            <h4 className="font-mono text-[11px] font-semibold uppercase tracking-widest text-white/45">
              {t.footer.colContacts}
            </h4>
            <ul className="mt-4 space-y-2.5 text-[13px]">
              {LEGAL.telegramUrl && (
                <li>
                  <a
                    href={LEGAL.telegramUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-white/50 transition-colors hover:text-brand"
                  >
                    {lv(LEGAL.telegramHandle)}
                  </a>
                </li>
              )}
              {LEGAL.phone && (
                <li>
                  <a href={`tel:${LEGAL.phone}`} className="text-white/50 transition-colors hover:text-brand">
                    {lv(LEGAL.phoneDisplay)}
                  </a>
                </li>
              )}
              {LEGAL.email && (
                <li>
                  <a href={`mailto:${LEGAL.email}`} className="text-white/50 transition-colors hover:text-brand">
                    {LEGAL.email}
                  </a>
                </li>
              )}
              {!LEGAL.telegramUrl && !LEGAL.phone && !LEGAL.email && (
                <li className="text-white/30">—</li>
              )}
            </ul>
          </div>

          {/* Документы */}
          <div>
            <h4 className="font-mono text-[11px] font-semibold uppercase tracking-widest text-white/45">
              {t.footer.colDocs}
            </h4>
            <ul className="mt-4 space-y-2.5">
              {docLinks.map((l) => (
                <li key={l.href}>
                  <Link href={l.href} className="text-[13px] text-white/50 transition-colors hover:text-brand">
                    {l.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          {/* Продукт */}
          <div>
            <h4 className="font-mono text-[11px] font-semibold uppercase tracking-widest text-white/45">
              {t.footer.colProduct}
            </h4>
            <ul className="mt-4 space-y-2.5">
              {productLinks.map((l) => (
                <li key={l.href}>
                  <Link href={l.href} className="text-[13px] text-white/50 transition-colors hover:text-brand">
                    {l.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        </div>

        <hr className="rule-soft mt-12" />
        <div className="mt-8 flex flex-col gap-3 text-[12px] text-white/40 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between">
          <span>© 2026 FloV:MP. {t.common.allRightsReserved}</span>
          <span>{lv(LEGAL.ownerName)}</span>
          <span>ИНН {lv(LEGAL.inn)}</span>
          <span className="hidden lg:inline">{t.footer.descriptor}</span>
          {LEGAL.telegramUrl && (
            <a
              href={LEGAL.telegramUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center gap-1.5 font-medium text-white/50 transition-colors hover:text-brand"
            >
              <Send className="h-3.5 w-3.5" />
              Telegram
            </a>
          )}
        </div>
      </div>
    </footer>
  );
}
