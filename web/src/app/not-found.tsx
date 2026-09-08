'use client';

import React from 'react';
import Link from 'next/link';
import { useT } from '@/lib/i18n';

export default function NotFound() {
  const t = useT();
  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center px-5 text-center">
      <div className="font-mono text-6xl font-semibold text-white/15">404</div>
      <h1 className="mt-4 text-xl font-semibold tracking-tight text-white">{t.notFound.title}</h1>
      <p className="mt-2 text-[13px] text-white/45">{t.notFound.sub}</p>
      <Link href="/" className="btn btn-primary mt-6 h-10 px-5 text-sm">
        {t.common.backHome}
      </Link>
    </div>
  );
}
