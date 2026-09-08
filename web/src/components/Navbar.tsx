'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ArrowRight, LayoutDashboard, LogOut, Menu, ShieldAlert, X } from 'lucide-react';
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

  return (
    <header className="sticky top-0 z-[60] px-3 pt-3 sm:px-4">
      <nav
        className={`mx-auto flex h-14 max-w-6xl items-center gap-3 rounded-2xl border px-3 backdrop-blur-xl transition-all duration-300 sm:px-4 ${
          scrolled
            ? 'border-white/[0.12] bg-[#131316]/90 shadow-[0_16px_50px_-20px_rgba(0,0,0,0.7)]'
            : 'border-white/[0.08] bg-[#131316]/70 shadow-[0_10px_40px_-24px_rgba(0,0,0,0.6)]'
        }`}
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
              className={`relative whitespace-nowrap rounded-xl px-3 py-2 text-[13px] font-semibold transition-colors hover:bg-white/[0.06] hover:text-white ${
                isActive(l.href) ? 'text-white' : 'text-white/55'
              }`}
            >
              {l.label}
              {isActive(l.href) && (
                <span className="absolute inset-x-3 -bottom-0.5 h-0.5 rounded-full bg-brand" />
              )}
            </Link>
          ))}
        </div>

        <div className="ml-auto hidden items-center gap-2 md:flex">
          <LangSwitch />
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
                className="rounded-xl p-2 text-white/45 transition hover:bg-white/5 hover:text-white"
                title={t.common.logout}
              >
                <LogOut className="h-4 w-4" />
              </button>
            </>
          ) : (
            <>
              <Link href="/auth/login" className="rounded-xl px-3 py-2 text-[13px] font-semibold text-white/60 transition-colors hover:text-white">
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
          className="ml-auto rounded-xl border border-white/10 bg-white/[0.04] p-2 text-white/80 lg:hidden"
          aria-label="Menu"
        >
          {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </nav>

      {/* Mobile sheet */}
      <div
        className={`mx-auto mt-2 max-w-6xl overflow-hidden rounded-2xl border border-white/[0.1] bg-[#131316]/95 backdrop-blur-xl transition-[max-height,opacity] duration-300 lg:hidden ${
          open ? 'max-h-[640px] opacity-100' : 'max-h-0 border-transparent opacity-0'
        }`}
      >
        <div className="space-y-1 p-4">
          <div className="mb-2">
            <LangSwitch />
          </div>
          {LINKS.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              className={`block rounded-xl px-3 py-3 text-[15px] font-semibold transition-colors hover:bg-white/5 hover:text-white ${
                isActive(l.href) ? 'text-brand' : 'text-white/70'
              }`}
            >
              {l.label}
            </Link>
          ))}
          <div className="mt-3 flex flex-col gap-2 pt-3">
            <hr className="rule-soft mb-1" />
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
