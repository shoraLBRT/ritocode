import { createContext, useContext } from 'react';
import { ru } from './ru';
import { translate } from './translate';
import type { Translate } from './translate';

export interface I18n {
  /** The locale being shown, a BCP 47 tag; what `Intl` formatters are given. */
  readonly locale: string;
  readonly t: Translate;
}

/** The default: Russian, so a component rendered without a provider still reads its text. */
export const I18nContext = createContext<I18n>({
  locale: 'ru',
  t: (key, params) => translate(ru, 'ru', key, params),
});

/** The translate function for the current locale. Every user-visible string goes through it. */
export function useT(): Translate {
  return useContext(I18nContext).t;
}

/** The current locale, for `Intl` date and number formatting (docs/SPEC.md §3.6). */
export function useLocale(): string {
  return useContext(I18nContext).locale;
}
