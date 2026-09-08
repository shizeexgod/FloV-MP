'use client';

import React from 'react';
import { useI18n } from '@/lib/i18n';

export default function LangSwitch({ className = '' }: { className?: string }) {
  const { lang, setLang } = useI18n();
  return (
    <div
      className={`inline-flex items-center rounded-lg border border-white/10 bg-white/[0.03] p-0.5 ${className}`}
      role="group"
      aria-label="Language"
    >
      {(['ru', 'en'] as const).map((l) => (
        <button
          key={l}
          type="button"
          onClick={() => setLang(l)}
          aria-pressed={lang === l}
          className={`rounded-md px-2 py-1 font-mono text-[11px] font-semibold uppercase transition-colors ${
            lang === l ? 'bg-brand text-[#0a0a0c]' : 'text-white/45 hover:text-white/80'
          }`}
        >
          {l}
        </button>
      ))}
    </div>
  );
}
