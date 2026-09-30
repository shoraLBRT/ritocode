import type { ru } from './ru';

/**
 * The whole translation mechanism: a nested catalogue per locale, a key that is a dotted path into
 * it, `{name}` placeholders, and plural forms chosen by `Intl.PluralRules`.
 *
 * Written here rather than taken from a library. What the product needs — typed keys, Russian
 * plurals, interpolation, one locale now and a second one later without touching components — is
 * this file; i18next and FormatJS bring message formats, loaders and runtime catalogues the MVP
 * has no use for, and a key typed as a plain string, which is the mistake this type prevents.
 */

/** A string that varies with a count, by the CLDR plural categories Russian uses. */
export interface PluralForms {
  readonly one: string;
  readonly few: string;
  readonly many: string;
  readonly other: string;
}

type Leaf = string | PluralForms;

/** The shape every locale's catalogue has: the Russian one's, with its strings widened. */
export type Messages = Widen<typeof ru>;

type Widen<T> = {
  readonly [K in keyof T]: T[K] extends string ? string : T[K] extends PluralForms ? PluralForms : Widen<T[K]>;
};

/** Every key the catalogue has, as a dotted path: `'nav.home'`, `'session.signedInAs'`. */
export type MessageKey = Paths<typeof ru>;

type Paths<T> = {
  [K in keyof T & string]: T[K] extends Leaf ? K : `${K}.${Paths<T[K]>}`;
}[keyof T & string];

/** Values for a message's placeholders. `count` also picks a plural form. */
export type MessageParams = Readonly<Record<string, string | number>>;

export type Translate = (key: MessageKey, params?: MessageParams) => string;

export function translate(messages: Messages, locale: string, key: MessageKey, params: MessageParams = {}): string {
  const entry = lookup(messages, key);
  const template = typeof entry === 'string' ? entry : pluralForm(entry, locale, params);

  return template.replace(/\{(\w+)\}/g, (placeholder, name: string) => {
    const value = params[name];

    if (value === undefined) {
      return placeholder;
    }

    return typeof value === 'number' ? new Intl.NumberFormat(locale).format(value) : value;
  });
}

function lookup(messages: Messages, key: string): Leaf {
  let node: unknown = messages;

  for (const part of key.split('.')) {
    node = typeof node === 'object' && node !== null ? (node as Record<string, unknown>)[part] : undefined;
  }

  if (typeof node === 'string' || isPlural(node)) {
    return node;
  }

  throw new Error(`The catalogue has no message '${key}'.`);
}

function pluralForm(forms: PluralForms, locale: string, params: MessageParams): string {
  const count = params.count;

  if (typeof count !== 'number') {
    throw new Error('A message with plural forms needs a numeric `count`.');
  }

  const category = new Intl.PluralRules(locale).select(count);

  return category in forms ? forms[category as keyof PluralForms] : forms.other;
}

function isPlural(value: unknown): value is PluralForms {
  return typeof value === 'object' && value !== null && 'one' in value && 'other' in value;
}
