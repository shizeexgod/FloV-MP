import type { Metadata } from 'next';
import './globals.css';
import { I18nProvider } from '@/lib/i18n';
import { ThemeProvider, THEME_BOOT_SCRIPT } from '@/lib/theme';
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

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="ru" data-theme="dark" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: THEME_BOOT_SCRIPT }} />
      </head>
      <body className="min-h-screen flex flex-col bg-background text-foreground antialiased">
        <ThemeProvider>
          <I18nProvider>
            <div className="page-aura" aria-hidden />
            <Navbar />
            <main className="flex-1">{children}</main>
            <Footer />
          </I18nProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
