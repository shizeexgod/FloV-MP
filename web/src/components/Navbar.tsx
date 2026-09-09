'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { LayoutDashboard, LogOut, Menu, ShieldAlert, X } from 'lucide-react';
import { useT } from '@/lib/i18n';
import ThemeMenu, { ThemeControls } from './ThemeMenu';

interface SessionUser {
  username: string;
  email: string;
  role?: string;
}

export default function Navbar() {
  const t = useT();
  const pathname = usePathname();
  const [user, setUser] = useState<SessionUser | null>(null);
  const [scrolled, setScrolled] = useState(false);
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

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
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
    'flex h-9 items-center gap-2 rounded-xl border border-white/15 bg-white/[0.05] px-3.5 text-xs font-semibold text-white transition-colors hover:border-white/25 hover:bg-white/[0.08]';

  return (
    <header className="sticky top-0 z-[60] px-3 pt-3 sm:px-4">
      <nav
        className={`mx-auto flex h-14 max-w-6xl items-center gap-3 rounded-2xl border px-3 backdrop-blur-xl transition-all duration-300 sm:px-4 ${
          scrolled
            ? 'border-white/[0.12] bg-[#131316]/90 shadow-[0_16px_50px_-20px_rgba(0,0,0,0.7)]'
            : 'border-white/[0.08] bg-[#131316]/70 shadow-[0_10px_40px_-24px_rgba(0,0,0,0.6)]'
        }`}
        style={{ backgroundColor: scrolled ? 'color-mix(in srgb, var(--panel) 90%, transparent)' : 'color-mix(in srgb, var(--panel) 72%, transparent)' }}
      >
        <Link href="/" className="group flex items-center gap-2.5">
          <span className="block h-8 w-8 overflow-hidden rounded-[10px] border border-white/10 bg-ink-800 transition-transform duration-300 group-hover:scale-105">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/branding/logo.jpg" alt="FloV:MP" className="h-full w-full object-cover" />
          </span>
          <span className="text-[16px] font-extrabold tracking-tight text-white">
            FloV<span className="text-brand">:MP</span>
          </span>
        </Link>

        <div className="ml-3 hidden items-center gap-0.5 lg:flex">
          {LINKS.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              prefetch
              className={`relative whitespace-nowrap rounded-xl px-3 py-2 text-[13px] font-semibold transition-colors hover:bg-white/[0.06] hover:text-white ${
                isActive(l.href) ? 'text-white' : 'text-white/55'
              }`}
            >
              {l.label}
              {isActive(l.href) && <span className="absolute inset-x-3 -bottom-0.5 h-0.5 rounded-full bg-brand" />}
            </Link>
          ))}
        </div>

        <div className="ml-auto hidden items-center gap-2 md:flex">
          {user ? (
            <>
              {user.role === 'admin' && (
                <Link href="/admin" className={accountCls}>
                  <ShieldAlert className="h-3.5 w-3.5 text-brand" /> Admin
                </Link>
              )}
              <Link href="/dashboard" prefetch className={accountCls}>
                <LayoutDashboard className="h-3.5 w-3.5 text-brand" />
                {user.username}
              </Link>
              <button
                onClick={logout}
                className="rounded-xl border border-white/10 p-2 text-white/45 transition hover:bg-white/5 hover:text-white"
                title={t.common.logout}
              >
                <LogOut className="h-4 w-4" />
              </button>
            </>
          ) : (
            <Link href="/auth/login" prefetch className={accountCls}>
              <LayoutDashboard className="h-3.5 w-3.5 text-brand" />
              {t.common.login}
            </Link>
          )}
          <ThemeMenu />
        </div>

        <button
          onClick={() => setOpen((v) => !v)}
          className="ml-auto rounded-xl border border-white/10 bg-white/[0.04] p-2 text-white/80 md:hidden"
          aria-label={t.common.menu}
        >
          {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </nav>

      {/* Mobile sheet */}
      <div
        className={`mx-auto mt-2 max-w-6xl overflow-hidden rounded-2xl border border-white/[0.1] backdrop-blur-xl transition-[max-height,opacity] duration-300 md:hidden ${
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
              className={`block rounded-xl px-3 py-3 text-[15px] font-semibold transition-colors hover:bg-white/5 hover:text-white ${
                isActive(l.href) ? 'text-brand' : 'text-white/70'
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
                  <ShieldAlert className="h-4 w-4 text-brand" /> Admin
                </Link>
              )}
              <Link href="/dashboard" prefetch className={accountCls + ' h-11 justify-center text-sm'}>
                <LayoutDashboard className="h-4 w-4 text-brand" />
                {t.common.openDashboard} · {user.username}
              </Link>
              <button onClick={logout} className="py-2 text-center text-sm font-medium text-white/50">
                {t.common.logout}
              </button>
            </div>
          ) : (
            <Link href="/auth/login" prefetch className={accountCls + ' h-11 justify-center text-sm'}>
              <LayoutDashboard className="h-4 w-4 text-brand" />
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
