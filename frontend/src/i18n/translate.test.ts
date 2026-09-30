import { describe, expect, it } from 'vitest';
import { ru } from './ru';
import { translate } from './translate';
import type { Messages } from './translate';

// A catalogue of the real shape with one plural entry grafted on, since the shell needs none yet.
const withPlural = {
  ...ru,
  state: { ...ru.state, loading: { one: '{count} карточка', few: '{count} карточки', many: '{count} карточек', other: '{count} карточки' } },
} as unknown as Messages;

describe('translate', () => {
  it('reads a message by its dotted key', () => {
    expect(translate(ru, 'ru', 'nav.home')).toBe(ru.nav.home);
  });

  it('fills placeholders', () => {
    expect(translate(ru, 'ru', 'session.signedInAs', { username: 'developer' })).toBe('Вы вошли как developer');
  });

  it('leaves a placeholder it was given no value for, so the gap is visible', () => {
    expect(translate(ru, 'ru', 'session.signedInAs')).toBe('Вы вошли как {username}');
  });

  it('formats a number by the locale', () => {
    // Russian groups thousands with a no-break space, which Intl knows and a literal would hide.
    const grouped = new Intl.NumberFormat('ru').format(12345);
    expect(grouped).not.toBe('12345');
    expect(translate(ru, 'ru', 'state.unexpectedStatus', { status: 12345 })).toBe(`API ответил неожиданным статусом (${grouped}).`);
  });

  it('picks the Russian plural form by the count', () => {
    expect(translate(withPlural, 'ru', 'state.loading', { count: 1 })).toBe('1 карточка');
    expect(translate(withPlural, 'ru', 'state.loading', { count: 3 })).toBe('3 карточки');
    expect(translate(withPlural, 'ru', 'state.loading', { count: 5 })).toBe('5 карточек');
    expect(translate(withPlural, 'ru', 'state.loading', { count: 21 })).toBe('21 карточка');
    expect(translate(withPlural, 'ru', 'state.loading', { count: 1.5 })).toBe('1,5 карточки');
  });

  it('refuses a plural message without a count', () => {
    expect(() => translate(withPlural, 'ru', 'state.loading')).toThrow(/count/);
  });
});
