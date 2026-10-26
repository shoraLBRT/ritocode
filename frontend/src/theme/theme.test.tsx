import { afterEach, describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider, ru } from '../i18n';
import { applyTheme, readTheme, saveTheme } from './theme';
import { ThemeToggle } from './ThemeToggle';

afterEach(() => {
  localStorage.clear();
  delete document.documentElement.dataset.theme;
});

describe('the theme', () => {
  it('is light unless the dark one was chosen', () => {
    expect(readTheme(localStorage)).toBe('light');
    expect(readTheme(null)).toBe('light');

    saveTheme(localStorage, 'dark');
    expect(readTheme(localStorage)).toBe('dark');
  });

  it('switches from the header and is remembered', () => {
    applyTheme('light');
    render(
      <I18nProvider>
        <ThemeToggle />
      </I18nProvider>,
    );

    fireEvent.click(screen.getByRole('button', { name: ru.app.themeDark }));

    expect(document.documentElement.dataset.theme).toBe('dark');
    expect(readTheme(localStorage)).toBe('dark');
    expect(screen.getByRole('button', { name: ru.app.themeLight })).toBeInTheDocument();
  });
});
