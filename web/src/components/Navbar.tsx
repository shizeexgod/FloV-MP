'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import Image from 'next/image';
import { Shield, Server, Terminal, User, LogOut, Menu, X, Key, Zap, ArrowRight } from 'lucide-react';

export default function Navbar() {
  const [user, setUser] = useState<{ username: string; email: string; role?: string } | null>(null);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  useEffect(() => {
    fetch('/api/auth/me')
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data?.authenticated) {
          setUser(data.user);
        }
      })
      .catch(() => {});
  }, []);

  const handleLogout = async () => {
    await fetch('/api/auth/logout', { method: 'POST' });
    setUser(null);
    window.location.href = '/';
  };

  return (
    <nav className="sticky top-0 z-50 glass-panel border-b border-white/10 bg-surface-400/90 backdrop-blur-xl">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-20">
          {/* Brand Logo with actual Codex FloV:MP badge */}
          <Link href="/" className="flex items-center gap-3.5 group">
            <div className="relative w-12 h-12 rounded-2xl overflow-hidden border border-brand/50 shadow-neon-pink bg-surface-300 group-hover:scale-105 transition-transform">
              <img
                src="/branding/logo-codex.png"
                alt="FloV:MP Logo"
                className="w-full h-full object-cover"
              />
            </div>
            <div className="flex flex-col">
              <div className="flex items-center gap-1.5">
                <span className="font-black text-2xl tracking-wider text-white">
                  FloV<span className="text-brand">:MP</span>
                </span>
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-brand/20 border border-brand/40 text-brand font-mono font-bold uppercase tracking-wider">
                  SaaS Engine
                </span>
              </div>
              <span className="text-[11px] text-gray-400 font-medium tracking-tight">
                Автономный мультиплеерный рантайм GTA V
              </span>
            </div>
          </Link>

          {/* Desktop Nav Links (structured like icsnotify.ru) */}
          <div className="hidden lg:flex items-center space-x-7">
            <a href="#about" className="text-sm font-medium text-gray-300 hover:text-white transition-colors">
              О платформе
            </a>
            <a href="#services" className="text-sm font-medium text-gray-300 hover:text-white transition-colors">
              Возможности
            </a>
            <a href="#tech" className="text-sm font-medium text-gray-300 hover:text-white transition-colors">
              Стек & Сеть
            </a>
            <a href="#process" className="text-sm font-medium text-gray-300 hover:text-white transition-colors">
              6 шагов запуска
            </a>
            <a href="/#pricing" className="text-sm font-medium text-gray-300 hover:text-white transition-colors">
              Тарифы
            </a>
            <Link href="/docs" className="text-sm font-medium text-brand hover:text-brand-hover transition-colors flex items-center gap-1">
              <span>Документация</span>
            </Link>
            <a href="/#contacts" className="text-sm font-medium text-gray-300 hover:text-white transition-colors">
              Контакты
            </a>
          </div>

          {/* Right Action Buttons */}
          <div className="hidden md:flex items-center gap-3">
            {user ? (
              <div className="flex items-center gap-3">
                {user.role === 'admin' && (
                  <Link
                    href="/admin"
                    className="px-3 py-2 rounded-xl bg-red-500/20 border border-red-500/40 text-red-400 hover:bg-red-500/30 text-xs font-bold font-mono transition-colors"
                  >
                    Админ-панель
                  </Link>
                )}
                <Link
                  href="/dashboard"
                  className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-brand/20 border border-brand/40 text-brand hover:bg-brand/30 transition-colors font-semibold text-sm shadow-neon-pink"
                >
                  <Key className="w-4 h-4" />
                  <span>Кабинет ({user.username})</span>
                </Link>
                <button
                  onClick={handleLogout}
                  className="p-2.5 rounded-xl text-gray-400 hover:text-red-400 hover:bg-white/5 transition-colors"
                  title="Выйти"
                >
                  <LogOut className="w-4 h-4" />
                </button>
              </div>
            ) : (
              <div className="flex items-center gap-3">
                <Link
                  href="/auth/login"
                  className="px-4 py-2.5 rounded-xl text-sm font-semibold text-gray-300 hover:text-white hover:bg-white/5 transition-colors"
                >
                  Войти
                </Link>
                <Link
                  href="/auth/register"
                  className="px-5 py-2.5 rounded-xl text-sm font-bold bg-gradient-to-r from-brand to-pink-600 hover:from-brand-hover hover:to-pink-500 text-white shadow-neon-pink transition-all flex items-center gap-2"
                >
                  <span>Подключить FloV:MP</span>
                  <ArrowRight className="w-4 h-4" />
                </Link>
              </div>
            )}
          </div>

          {/* Mobile Menu Toggle */}
          <div className="lg:hidden flex items-center">
            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="p-2.5 rounded-xl bg-surface-300 text-gray-300 hover:text-white"
            >
              {mobileMenuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
            </button>
          </div>
        </div>
      </div>

      {/* Mobile Dropdown */}
      {mobileMenuOpen && (
        <div className="lg:hidden border-t border-white/10 bg-surface-300 px-5 pt-4 pb-8 space-y-3">
          <a
            href="#about"
            onClick={() => setMobileMenuOpen(false)}
            className="block py-2 text-base font-medium text-gray-300 hover:text-white"
          >
            О платформе
          </a>
          <a
            href="#services"
            onClick={() => setMobileMenuOpen(false)}
            className="block py-2 text-base font-medium text-gray-300 hover:text-white"
          >
            Возможности
          </a>
          <a
            href="#tech"
            onClick={() => setMobileMenuOpen(false)}
            className="block py-2 text-base font-medium text-gray-300 hover:text-white"
          >
            Стек & Сеть
          </a>
          <a
            href="#process"
            onClick={() => setMobileMenuOpen(false)}
            className="block py-2 text-base font-medium text-gray-300 hover:text-white"
          >
            6 шагов запуска
          </a>
          <a
            href="#pricing"
            onClick={() => setMobileMenuOpen(false)}
            className="block py-2 text-base font-medium text-gray-300 hover:text-white"
          >
            Тарифы
          </a>
          <a
            href="#contacts"
            onClick={() => setMobileMenuOpen(false)}
            className="block py-2 text-base font-medium text-gray-300 hover:text-white"
          >
            Контакты
          </a>
          <div className="pt-3 border-t border-white/10 flex flex-col gap-2">
            {user ? (
              <>
                <Link
                  href="/dashboard"
                  onClick={() => setMobileMenuOpen(false)}
                  className="w-full text-center py-3 rounded-xl bg-brand text-white font-bold"
                >
                  Панель управления ({user.username})
                </Link>
                <button
                  onClick={handleLogout}
                  className="w-full py-2 text-center text-sm text-red-400"
                >
                  Выйти из системы
                </button>
              </>
            ) : (
              <>
                <Link
                  href="/auth/login"
                  onClick={() => setMobileMenuOpen(false)}
                  className="w-full text-center py-3 rounded-xl bg-surface-200 text-white font-semibold border border-white/10"
                >
                  Вход для клиентов
                </Link>
                <Link
                  href="/auth/register"
                  onClick={() => setMobileMenuOpen(false)}
                  className="w-full text-center py-3 rounded-xl bg-brand text-white font-bold shadow-neon-pink"
                >
                  Подключить FloV:MP
                </Link>
              </>
            )}
          </div>
        </div>
      )}
    </nav>
  );
}
