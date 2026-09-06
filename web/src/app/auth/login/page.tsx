'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Zap, Lock, Mail, ArrowRight, AlertCircle } from 'lucide-react';

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
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
      if (!res.ok) {
        throw new Error(data.error || 'Ошибка входа');
      }

      router.push('/dashboard');
      router.refresh();
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-[80vh] flex items-center justify-center px-4 py-12">
      <div className="w-full max-w-md">
        <div className="glass-panel p-8 rounded-3xl border border-white/10 shadow-glass">
          {/* Header */}
          <div className="text-center mb-8">
            <div className="inline-flex w-12 h-12 rounded-2xl bg-brand/20 border border-brand/40 items-center justify-center text-brand mb-4 shadow-neon-pink">
              <Zap className="w-6 h-6" />
            </div>
            <h1 className="text-2xl font-bold text-white tracking-tight">Вход в личный кабинет</h1>
            <p className="text-sm text-gray-400 mt-2">
              Управление лицензиями и серверами FloV:MP
            </p>
          </div>

          {error && (
            <div className="mb-6 p-4 rounded-xl bg-red-500/10 border border-red-500/30 flex items-center gap-3 text-red-400 text-sm">
              <AlertCircle className="w-5 h-5 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                Email
              </label>
              <div className="relative">
                <Mail className="w-5 h-5 absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400" />
                <input
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="admin@myproject.ru"
                  className="w-full pl-11 pr-4 py-3 bg-surface-300 border border-white/10 rounded-xl text-white placeholder-gray-500 text-sm focus:outline-none focus:border-brand transition-colors"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-300 uppercase tracking-wider mb-2 font-mono">
                Пароль
              </label>
              <div className="relative">
                <Lock className="w-5 h-5 absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400" />
                <input
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  className="w-full pl-11 pr-4 py-3 bg-surface-300 border border-white/10 rounded-xl text-white placeholder-gray-500 text-sm focus:outline-none focus:border-brand transition-colors"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full mt-6 py-3.5 rounded-xl bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white font-bold text-sm shadow-neon-pink flex items-center justify-center gap-2 transition-all disabled:opacity-50"
            >
              {loading ? (
                <span>Авторизация...</span>
              ) : (
                <>
                  <span>Войти в аккаунт</span>
                  <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>

          <div className="mt-8 pt-6 border-t border-white/10 text-center text-xs text-gray-400">
            Нет аккаунта?{' '}
            <Link href="/auth/register" className="text-brand font-semibold hover:underline">
              Зарегистрироваться и получить ключ
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
