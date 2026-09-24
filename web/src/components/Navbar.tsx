'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { LogIn, Menu, Monitor, ShieldAlert, X } from 'lucide-react';
import { useT } from '@/lib/i18n';

interface SessionUser {
  username: string;
  email: string;
  role?: string;
}

/** Public navigation stays intentionally light: one account action and no controls competing with the product. */
export default function Navbar() {
  const t = useT();
  const pathname = usePathname();
  const [user, setUser] = useState<SessionUser | null>(null);
  const [open, setOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  const links = [
    { href: '/features', label: t.nav.features },
    { href: '/pricing', label: t.nav.pricing },
    { href: '/projects', label: t.nav.projects },
    { href: '/docs', label: t.nav.docs },
    { href: '/contact', label: t.nav.contact },
  ];

  useEffect(() => {
    fetch('/api/auth/me').then((response) => (response.ok ? response.json() : null)).then((data) => data?.authenticated && setUser(data.user)).catch(() => {});
  }, []);
  useEffect(() => setOpen(false), [pathname]);
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 12);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);
  useEffect(() => {
    document.body.style.overflow = open ? 'hidden' : '';
    return () => { document.body.style.overflow = ''; };
  }, [open]);

  const active = (href: string) => pathname === href || pathname.startsWith(`${href}/`);
  const accountClass = 'account-link inline-flex h-9 items-center gap-2 rounded-[10px] border border-white/[0.14] bg-white/[0.015] px-3.5 text-[12px] font-bold text-white transition-[border-color,background-color,transform,box-shadow] duration-200 hover:-translate-y-px hover:border-white/[0.26] hover:bg-white/[0.055]';

  return (
    <header className={`site-header sticky top-0 z-[60] w-full ${scrolled ? 'is-scrolled' : ''}`}>
      <nav className="mx-auto grid h-16 w-full max-w-[1360px] grid-cols-[1fr_auto_1fr] items-center px-5 sm:px-8" aria-label={t.common.menu}>
        <Link href="/" className="group flex w-max items-center" aria-label="FloV:MP">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/branding/logo-codex.png" alt="" width="40" height="40" className="header-logo h-10 w-10 object-contain transition-transform duration-200 group-hover:scale-[1.035]" />
        </Link>

        <div className="hidden items-center gap-1 lg:flex">
          {links.map((link) => <Link key={link.href} href={link.href} prefetch className={`nav-link px-3 py-2 text-[12.5px] font-semibold ${active(link.href) ? 'is-active text-white' : 'text-white/[0.52]'}`}>{link.label}</Link>)}
        </div>

        <div className="hidden items-center justify-self-end gap-2 lg:flex">
          {user?.role === 'admin' ? <Link href="/admin" className={accountClass}><ShieldAlert aria-hidden="true" className="h-3.5 w-3.5 text-brand" />{t.common.adminPanel}</Link> : null}
          <Link href={user ? '/dashboard' : '/auth/login'} prefetch className={accountClass}>{user ? <Monitor aria-hidden="true" className="h-3.5 w-3.5 text-brand" /> : <LogIn aria-hidden="true" className="h-3.5 w-3.5 text-brand" />}{user ? t.common.openDashboard : t.common.login}</Link>
        </div>

        <button type="button" onClick={() => setOpen((value) => !value)} className="col-start-3 justify-self-end grid h-10 w-10 place-items-center rounded-[10px] border border-white/[0.1] text-white/[0.78] transition-[border-color,background-color,color] hover:border-white/[0.2] hover:bg-white/[0.05] hover:text-white lg:hidden" aria-label={t.common.menu} aria-expanded={open} aria-controls="mobile-navigation">
          {open ? <X aria-hidden="true" className="h-[18px] w-[18px]" /> : <Menu aria-hidden="true" className="h-[18px] w-[18px]" />}
        </button>
      </nav>

      <div id="mobile-navigation" {...(open ? {} : ({ inert: '' } as Record<string, string>))} aria-hidden={!open} className={`mobile-navigation overflow-hidden transition-[max-height,opacity] duration-300 lg:hidden ${open ? 'max-h-[480px] opacity-100' : 'pointer-events-none max-h-0 opacity-0'}`}>
        <div className="mx-auto max-w-[1360px] px-5 py-4 sm:px-8">
          <div className="grid gap-1">
            {links.map((link) => <Link key={link.href} href={link.href} prefetch className={`rounded-lg px-3 py-3 text-[14px] font-semibold transition-colors ${active(link.href) ? 'bg-white/[0.06] text-white' : 'text-white/[0.58] hover:bg-white/[0.04] hover:text-white'}`}>{link.label}</Link>)}
          </div>
          <hr className="rule-soft my-4" />
          <div className="flex items-center justify-between gap-3">
            <Link href={user ? '/dashboard' : '/auth/login'} prefetch className={accountClass}>{user ? <Monitor aria-hidden="true" className="h-3.5 w-3.5 text-brand" /> : <LogIn aria-hidden="true" className="h-3.5 w-3.5 text-brand" />}{user ? t.common.openDashboard : t.common.login}</Link>
          </div>
        </div>
      </div>
    </header>
  );
}
