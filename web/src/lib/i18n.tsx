'use client';

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
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

  const setLang = useCallback((l: Lang) => {
    if (l === lang) return;

    const commit = () => {
      setLangState(l);
      try {
        localStorage.setItem(KEY, l);
      } catch {
        /* ignore */
      }
      window.scrollTo({ top: 0, left: 0, behavior: 'auto' });
    };

    // A language choice must return to the beginning of the page immediately.
    // Avoid a document-wide view-transition here: it crossfades two scroll positions
    // and makes the old footer briefly ghost over the hero.
    commit();
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
