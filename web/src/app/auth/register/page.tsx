'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  AlertCircle,
  ArrowRight,
  Check,
  Eye,
  EyeOff,
  KeyRound,
  Mail,
  Send,
  Sparkles,
  User,
} from 'lucide-react';
import { AuroraBlobs, FieldLabel, Spinner } from '@/components/ui';

export default function RegisterPage() {
  const router = useRouter();
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [telegram, setTelegram] = useState('');
  const [showPass, setShowPass] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const res = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, email, password, telegram }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || 'Ошибка регистрации');
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
      <div className="relative w-full max-w-lg">
        <div className="glass-panel card-edge rounded-3xl p-8 shadow-glass sm:p-10">
          <div className="text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl border border-brand/40 bg-brand/10 text-brand shadow-neon-pink">
              <Sparkles className="h-6 w-6" />
            </div>
            <h1 className="mt-5 text-2xl font-bold text-white">Регистрация в FloV:MP</h1>
            <p className="mt-2 text-sm text-slate-400">
              Бесплатная лицензия Indie на 128 слотов — сразу после создания аккаунта
            </p>
          </div>

          {error && (
            <div className="mt-6 flex items-center gap-3 rounded-xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400 animate-slide-down">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="mt-7 space-y-5">
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
              <div>
                <FieldLabel>Логин</FieldLabel>
                <div className="relative">
                  <User className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
                  <input
                    type="text"
                    required
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    placeholder="MikhailDev"
                    className="field h-11 pl-10 pr-4"
                  />
                </div>
              </div>
              <div>
                <FieldLabel>Telegram</FieldLabel>
                <div className="relative">
                  <Send className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
                  <input
                    type="text"
                    value={telegram}
                    onChange={(e) => setTelegram(e.target.value)}
                    placeholder="@username"
                    className="field h-11 pl-10 pr-4"
                  />
                </div>
              </div>
            </div>

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
                  placeholder="admin@derzhava-rp.ru"
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
                  minLength={6}
                  autoComplete="new-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Минимум 6 символов"
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

            <div className="rounded-xl border border-emeraldx/20 bg-emeraldx/[0.06] px-4 py-3.5">
              <div className="flex items-center gap-2 text-xs font-semibold text-emeraldx">
                <Check className="h-4 w-4" />
                Автоматически создаётся лицензия «Инди» (128 слотов, 30 дней)
              </div>
              <p className="mt-1.5 text-[11px] text-slate-400">
                Ключ появится в личном кабинете сразу после регистрации.
              </p>
            </div>

            <button type="submit" disabled={loading} className="btn btn-primary h-11 w-full text-sm disabled:opacity-50">
              {loading ? (
                <>
                  <Spinner className="h-4 w-4" /> Создание аккаунта…
                </>
              ) : (
                <>
                  Создать аккаунт и получить ключ <ArrowRight className="h-4 w-4" />
                </>
              )}
            </button>
          </form>

          <div className="mt-7 border-t border-white/[0.08] pt-6 text-center text-xs text-slate-400">
            Уже есть аккаунт?{' '}
            <Link href="/auth/login" className="font-semibold text-brand hover:underline">
              Войти в личный кабинет
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
