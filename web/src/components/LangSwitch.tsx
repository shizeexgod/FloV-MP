'use client';

import React from 'react';
import { useI18n } from '@/lib/i18n';

export default function LangSwitch({ className = '' }: { className?: string }) {
  const { lang, setLang, t } = useI18n();
  return (
    <div
      className={`relative inline-grid h-9 w-[76px] grid-cols-2 items-center rounded-xl border border-white/10 bg-white/[0.03] p-1 ${className}`}
      role="group"
      aria-label={t.common.language}
    >
      <span
        aria-hidden="true"
        className={`pointer-events-none absolute left-1 top-1 h-7 w-[34px] rounded-lg bg-brand transition-transform duration-200 ease-[cubic-bezier(.2,.8,.2,1)] ${
          lang === 'en' ? 'translate-x-[34px]' : 'translate-x-0'
        }`}
      />
      {(['ru', 'en'] as const).map((l) => (
        <button
          key={l}
          type="button"
          onClick={() => setLang(l)}
          aria-pressed={lang === l}
          aria-label={l === 'ru' ? 'Русский' : 'English'}
          className={`relative z-10 grid h-7 place-items-center rounded-lg font-mono text-[10px] font-semibold uppercase transition-colors duration-200 ${
            lang === l ? 'text-[#0a0a0c]' : 'text-white/45 hover:text-white/80'
          }`}
        >
          {l}
        </button>
      ))}
    </div>
  );
}
