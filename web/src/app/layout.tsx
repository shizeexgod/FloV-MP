import type { Metadata } from 'next';
import './globals.css';
import Navbar from '@/components/Navbar';
import Footer from '@/components/Footer';

export const metadata: Metadata = {
  metadataBase: new URL('https://flovmp.ru'),
  title: {
    default: 'FloV:MP — Автономный мультиплеерный движок и SaaS-платформа для GTA V',
    template: '%s — FloV:MP',
  },
  description:
    'SaaS-платформа и автономный C# .NET 8 движок мультиплеера для GTA V. Встроенный 3D Voice, синхронизация NPC и трафика, FastDL CDN, криптографическая верификация лицензий и архитектура под 1500+ игроков без зависимости от Take-Two, RAGE:MP и alt:V backend.',
  keywords: [
    'FloV:MP',
    'мультиплеер GTA V',
    'альтернатива alt:V',
    'альтернатива RAGE:MP',
    'RP сервер',
    'SaaS движок',
    'FastDL CDN',
  ],
  openGraph: {
    title: 'FloV:MP — Автономный мультиплеерный движок для GTA V',
    description:
      'Независимый рантайм, встроенный 3D Voice, FastDL CDN и кастомный лаунчер под ваш RP-проект.',
    type: 'website',
    locale: 'ru_RU',
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="ru" className="dark">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800;900&family=JetBrains+Mono:wght@400;500;600;700&display=swap"
          rel="stylesheet"
        />
      </head>
      <body className="min-h-screen flex flex-col bg-background text-foreground antialiased">
        <div className="page-aura" aria-hidden />
        <Navbar />
        <main className="flex-1">{children}</main>
        <Footer />
      </body>
    </html>
  );
}
