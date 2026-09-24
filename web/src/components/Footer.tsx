'use client';

import React from 'react';
import Link from 'next/link';
import { ArrowUpRight } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { LEGAL } from '@/lib/legal';
import LangMenu from './LangMenu';

export default function Footer() {
  const t = useT();
  const links = t.footer.links;
  const product = [
    { label: links.features, href: '/features' },
    { label: links.pricing, href: '/pricing' },
    { label: links.projects, href: '/projects' },
    { label: links.docs, href: '/docs' },
  ];
  const legal = [
    { label: links.offer, href: '/legal/offer' },
    { label: links.requisites, href: '/legal/requisites' },
    { label: links.terms, href: '/legal/terms' },
    { label: links.privacy, href: '/legal/privacy' },
  ];

  return (
    <footer className="site-footer mt-12">
      <div className="mx-auto max-w-[1240px] px-5 py-14 sm:px-8 sm:py-16">
        <div className="grid gap-11 lg:grid-cols-[1.75fr_.7fr_.85fr]">
          <div>
            <Link href="/" className="inline-flex items-center gap-2.5" aria-label="FloV:MP">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src="/branding/logo.png" alt="" width="34" height="34" loading="lazy" className="h-[34px] w-[34px] object-contain" />
              <span translate="no" className="text-[16px] font-extrabold tracking-[-0.04em] text-white">FloV<span className="text-brand">:MP</span></span>
            </Link>
            <p className="mt-5 max-w-sm text-[13px] leading-relaxed text-white/[0.46]">{t.footer.tagline}</p>
            <p className="mt-5 max-w-md text-[11px] leading-relaxed text-white/[0.29]">{t.footer.legalDisclaimer}</p>
            <a href={`mailto:${LEGAL.email}`} className="group mt-5 inline-flex w-max items-center gap-1.5 text-[12px] font-semibold text-white/[0.54] transition-colors hover:text-brand">{LEGAL.email}<ArrowUpRight aria-hidden="true" className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5 group-hover:-translate-y-0.5" /></a>
          </div>

          <nav aria-label={t.footer.colProduct}>
            <h2 className="font-mono text-[10px] font-semibold uppercase tracking-[0.18em] text-white/[0.36]">{t.footer.colProduct}</h2>
            <ul className="mt-4 space-y-2.5">{product.map((link) => <li key={link.href}><Link href={link.href} className="hover-text text-[13px] text-white/[0.55] hover:text-white">{link.label}</Link></li>)}</ul>
          </nav>

          <nav aria-label={t.footer.colDocs}>
            <h2 className="font-mono text-[10px] font-semibold uppercase tracking-[0.18em] text-white/[0.36]">{t.footer.colDocs}</h2>
            <ul className="mt-4 space-y-2.5">{legal.map((link) => <li key={link.href}><Link href={link.href} className="hover-text text-[13px] text-white/[0.55] hover:text-white">{link.label}</Link></li>)}</ul>
          </nav>

        </div>

        <hr className="rule-soft mt-14" />
        <div className="mt-7 flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
          <span className="text-[11px] text-white/[0.34]">© 2026 FloV:MP. {t.common.allRightsReserved}</span>
          <LangMenu />
        </div>
      </div>
    </footer>
  );
}
