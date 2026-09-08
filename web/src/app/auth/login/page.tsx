'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { AlertCircle, ArrowRight, Eye, EyeOff, KeyRound, Mail, ShieldCheck } from 'lucide-react';
import { useT } from '@/lib/i18n';

export default function LoginPage() {
  const t = useT();
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPass, setShowPass] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || t.auth.errGeneric);
      router.push('/dashboard');
      router.refresh();
    } catch (err: any) {
      setError(err.message || t.auth.errGeneric);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-[calc(100vh-64px)] items-center justify-center px-4 py-16">
      <div className="w-full max-w-md">
        <div className="card card-edge rounded-2xl p-8 sm:p-9">
          <div className="text-center">
            <div className="mx-auto grid h-11 w-11 place-items-center rounded-xl border border-brand/40 bg-brand/10 text-brand">
              <KeyRound className="h-5 w-5" />
            </div>
            <h1 className="mt-5 text-xl font-semibold tracking-tight text-white">{t.auth.loginTitle}</h1>
            <p className="mt-1.5 text-[13px] text-white/45">{t.auth.loginSub}</p>
          </div>

          {error && (
            <div className="mt-6 flex items-center gap-3 rounded-xl border border-err/30 bg-err/10 px-4 py-3 text-[13px] text-err">
              <AlertCircle className="h-4 w-4 flex-none" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="mt-7 space-y-4">
            <div>
              <label className="mb-1.5 block font-mono text-[11px] uppercase tracking-wider text-white/40">{t.auth.email}</label>
              <div className="relative">
                <Mail className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-white/30" />
                <input
                  type="email"
                  required
                  autoComplete="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="owner@flovmp.ru"
                  className="field h-11 pl-10 pr-4"
                />
              </div>
            </div>

            <div>
              <label className="mb-1.5 block font-mono text-[11px] uppercase tracking-wider text-white/40">{t.auth.password}</label>
              <div className="relative">
                <KeyRound className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-white/30" />
                <input
                  type={showPass ? 'text' : 'password'}
                  required
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  className="field h-11 pl-10 pr-11"
                />
                <button
                  type="button"
                  onClick={() => setShowPass((v) => !v)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-white/35 transition hover:text-white"
                >
                  {showPass ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>

            <button type="submit" disabled={loading} className="btn btn-primary h-11 w-full text-sm">
              {loading ? t.auth.signInBusy : t.auth.signIn}
              {!loading && <ArrowRight className="h-4 w-4" />}
            </button>
          </form>

          <div className="mt-7 border-t border-white/[0.08] pt-6 text-center text-xs text-white/45">
            {t.auth.noAccount}{' '}
            <Link href="/auth/register" className="font-semibold text-brand hover:underline">
              {t.auth.toRegister}
            </Link>
          </div>
        </div>

        <p className="mt-5 flex items-center justify-center gap-2 text-center font-mono text-[11px] text-white/30">
          <ShieldCheck className="h-3.5 w-3.5 text-ok" />
          JWT · cookie httpOnly
        </p>
      </div>
    </div>
  );
}
