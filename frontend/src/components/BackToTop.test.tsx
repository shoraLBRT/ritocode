import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider, ru } from '../i18n';
import { BackToTop } from './BackToTop';

function scrollTo(y: number) {
  Object.defineProperty(window, 'scrollY', { value: y, configurable: true });
  act(() => {
    window.dispatchEvent(new Event('scroll'));
  });
}

function mount() {
  return render(
    <I18nProvider>
      <main id="main">
        <h1>Страница</h1>
        <BackToTop />
      </main>
    </I18nProvider>,
  );
}

afterEach(() => {
  Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
});

describe('BackToTop', () => {
  it('shows only after a screen of scroll', () => {
    mount();
    const button = screen.getByRole('button', { name: ru.app.backToTop, hidden: true });
    expect(button).not.toHaveAttribute('data-visible');

    scrollTo(window.innerHeight + 10);
    expect(button).toHaveAttribute('data-visible');

    scrollTo(0);
    expect(button).not.toHaveAttribute('data-visible');
  });

  it('scrolls to the top and moves focus to the heading', () => {
    const scroll = vi.fn();
    window.scrollTo = scroll;
    mount();
    scrollTo(window.innerHeight * 2);

    fireEvent.click(screen.getByRole('button', { name: ru.app.backToTop }));

    expect(scroll).toHaveBeenCalledWith(expect.objectContaining({ top: 0 }));
    expect(screen.getByRole('heading', { name: 'Страница' })).toHaveFocus();
  });
});
