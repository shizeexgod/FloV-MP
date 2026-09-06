'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ArrowRight, KeyRound, LogOut, Menu, ShieldAlert, X } from 'lucide-react';

interface SessionUser {
  username: string;
  email: string;
  role?: string;
}

const NAV_LINKS = [
  { href: '/#platform', label: 'Платформа' },
  { href: '/#features', label: 'Возможности' },
  { href: '/#architecture', label: 'Архитектура' },
  { href: '/#process', label: 'Запуск' },
  { href: '/#pricing', label: 'Тарифы' },
  { href: '/docs', label: 'Документация' },
];

export default function Navbar() {
  const pathname = usePathname();
  const [user, setUser] = useState<SessionUser | null>(null);
  const [scrolled, setScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [online, setOnline] = useState<boolean | null>(null);

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
    const onScroll = () => setScrolled(window.scrollY > 12);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  useEffect(() => {
    setMobileOpen(false);
  }, [pathname]);

  useEffect(() => {
    document.body.style.overflow = mobileOpen ? 'hidden' : '';
    return () => {
      document.body.style.overflow = '';
    };
  }, [mobileOpen]);

  const logout = async () => {
    await fetch('/api/auth/logout', { method: 'POST' });
    setUser(null);
    window.location.href = '/';
  };

  return (
    <header
      className={`sticky top-0 z-[60] border-b transition-colors duration-300 ${
        scrolled ? 'border-white/[0.08] bg-ink-950/90 backdrop-blur-xl' : 'border-white/[0.05] bg-ink-950/70 backdrop-blur-md'
      }`}
    >
      <nav className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
        {/* Brand */}
        <Link href="/" className="group flex items-center gap-2.5">
          <span className="relative block h-9 w-9 overflow-hidden rounded-lg border border-white/10 bg-ink-800">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src="/branding/logo.jpg" alt="FloV:MP" className="h-full w-full object-cover" />
          </span>
          <span className="flex items-center gap-2">
            <span className="text-[17px] font-bold tracking-tight text-white">
              FloV<span className="text-brand">:MP</span>
            </span>
            <span className="hidden font-mono text-[10px] font-medium uppercase tracking-wider text-slate-500 sm:inline">
              SaaS Engine
            </span>
          </span>
        </Link>

        {/* Desktop links */}
        <div className="hidden items-center gap-1 lg:flex">
          {NAV_LINKS.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              className={`rounded-lg px-3 py-2 text-[13px] font-medium transition-colors hover:bg-white/5 hover:text-white ${
                l.href === '/docs' && pathname === '/docs' ? 'text-brand' : 'text-slate-300'
              }`}
            >
              {l.label}
            </Link>
          ))}
        </div>

        {/* Right actions */}
        <div className="hidden items-center gap-2.5 md:flex">
          <span className="flex items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-1.5 font-mono text-[10px] font-medium uppercase tracking-wider text-slate-400">
            <span className={online === false ? 'text-err' : 'text-ok'} style={{ display: 'inline-flex' }}>
              <span className="status-dot" />
            </span>
            {online === false ? 'Node offline' : 'Node online'}
          </span>

          {user ? (
            <>
              {user.role === 'admin' && (
                <Link
                  href="/admin"
                  className="btn btn-ghost h-9 gap-1.5 px-3 text-xs font-semibold text-slate-300"
                >
                  <ShieldAlert className="h-3.5 w-3.5" />
                  Админ-панель
                </Link>
              )}
              <Link href="/dashboard" className="btn btn-ghost h-9 gap-2 px-3.5 text-xs font-semibold">
                <KeyRound className="h-3.5 w-3.5 text-brand" />
                {user.username}
              </Link>
              <button
                onClick={logout}
                className="rounded-lg p-2 text-slate-500 transition hover:bg-white/5 hover:text-white"
                title="Выйти"
              >
                <LogOut className="h-4 w-4" />
              </button>
            </>
          ) : (
            <>
              <Link
                href="/auth/login"
                className="rounded-lg px-3 py-2 text-[13px] font-medium text-slate-300 transition-colors hover:text-white"
              >
                Войти
              </Link>
              <Link href="/auth/register" className="btn btn-primary h-9 px-4 text-xs">
                Подключить проект
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </>
          )}
        </div>

        {/* Mobile toggle */}
        <button
          onClick={() => setMobileOpen((v) => !v)}
          className="rounded-lg border border-white/10 bg-white/[0.03] p-2 text-slate-200 lg:hidden"
          aria-label="Меню"
        >
          {mobileOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </nav>

      {/* Mobile sheet */}
      <div
        className={`overflow-hidden border-t border-white/[0.08] bg-ink-950/95 backdrop-blur-xl transition-[max-height,opacity] duration-300 lg:hidden ${
          mobileOpen ? 'max-h-[560px] opacity-100' : 'max-h-0 opacity-0'
        }`}
      >
        <div className="space-y-1 px-4 py-5">
          {NAV_LINKS.map((l, i) => (
            <Link
              key={l.href}
              href={l.href}
              style={{ animationDelay: `${i * 35}ms` }}
              className="animate-slide-down block rounded-xl px-3 py-3 text-[15px] font-medium text-slate-200 transition-colors hover:bg-white/5 hover:text-white"
            >
              {l.label}
            </Link>
          ))}
          <div className="mt-4 flex flex-col gap-2 border-t border-white/[0.08] pt-4">
            {user ? (
              <>
                {user.role === 'admin' && (
                  <Link href="/admin" className="btn btn-ghost h-11 text-sm font-semibold">
                    <ShieldAlert className="h-4 w-4" /> Админ-панель
                  </Link>
                )}
                <Link href="/dashboard" className="btn btn-primary h-11 text-sm">
                  Личный кабинет ({user.username})
                </Link>
                <button onClick={logout} className="py-2 text-center text-sm font-semibold text-slate-400">
                  Выйти из системы
                </button>
              </>
            ) : (
              <>
                <Link
                  href="/auth/login"
                  className="btn btn-ghost h-11 text-sm font-semibold"
                >
                  Войти
                </Link>
                <Link href="/auth/register" className="btn btn-primary h-11 text-sm">
                  Подключить проект <ArrowRight className="h-4 w-4" />
                </Link>
              </>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
