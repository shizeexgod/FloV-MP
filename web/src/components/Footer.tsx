import React from 'react';
import Link from 'next/link';
import { Zap, ShieldCheck, Cpu, Code2, Globe } from 'lucide-react';

export default function Footer() {
  return (
    <footer className="border-t border-white/10 bg-surface-400 py-12 text-sm text-gray-400">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-8 mb-8">
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-lg bg-brand flex items-center justify-center text-white">
                <Zap className="w-4 h-4" />
              </div>
              <span className="font-bold text-lg text-white">
                FloV<span className="text-brand">:MP</span>
              </span>
            </div>
            <p className="text-xs text-gray-400 leading-relaxed">
              Независимый мультиплеерный движок нового поколения для GTA V. Полная автономия сетевого стека, C# .NET 8 ядро, синхронизация NPC и встроенный 3D Voice.
            </p>
            <div className="flex items-center gap-3 text-xs text-gray-500 font-mono">
              <span className="inline-block w-2 h-2 rounded-full bg-emerald-400"></span>
              Node: 188.127.229.224:7788
            </div>
          </div>

          <div>
            <h4 className="font-semibold text-white mb-4 text-xs uppercase tracking-wider">Платформа</h4>
            <ul className="space-y-2 text-xs">
              <li><a href="#features" className="hover:text-white transition-colors">Сетевой протокол UDP</a></li>
              <li><a href="#tech" className="hover:text-white transition-colors">C# CoreCLR Gamemode</a></li>
              <li><a href="#features" className="hover:text-white transition-colors">WebRTC 3D Voice Chat</a></li>
              <li><a href="#features" className="hover:text-white transition-colors">Синхронизация NPC/Peds</a></li>
              <li><a href="#tech" className="hover:text-white transition-colors">CEF / NUI UI-стек</a></li>
            </ul>
          </div>

          <div>
            <h4 className="font-semibold text-white mb-4 text-xs uppercase tracking-wider">Клиентам</h4>
            <ul className="space-y-2 text-xs">
              <li><Link href="/auth/register" className="hover:text-white transition-colors">Получить лицензионный ключ</Link></li>
              <li><Link href="/dashboard" className="hover:text-white transition-colors">Привязать IP сервера</Link></li>
              <li><a href="#pricing" className="hover:text-white transition-colors">Тарифные планы</a></li>
              <li><a href="#faq" className="hover:text-white transition-colors">Миграция с RAGE:MP / FiveM</a></li>
            </ul>
          </div>

          <div>
            <h4 className="font-semibold text-white mb-4 text-xs uppercase tracking-wider">Экосистема</h4>
            <ul className="space-y-2 text-xs">
              <li><span className="text-gray-300">Держава Онлайн (RP Проект)</span></li>
              <li><span className="text-gray-300">FastDL CDN Mirror</span></li>
              <li><span className="text-gray-300">FloV:MP Native Launcher</span></li>
              <li><span className="text-gray-300">8-уровневая админ-система</span></li>
            </ul>
          </div>
        </div>

        <div className="pt-8 border-t border-white/5 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-gray-500">
          <p>© 2026 FloV:MP Engine & Держава Онлайн. Все права защищены.</p>
          <div className="flex items-center gap-4">
            <span className="flex items-center gap-1">
              <ShieldCheck className="w-3.5 h-3.5 text-brand" />
              SaaS Engine Licensing v1.4
            </span>
            <span>•</span>
            <span className="flex items-center gap-1">
              <Cpu className="w-3.5 h-3.5 text-cyan-neon" />
              CoreCLR .NET 8 / MariaDB
            </span>
          </div>
        </div>
      </div>
    </footer>
  );
}
