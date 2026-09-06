import type { Metadata } from 'next';
import './globals.css';
import Navbar from '@/components/Navbar';
import Footer from '@/components/Footer';

export const metadata: Metadata = {
  title: 'FloV:MP — Независимый мультиплеерный движок нового поколения для GTA V',
  description:
    'SaaS-платформа и автономный C# .NET 8 движок мультиплеера для GTA V. Встроенный 3D Voice, синхронизация NPC, 1500+ игроков, защита и готовая экосистема под RP проекты.',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="ru" className="dark">
      <body className="min-h-screen flex flex-col bg-background text-foreground antialiased selection:bg-brand selection:text-white">
        <Navbar />
        <main className="flex-1">{children}</main>
        <Footer />
      </body>
    </html>
  );
}
