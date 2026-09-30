import { useEffect, useMemo } from 'react';
import type { ReactNode } from 'react';
import { I18nContext } from './I18nContext';
import type { I18n } from './I18nContext';
import { ru } from './ru';
import { translate } from './translate';
import type { Messages } from './translate';

/** The catalogues this build carries, by locale. A second locale is one more entry here. */
const catalogues: Readonly<Partial<Record<string, Messages>>> = { ru };

/**
 * Supplies the locale and its translate function, and declares the language on the document —
 * `<html lang>` — so a screen reader and a search engine read the page in the right language.
 */
export function I18nProvider({ locale = 'ru', children }: { locale?: string; children: ReactNode }) {
  const value = useMemo<I18n>(() => {
    const messages = catalogues[locale] ?? ru;
    return { locale, t: (key, params) => translate(messages, locale, key, params) };
  }, [locale]);

  useEffect(() => {
    document.documentElement.lang = locale;
  }, [locale]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}
