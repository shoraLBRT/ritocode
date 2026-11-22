import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse } from '../test/responses';

const sections = {
  signs: '- Цены объявлены как `float`.',
  whyAiDoesIt: null,
  cost: 'Копейки расходятся с бухгалтерией.',
  acceptableWhen: null,
  detection: null,
  treatment: 'Хранить в `Decimal`.',
  sources: null,
  counterArguments: null,
};

const catalogue = {
  classes: [
    { id: 'domain', name: 'Предметная область', description: null },
    { id: 'hygiene', name: 'Гигиена и безопасность', description: null },
  ],
  cards: [
    { slug: 'money-in-float', class: 'domain', name: 'Деньги во float', summary: 'Суммы считаются в **числах с плавающей точкой**.', keywords: [], sections },
    { slug: 'dates-as-strings', class: 'domain', name: 'Даты строками', summary: 'Дата хранится как текст.', keywords: [], sections },
    { slug: 'secrets-in-repo', class: 'hygiene', name: 'Секреты в репозитории', summary: 'Пароли в коде.', keywords: [], sections },
  ],
};

function api() {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    if (url.endsWith('/me/studied')) {
      return Promise.resolve(jsonResponse({ cards: [] }));
    }
    return Promise.resolve(url.endsWith('/me') ? jsonResponse({ code: 'unauthorized' }, 401) : jsonResponse(catalogue));
  });
}

describe('the page of a problem card', () => {
  it('shows the card in full under its class, and the other cards of the class', async () => {
    renderApp(api(), '/problems/money-in-float');

    expect(await screen.findByRole('heading', { level: 1, name: 'Деньги во float' })).toBeInTheDocument();
    const card = screen.getByRole('article', { name: 'Деньги во float' });
    expect(within(card).getAllByText('Предметная область').length).toBeGreaterThan(0);
    expect(within(card).getByText('Копейки расходятся с бухгалтерией.')).toBeInTheDocument();
    expect(within(card).getByRole('heading', { name: ru.problems.sections.treatment })).toBeInTheDocument();

    const related = screen.getByRole('region', { name: 'Ещё в классе «Предметная область»' });
    expect(within(related).getByRole('link', { name: 'Даты строками' })).toHaveAttribute('href', '/problems/dates-as-strings');
    expect(within(related).queryByRole('link', { name: 'Секреты в репозитории' })).toBeNull();
    // The header links the catalogue too; the crumb is one more.
    for (const link of screen.getAllByRole('link', { name: ru.problems.catalogue })) {
      expect(link).toHaveAttribute('href', '/problems');
    }
  });

  it('takes the card as the title and description of the document', async () => {
    renderApp(api(), '/problems/money-in-float');

    await screen.findByRole('heading', { level: 1, name: 'Деньги во float' });
    await waitFor(() => {
      expect(document.title).toBe('Деньги во float — проблема кода от ИИ · Ritocode');
    });
    expect(document.head.querySelector('meta[name="description"]')).toHaveAttribute('content', 'Суммы считаются в числах с плавающей точкой.');
  });

  it('is the page of an unknown address for a slug that names no card', async () => {
    renderApp(api(), '/problems/no-such-card');

    expect(await screen.findByRole('heading', { level: 1, name: ru.notFound.title })).toBeInTheDocument();
  });
});
