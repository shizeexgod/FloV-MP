'use client';

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { DICT, type Lang } from './dict';

const STORAGE_KEY = 'flovmp_lang';

interface Ctx {
  lang: Lang;
  setLang: (l: Lang) => void;
  t: (path: string) => string;
  /** returns an array (for list-type keys) */
  tl: (path: string) => string[];
}

const I18nContext = createContext<Ctx | null>(null);

function resolve(obj: any, path: string): any {
  return path.split('.').reduce((acc, k) => (acc == null ? acc : acc[k]), obj);
}

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [lang, setLangState] = useState<Lang>('ru');

  useEffect(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY) as Lang | null;
      if (saved === 'ru' || saved === 'en') setLangState(saved);
    } catch {
      /* ignore */
    }
  }, []);

  useEffect(() => {
    if (typeof document !== 'undefined') document.documentElement.lang = lang;
  }, [lang]);

  const setLang = useCallback((l: Lang) => {
    setLangState(l);
    try {
      localStorage.setItem(STORAGE_KEY, l);
    } catch {
      /* ignore */
    }
  }, []);

  const t = useCallback(
    (path: string): string => {
      const v = resolve(DICT[lang], path);
      if (typeof v === 'string') return v;
      const fallback = resolve(DICT.ru, path);
      return typeof fallback === 'string' ? fallback : path;
    },
    [lang]
  );

  const tl = useCallback(
    (path: string): string[] => {
      const v = resolve(DICT[lang], path);
      if (Array.isArray(v)) return v as string[];
      const fallback = resolve(DICT.ru, path);
      return Array.isArray(fallback) ? (fallback as string[]) : [];
    },
    [lang]
  );

  const value = useMemo<Ctx>(() => ({ lang, setLang, t, tl }), [lang, setLang, t, tl]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): Ctx {
  const ctx = useContext(I18nContext);
  if (!ctx) {
    // Safe fallback if a component renders outside the provider (e.g. isolated tests)
    return {
      lang: 'ru',
      setLang: () => {},
      t: (p) => {
        const v = resolve(DICT.ru, p);
        return typeof v === 'string' ? v : p;
      },
      tl: (p) => {
        const v = resolve(DICT.ru, p);
        return Array.isArray(v) ? v : [];
      },
    };
  }
  return ctx;
}

export type { Lang };
