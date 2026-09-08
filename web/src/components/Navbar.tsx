'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ArrowRight, KeyRound, LayoutDashboard, LogOut, Menu, ShieldAlert, X } from 'lucide-react';
import { useT } from '@/lib/i18n';
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
  const [scrolled, setScrolled] = useState(false);
  const [open, setOpen] = useState(false);
  const [online, setOnline] = useState<boolean | null>(null);

  const LINKS = [
    { href: '/features', label: t.nav.features },
    { href: '/pricing', label: t.nav.pricing },
    { href: '/projects', label: t.nav.projects },
    { href: '/roadmap', label: t.nav.roadmap },
    { href: '/docs', label: t.nav.docs },
    { href: '/contact', label: t.nav.contact },
  ];

  useEffect(() => {
    fetch('/api/auth/me')
      .then((r) => (r.ok ? r.json() : null))
      .then((d) => d?.authenticated && setUser(d.user))
      .catch(() => {});
    fetch('/api/server-status')
      .then((r) => (r.ok ? r.json() : null))
      .then((d) => setOnline(Boolean(d?.online)))
      .catch(() => setOnline(false));
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

  return (
    <header
      className={`sticky top-0 z-[60] border-b transition-colors duration-300 ${
        scrolled ? 'border-white/[0.08] bg-ink-950/90 backdrop-blur-xl' : 'border-white/[0.05] bg-ink-950/70 backdrop-blur-md'
      }`}
    >
      <nav className="mx-auto flex h-16 max-w-6xl items-center gap-3 px-5 sm:px-6">
        <Link href="/" className="group flex items-center gap-2.5">
          <span className="block h-8 w-8 overflow-hidden rounded-lg border border-white/10 bg-ink-800">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/branding/logo.jpg" alt="FloV:MP" className="h-full w-full object-cover" />
          </span>
          <span className="text-[16px] font-semibold tracking-tight text-white">
            FloV<span className="text-brand">:MP</span>
          </span>
        </Link>

        <div className="ml-4 hidden items-center gap-1 lg:flex">
          {LINKS.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              className={`whitespace-nowrap rounded-lg px-2.5 py-2 text-[13px] font-medium transition-colors hover:bg-white/5 hover:text-white ${
                isActive(l.href) ? 'text-brand' : 'text-white/60'
              }`}
            >
              {l.label}
            </Link>
          ))}
        </div>

        <div className="ml-auto hidden items-center gap-2.5 md:flex">
          <LangSwitch />
          <span
            className="flex items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 font-mono text-[10px] font-medium uppercase tracking-wider text-white/45"
            title={online === false ? t.common.offline : t.common.online}
          >
            <span className={online === false ? 'text-err' : 'text-ok'} style={{ display: 'inline-flex' }}>
              <span className="status-dot" />
            </span>
            {online === false ? 'Node offline' : 'Node online'}
          </span>

          {user ? (
            <>
              {user.role === 'admin' && (
                <Link href="/admin" className="btn btn-ghost h-9 gap-1.5 px-3 text-xs">
                  <ShieldAlert className="h-3.5 w-3.5" /> Admin
                </Link>
              )}
              <Link href="/dashboard" className="btn btn-ghost h-9 gap-2 px-3.5 text-xs">
                <LayoutDashboard className="h-3.5 w-3.5 text-brand" />
                {user.username}
              </Link>
              <button
                onClick={logout}
                className="rounded-lg p-2 text-white/45 transition hover:bg-white/5 hover:text-white"
                title={t.common.logout}
              >
                <LogOut className="h-4 w-4" />
              </button>
            </>
          ) : (
            <>
              <Link href="/auth/login" className="rounded-lg px-3 py-2 text-[13px] font-medium text-white/60 transition-colors hover:text-white">
                {t.common.login}
              </Link>
              <Link href="/pricing" className="btn btn-primary h-9 px-4 text-xs">
                {t.common.getLicense}
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </>
          )}
        </div>

        <button
          onClick={() => setOpen((v) => !v)}
          className="ml-auto rounded-lg border border-white/10 bg-white/[0.03] p-2 text-white/80 lg:hidden"
          aria-label="Menu"
        >
          {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </nav>

      <div
        className={`overflow-hidden border-t border-white/[0.08] bg-ink-950/95 backdrop-blur-xl transition-[max-height,opacity] duration-300 lg:hidden ${
          open ? 'max-h-[640px] opacity-100' : 'max-h-0 opacity-0'
        }`}
      >
        <div className="space-y-1 px-5 py-5">
          <div className="mb-3 flex items-center justify-between">
            <LangSwitch />
            <span className="flex items-center gap-2 font-mono text-[10px] uppercase tracking-wider text-white/45">
              <span className={online === false ? 'text-err' : 'text-ok'} style={{ display: 'inline-flex' }}>
                <span className="status-dot" />
              </span>
              {online === false ? 'Node offline' : 'Node online'}
            </span>
          </div>
          {LINKS.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              className={`block rounded-xl px-3 py-3 text-[15px] font-medium transition-colors hover:bg-white/5 hover:text-white ${
                isActive(l.href) ? 'text-brand' : 'text-white/70'
              }`}
            >
              {l.label}
            </Link>
          ))}
          <div className="mt-4 flex flex-col gap-2 border-t border-white/[0.08] pt-4">
            {user ? (
              <>
                {user.role === 'admin' && (
                  <Link href="/admin" className="btn btn-ghost h-11 text-sm">
                    <ShieldAlert className="h-4 w-4" /> Admin
                  </Link>
                )}
                <Link href="/dashboard" className="btn btn-primary h-11 text-sm">
                  {t.common.openDashboard} · {user.username}
                </Link>
                <button onClick={logout} className="py-2 text-center text-sm font-medium text-white/50">
                  {t.common.logout}
                </button>
              </>
            ) : (
              <>
                <Link href="/auth/login" className="btn btn-ghost h-11 text-sm">
                  {t.common.login}
                </Link>
                <Link href="/pricing" className="btn btn-primary h-11 text-sm">
                  {t.common.getLicense} <ArrowRight className="h-4 w-4" />
                </Link>
              </>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
