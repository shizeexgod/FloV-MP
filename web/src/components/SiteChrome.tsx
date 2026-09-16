'use client';

import React from 'react';
import { usePathname } from 'next/navigation';
import { useT } from '@/lib/i18n';
import Navbar from './Navbar';
import Footer from './Footer';

export default function SiteChrome({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const t = useT();
  const isWorkspace = pathname.startsWith('/dashboard');

  if (isWorkspace) {
    return <main id="main-content" className="min-h-dvh">{children}</main>;
  }

  return (
    <>
      <a
        href="#main-content"
        className="fixed left-3 top-3 z-[100] -translate-y-20 rounded-lg bg-brand px-4 py-2 text-xs font-bold text-[#16040c] transition-transform focus-visible:translate-y-0"
      >
        {t.common.skipContent}
      </a>
      <Navbar />
      <main id="main-content" className="flex-1">{children}</main>
      <Footer />
    </>
  );
}
