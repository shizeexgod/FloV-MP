'use client';

import React, { useEffect, useRef, useState } from 'react';
import { Check, Menu, Moon, Sun } from 'lucide-react';
import { useI18n, useT } from '@/lib/i18n';
import { ACCENTS, useTheme, type AccentKey } from '@/lib/theme';

/** Accent / language / theme controls — shared by the desktop dropdown and the mobile sheet. */
export function ThemeControls({ ringOffset = '#131316' }: { ringOffset?: string }) {
  const t = useT();
  const { lang, setLang } = useI18n();
  const { mode, setMode, accent, setAccent } = useTheme();

  const seg = (active: boolean) =>
    `flex items-center justify-center gap-1.5 rounded-lg border py-1.5 text-[12px] font-semibold transition-colors ${
      active ? 'border-brand bg-brand text-[#0a0a0c]' : 'border-white/10 text-white/55 hover:text-white'
    }`;
  const label = 'font-mono text-[10px] font-semibold uppercase tracking-[0.18em] text-white/40';

  return (
    <div className="space-y-3.5">
      <div>
        <div className={label}>{t.common.accent}</div>
        <div className="mt-2.5 flex items-center gap-2">
          {ACCENTS.map((a) => (
            <button
              key={a.key}
              type="button"
              onClick={() => setAccent(a.key as AccentKey)}
              aria-label={a.label}
              aria-pressed={accent === a.key}
              className="grid h-7 w-7 place-items-center rounded-full transition-transform hover:scale-110"
              style={{
                backgroundColor: a.base,
                boxShadow: accent === a.key ? `0 0 0 2px ${ringOffset}, 0 0 0 4px rgba(255,255,255,0.7)` : 'none',
              }}
            >
              {accent === a.key && <Check className="h-3.5 w-3.5 text-white" />}
            </button>
          ))}
        </div>
      </div>

      <hr className="rule-soft" />

      <div>
        <div className={label}>{t.common.language}</div>
        <div className="mt-2 grid grid-cols-2 gap-1.5">
          {(['ru', 'en'] as const).map((l) => (
            <button key={l} type="button" onClick={() => setLang(l)} aria-pressed={lang === l} className={seg(lang === l)}>
              {l.toUpperCase()}
            </button>
          ))}
        </div>
      </div>

      <hr className="rule-soft" />

      <div>
        <div className={label}>{t.common.theme}</div>
        <div className="mt-2 grid grid-cols-2 gap-1.5">
          <button type="button" onClick={() => setMode('light')} aria-pressed={mode === 'light'} className={seg(mode === 'light')}>
            <Sun className="h-3.5 w-3.5" />
            {t.common.themeLight}
          </button>
          <button type="button" onClick={() => setMode('dark')} aria-pressed={mode === 'dark'} className={seg(mode === 'dark')}>
            <Moon className="h-3.5 w-3.5" />
            {t.common.themeDark}
          </button>
        </div>
      </div>
    </div>
  );
}

/** ☰ dropdown for the desktop header. */
export default function ThemeMenu() {
  const t = useT();
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  return (
    <div ref={wrapRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-label={t.common.menu}
        aria-expanded={open}
        className={`grid h-9 w-9 place-items-center rounded-xl border transition-colors ${
          open ? 'border-white/20 bg-white/[0.08] text-white' : 'border-white/10 bg-white/[0.04] text-white/70 hover:text-white'
        }`}
      >
        <Menu className="h-4 w-4" />
      </button>

      <div
        className={`absolute right-0 top-[calc(100%+0.55rem)] w-64 origin-top-right rounded-2xl border border-white/[0.12] p-4 shadow-[0_24px_60px_-20px_rgba(0,0,0,0.7)] transition-all duration-200 ${
          open ? 'pointer-events-auto scale-100 opacity-100' : 'pointer-events-none scale-95 opacity-0'
        }`}
        style={{ backgroundColor: 'var(--panel)' }}
        role="menu"
      >
        <ThemeControls ringOffset="var(--panel)" />
      </div>
    </div>
  );
}
