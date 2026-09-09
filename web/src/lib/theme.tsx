'use client';

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';

export type ThemeMode = 'light' | 'dark';
export type AccentKey = 'pink' | 'violet' | 'blue' | 'emerald' | 'amber';

interface AccentDef {
  key: AccentKey;
  label: string;
  base: string;
  hi: string;
  lo: string;
}

/** Presets. `base` = --brand, `hi` = lighter hover, `lo` = darker press. */
export const ACCENTS: AccentDef[] = [
  { key: 'pink', label: 'Розовый', base: '#ff3d8a', hi: '#ff70ab', lo: '#d81f6a' },
  { key: 'violet', label: 'Фиолетовый', base: '#8b5cf6', hi: '#a985fb', lo: '#6d3fd6' },
  { key: 'blue', label: 'Синий', base: '#3b82f6', hi: '#6ba5fb', lo: '#2563d4' },
  { key: 'emerald', label: 'Изумруд', base: '#10b981', hi: '#3fd6a3', lo: '#0a8f63' },
  { key: 'amber', label: 'Янтарь', base: '#f59e0b', hi: '#ffb838', lo: '#c47d05' },
];

const ACCENT_KEY = 'flovmp_accent';
const MODE_KEY = 'flovmp_theme';

interface Ctx {
  mode: ThemeMode;
  setMode: (m: ThemeMode) => void;
  accent: AccentKey;
  setAccent: (a: AccentKey) => void;
}

const ThemeContext = createContext<Ctx | null>(null);

function applyAccent(key: AccentKey) {
  const a = ACCENTS.find((x) => x.key === key) ?? ACCENTS[0];
  const root = document.documentElement;
  root.style.setProperty('--brand', a.base);
  root.style.setProperty('--brand-hi', a.hi);
  root.style.setProperty('--brand-lo', a.lo);
}

function applyMode(mode: ThemeMode) {
  document.documentElement.setAttribute('data-theme', mode);
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>('dark');
  const [accent, setAccentState] = useState<AccentKey>('pink');

  // Hydrate from the values the pre-paint script already applied.
  useEffect(() => {
    try {
      const savedMode = localStorage.getItem(MODE_KEY);
      if (savedMode === 'light' || savedMode === 'dark') setModeState(savedMode);
      const savedAccent = localStorage.getItem(ACCENT_KEY) as AccentKey | null;
      if (savedAccent && ACCENTS.some((a) => a.key === savedAccent)) setAccentState(savedAccent);
    } catch {
      /* ignore */
    }
  }, []);

  const setMode = useCallback((m: ThemeMode) => {
    setModeState(m);
    applyMode(m);
    try {
      localStorage.setItem(MODE_KEY, m);
    } catch {
      /* ignore */
    }
  }, []);

  const setAccent = useCallback((a: AccentKey) => {
    setAccentState(a);
    applyAccent(a);
    try {
      localStorage.setItem(ACCENT_KEY, a);
    } catch {
      /* ignore */
    }
  }, []);

  const value = useMemo<Ctx>(() => ({ mode, setMode, accent, setAccent }), [mode, setMode, accent, setAccent]);

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): Ctx {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error('useTheme must be used within ThemeProvider');
  return ctx;
}

/** Inline, runs before first paint — no theme flash. Keep in sync with ACCENTS. */
export const THEME_BOOT_SCRIPT = `(function(){try{
var m=localStorage.getItem('${MODE_KEY}');if(m!=='light'&&m!=='dark')m='dark';
document.documentElement.setAttribute('data-theme',m);
var A={pink:['#ff3d8a','#ff70ab','#d81f6a'],violet:['#8b5cf6','#a985fb','#6d3fd6'],blue:['#3b82f6','#6ba5fb','#2563d4'],emerald:['#10b981','#3fd6a3','#0a8f63'],amber:['#f59e0b','#ffb838','#c47d05']};
var a=localStorage.getItem('${ACCENT_KEY}');if(!A[a])a='pink';
var s=document.documentElement.style;
s.setProperty('--brand',A[a][0]);s.setProperty('--brand-hi',A[a][1]);s.setProperty('--brand-lo',A[a][2]);
}catch(e){}})();`;
