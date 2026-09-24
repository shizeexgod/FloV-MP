'use client';

import React, { useEffect, useRef, useState } from 'react';
import { Check, ChevronDown, Globe2, X } from 'lucide-react';
import { type Lang } from '@/lib/dict';
import { useI18n } from '@/lib/i18n';

const LANGUAGES: Array<{ id: Lang; label: string; native: string }> = [
  { id: 'ru', label: 'Русский', native: 'RU' },
  { id: 'en', label: 'English', native: 'EN' },
];

/** A small footer trigger opens a focused language sheet instead of a persistent toggle. */
export default function LangMenu({ className = '' }: { className?: string }) {
  const { lang, setLang, t } = useI18n();
  const [open, setOpen] = useState(false);
  const closeRef = useRef<HTMLButtonElement | null>(null);
  const selected = LANGUAGES.find((item) => item.id === lang) ?? LANGUAGES[0];

  useEffect(() => {
    if (!open) return;
    closeRef.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false); };
    document.body.style.overflow = 'hidden';
    window.addEventListener('keydown', onKeyDown);
    return () => {
      document.body.style.overflow = '';
      window.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  const choose = (next: Lang) => {
    setLang(next);
    setOpen(false);
  };

  return (
    <div className={className}>
      <button type="button" onClick={() => setOpen(true)} className="language-trigger" aria-haspopup="dialog" aria-expanded={open}>
        <Globe2 aria-hidden="true" className="h-4 w-4" />
        <span>{selected.label}</span>
        <ChevronDown aria-hidden="true" className="h-4 w-4" />
      </button>

      {open ? (
        <div className="language-overlay" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setOpen(false); }}>
          <section className="language-sheet" role="dialog" aria-modal="true" aria-label={t.common.language}>
            <button ref={closeRef} type="button" onClick={() => setOpen(false)} className="language-close" aria-label={lang === 'ru' ? 'Закрыть' : 'Close'}><X aria-hidden="true" className="h-4 w-4" /></button>
            <Globe2 aria-hidden="true" className="mx-auto h-7 w-7 text-white/[0.36]" />
            <p className="mt-5 font-mono text-[10px] font-semibold uppercase tracking-[0.2em] text-white/[0.38]">{t.common.language}</p>
            <div className="mt-5 grid">
              {LANGUAGES.map((item) => (
                <button key={item.id} type="button" onClick={() => choose(item.id)} className={`language-option ${item.id === lang ? 'is-selected' : ''}`}>
                  <span>{item.label}</span><small>{item.native}</small>{item.id === lang ? <Check aria-hidden="true" className="h-4 w-4" /> : null}
                </button>
              ))}
            </div>
          </section>
        </div>
      ) : null}
    </div>
  );
}
