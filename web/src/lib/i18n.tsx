'use client';

import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { DICT, type Lang } from './dict';

const KEY = 'flovmp_lang';

interface Ctx {
  lang: Lang;
  setLang: (l: Lang) => void;
  t: (typeof DICT)['ru'];
}

const I18nContext = createContext<Ctx | null>(null);

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [lang, setLangState] = useState<Lang>('ru');
  const languageTimer = useRef<number | null>(null);

  useEffect(() => {
    try {
      const saved = localStorage.getItem(KEY) as Lang | null;
      if (saved === 'ru' || saved === 'en') setLangState(saved);
      else setLangState('ru');
    } catch {
      /* ignore */
    }
  }, []);

  useEffect(() => {
    try {
      document.documentElement.lang = lang;
    } catch {
      /* ignore */
    }
  }, [lang]);

  useEffect(() => {
    return () => {
      if (languageTimer.current !== null) window.clearTimeout(languageTimer.current);
      delete document.documentElement.dataset.languageSwitching;
    };
  }, []);

  const setLang = useCallback((l: Lang) => {
    if (l === lang) return;
    document.documentElement.dataset.languageSwitching = 'true';
    setLangState(l);
    try {
      localStorage.setItem(KEY, l);
    } catch {
      /* ignore */
    }
    if (languageTimer.current !== null) window.clearTimeout(languageTimer.current);
    languageTimer.current = window.setTimeout(() => {
      delete document.documentElement.dataset.languageSwitching;
      languageTimer.current = null;
    }, 280);
  }, [lang]);

  const value = useMemo<Ctx>(() => ({ lang, setLang, t: DICT[lang] }), [lang, setLang]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): Ctx {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error('useI18n must be used within I18nProvider');
  return ctx;
}

/** Shorthand: const t = useT(); t.home.h1a */
export function useT() {
  return useI18n().t;
}
