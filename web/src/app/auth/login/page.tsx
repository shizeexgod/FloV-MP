'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { AlertCircle, ArrowRight, Eye, EyeOff, KeyRound, Mail, ShieldCheck } from 'lucide-react';
import { AuroraBlobs, FieldLabel, Spinner } from '@/components/ui';

export default function LoginPage() {
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
      if (!res.ok) throw new Error(data.error || 'Ошибка входа');
      router.push('/dashboard');
      router.refresh();
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="relative flex min-h-[calc(100vh-72px)] items-center justify-center px-4 py-16">
      <AuroraBlobs />
      <div className="relative w-full max-w-md">
        <div className="glass-panel card-edge rounded-3xl p-8 shadow-glass sm:p-10">
          <div className="text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl border border-brand/40 bg-brand/10 text-brand shadow-neon-pink">
              <KeyRound className="h-6 w-6" />
            </div>
            <h1 className="mt-5 text-2xl font-bold text-white">Вход в личный кабинет</h1>
            <p className="mt-2 text-sm text-slate-400">Управление лицензиями и серверами FloV:MP</p>
          </div>

          {error && (
            <div className="mt-6 flex items-center gap-3 rounded-xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400 animate-slide-down">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="mt-7 space-y-5">
            <div>
              <FieldLabel>Email</FieldLabel>
              <div className="relative">
                <Mail className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
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
              <FieldLabel>Пароль</FieldLabel>
              <div className="relative">
                <KeyRound className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
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
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500 transition hover:text-white"
                >
                  {showPass ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>

            <button type="submit" disabled={loading} className="btn btn-primary h-11 w-full text-sm disabled:opacity-50">
              {loading ? (
                <>
                  <Spinner className="h-4 w-4" /> Авторизация…
                </>
              ) : (
                <>
                  Войти в аккаунт <ArrowRight className="h-4 w-4" />
                </>
              )}
            </button>
          </form>

          <div className="mt-7 border-t border-white/[0.08] pt-6 text-center text-xs text-slate-400">
            Нет аккаунта?{' '}
            <Link href="/auth/register" className="font-semibold text-brand hover:underline">
              Зарегистрироваться и получить ключ
            </Link>
          </div>
        </div>

        <p className="mt-5 flex items-center justify-center gap-2 text-center font-mono text-[11px] text-slate-500">
          <ShieldCheck className="h-3.5 w-3.5 text-emeraldx" />
          Сессия защищена JWT · cookie httpOnly
        </p>
      </div>
    </div>
  );
}
