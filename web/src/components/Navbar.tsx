'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { LayoutDashboard, LogOut, ShieldAlert } from 'lucide-react';
import { useT } from '@/lib/i18n';
import ThemeMenu, { ThemeControls } from './ThemeMenu';
import LangSwitch from './LangSwitch';

interface SessionUser {
  username: string;
  email: string;
  role?: string;
}

export default function Navbar() {
  const t = useT();
  const pathname = usePathname();
  const [user, setUser] = useState<SessionUser | null>(null);
  const [open, setOpen] = useState(false);

  const LINKS = [
    { href: '/features', label: t.nav.features },
    { href: '/pricing', label: t.nav.pricing },
    { href: '/projects', label: t.nav.projects },
    { href: '/docs', label: t.nav.docs },
    { href: '/contact', label: t.nav.contact },
  ];

  useEffect(() => {
    fetch('/api/auth/me')
      .then((r) => (r.ok ? r.json() : null))
      .then((d) => d?.authenticated && setUser(d.user))
      .catch(() => {});
  }, []);

  useEffect(() => setOpen(false), [pathname]);
  useEffect(() => {
    document.body.style.overflow = open ? 'hidden' : '';
    return () => {
      document.body.style.overflow = '';
    };
  }, [open]);

  const logout = async () => {
    await fetch('/api/auth/logout', { method: 'POST' });
    setUser(null);
    window.location.href = '/';
  };

  const isActive = (href: string) => pathname === href || pathname.startsWith(href + '/');

  // Личный кабинет / вход — всегда в обводке.
  const accountCls =
    'flex h-9 items-center gap-2 rounded-xl border border-white/[15%] bg-white/[0.05] px-3.5 text-xs font-semibold text-white transition-colors hover:border-white/[25%] hover:bg-white/[0.08]';

  return (
    <header className="site-header relative z-[60] w-full px-3 sm:px-6 lg:px-8">
      <nav
        className="site-nav mx-auto flex min-h-16 w-full max-w-[1440px] items-center gap-4 border-b px-0 py-3 transition-colors duration-300"
      >
        <Link href="/" className="site-nav__brand group flex shrink-0 items-center gap-2.5" aria-label="FloV:MP">
          <span className="block h-8 w-8 shrink-0 transition-transform duration-300 group-hover:scale-105">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/branding/logo.png" alt="" width="32" height="32" className="h-full w-full object-contain" />
          </span>
          <span translate="no" className="text-[16px] font-extrabold tracking-tight text-white">
            FloV<span className="text-brand">:MP</span>
          </span>
        </Link>

        <div className="site-nav__links ml-3 hidden items-center gap-1 lg:flex">
          {LINKS.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              prefetch
              className={`nav-link relative whitespace-nowrap px-3 py-2 text-[13px] font-semibold ${
                isActive(l.href) ? 'is-active text-white' : 'text-white/[55%]'
              }`}
            >
              {l.label}
            </Link>
          ))}
        </div>

        <div className="site-nav__actions ml-auto hidden items-center gap-2 md:flex">
          <LangSwitch />
          {user ? (
            <>
              {user.role === 'admin' && (
                <Link href="/admin" className={accountCls}>
                  <ShieldAlert aria-hidden="true" className="h-3.5 w-3.5 text-brand" /> {t.common.adminPanel}
                </Link>
              )}
              <Link href="/dashboard" prefetch className={accountCls}>
                <LayoutDashboard aria-hidden="true" className="h-3.5 w-3.5 text-brand" />
                {user.username}
              </Link>
              <button
                onClick={logout}
                className="rounded-xl border border-white/[10%] p-2 text-white/[45%] transition hover:bg-white/[5%] hover:text-white"
                title={t.common.logout}
                aria-label={t.common.logout}
              >
                <LogOut aria-hidden="true" className="h-4 w-4" />
              </button>
            </>
          ) : (
            <Link href="/auth/login" prefetch className={accountCls}>
              <LayoutDashboard aria-hidden="true" className="h-3.5 w-3.5 text-brand" />
              {t.common.login}
            </Link>
          )}
          <ThemeMenu showLanguage={false} />
        </div>

        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          className="mobile-menu-toggle ml-auto grid h-9 w-9 place-items-center rounded-xl border border-white/[10%] bg-white/[0.04] text-white/[80%] md:hidden"
          aria-label={t.common.menu}
          aria-expanded={open}
          aria-controls="mobile-navigation"
        >
          <span className="mobile-menu-toggle__glyph" aria-hidden="true"><i /><i /><i /></span>
        </button>
      </nav>

      {/* Mobile sheet */}
      <div
        id="mobile-navigation"
        className={`mx-auto mt-2 max-w-6xl overflow-hidden rounded-2xl border border-white/[0.1] backdrop-blur-md transition-[max-height,opacity] duration-300 md:hidden ${
          open ? 'max-h-[720px] opacity-100' : 'max-h-0 border-transparent opacity-0'
        }`}
        style={{ backgroundColor: 'color-mix(in srgb, var(--panel) 96%, transparent)' }}
      >
        <div className="space-y-1 p-4">
          {LINKS.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              prefetch
              className={`nav-link block px-3 py-3 text-[15px] font-semibold ${
                isActive(l.href) ? 'text-brand' : 'text-white/[70%]'
              }`}
            >
              {l.label}
            </Link>
          ))}

          <hr className="rule-soft my-3" />
          {user ? (
            <div className="flex flex-col gap-2">
              {user.role === 'admin' && (
                <Link href="/admin" className={accountCls + ' h-11 justify-center text-sm'}>
                  <ShieldAlert aria-hidden="true" className="h-4 w-4 text-brand" /> {t.common.adminPanel}
                </Link>
              )}
              <Link href="/dashboard" prefetch className={accountCls + ' h-11 justify-center text-sm'}>
                <LayoutDashboard aria-hidden="true" className="h-4 w-4 text-brand" />
                {t.common.openDashboard} · {user.username}
              </Link>
              <button onClick={logout} className="py-2 text-center text-sm font-medium text-white/[50%]">
                {t.common.logout}
              </button>
            </div>
          ) : (
            <Link href="/auth/login" prefetch className={accountCls + ' h-11 justify-center text-sm'}>
              <LayoutDashboard aria-hidden="true" className="h-4 w-4 text-brand" />
              {t.common.login}
            </Link>
          )}

          <hr className="rule-soft my-3" />
          <div className="px-1 pb-1">
            <ThemeControls ringOffset="var(--panel)" />
          </div>
        </div>
      </div>
    </header>
  );
}
