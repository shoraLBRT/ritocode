import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse } from '../test/responses';

const sections = {
  signs: '- Цены объявлены как `float`.\n- Итог округляется в конце.',
  whyAiDoesIt: null,
  cost: 'Копейки расходятся с бухгалтерией.',
  acceptableWhen: 'Никогда.',
  detection: null,
  treatment: 'Хранить в `Decimal`.',
  sources: null,
  counterArguments: null,
};

const catalogue = {
  classes: [
    { id: 'hygiene', name: 'Гигиена и безопасность', description: 'То, что должно быть закрыто оградой.' },
    { id: 'growth', name: 'Разрастание', description: null },
    { id: 'domain', name: 'Предметная область', description: null },
  ],
  cards: [
    {
      slug: 'secrets-in-repo',
      class: 'hygiene',
      name: 'Секреты в репозитории',
      summary: 'Пароли и токены прямо в коде.',
      keywords: ['пароль', 'api key'],
      sections,
    },
    {
      slug: 'money-in-float',
      class: 'domain',
      name: 'Деньги во float',
      summary: 'Суммы считаются в числах с плавающей точкой.',
      keywords: ['копейки', 'decimal'],
      sections,
    },
  ],
};

function byId(id: string): HTMLElement {
  const element = document.getElementById(id);

  if (element === null) {
    throw new Error(`Nothing on the page has the id '${id}'.`);
  }

  return element;
}

function api(body: object = catalogue) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    return Promise.resolve(url.endsWith('/me') ? jsonResponse({ id: 'u', username: 'developer' }) : jsonResponse(body));
  });
}

describe('the problem catalogue', () => {
  it('groups the cards by class, in the classes order, and leaves out classes with none', async () => {
    renderApp(api(), '/problems');

    await screen.findByRole('heading', { level: 3, name: /Секреты в репозитории/ });
    const classes = screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent);

    expect(classes).toEqual(['Гигиена и безопасность', 'Предметная область']);
  });

  it('shows every card in full, its sections rendered from Markdown', async () => {
    renderApp(api(), '/problems');

    await screen.findByRole('heading', { level: 3, name: /Деньги во float/ });
    const money = within(byId('money-in-float'));

    expect(money.getByText('Суммы считаются в числах с плавающей точкой.')).toBeInTheDocument();
    expect(money.getByRole('heading', { level: 4, name: ru.problems.sections.acceptableWhen })).toBeInTheDocument();
    expect(money.getByText('Никогда.')).toBeInTheDocument();
    expect(money.getAllByRole('listitem')).toHaveLength(2);
    expect(money.getByText('float').tagName).toBe('CODE');

    // A section the card leaves empty has no heading.
    expect(money.queryByRole('heading', { level: 4, name: ru.problems.sections.detection })).not.toBeInTheDocument();
  });

  it('searches names, summaries and keywords, ignoring case and ё', async () => {
    const umami = { track: vi.fn() };
    window.umami = umami;
    renderApp(api(), '/problems');
    const search = await screen.findByLabelText(ru.problems.search);

    fireEvent.change(search, { target: { value: 'КОПЕЙКИ' } });
    expect(document.getElementById('money-in-float')).not.toBeNull();
    expect(document.getElementById('secrets-in-repo')).toBeNull();
    expect(screen.getByText('1 карточка')).toBeInTheDocument();

    fireEvent.change(search, { target: { value: 'API KEY' } });
    expect(document.getElementById('secrets-in-repo')).not.toBeNull();

    fireEvent.change(search, { target: { value: 'плавающеи точкои' } });
    expect(document.getElementById('money-in-float')).toBeNull();

    fireEvent.change(search, { target: { value: 'ничего такого нет' } });
    expect(screen.getByText(ru.problems.noMatch)).toBeInTheDocument();

    // A search is counted once a visit, and what was typed is not sent.
    expect(umami.track.mock.calls).toEqual([['catalogue-search', undefined]]);
  });

  it('treats ё and е as the same letter', async () => {
    const withYo = { ...catalogue, cards: [{ ...catalogue.cards[0], summary: 'Всё лежит в коде.' }] };
    renderApp(api(withYo), '/problems');

    fireEvent.change(await screen.findByLabelText(ru.problems.search), { target: { value: 'все лежит' } });

    expect(document.getElementById('secrets-in-repo')).not.toBeNull();
  });

  it('gives every card an anchor, and opens at the card the address names', async () => {
    const scrolled: string[] = [];
    vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(function (this: Element) {
      scrolled.push(this.id);
    });

    renderApp(api(), '/problems#money-in-float');

    await waitFor(() => {
      expect(scrolled).toEqual(['money-in-float']);
    });
    expect(screen.getByRole('link', { name: 'Ссылка на карточку «Деньги во float»' })).toHaveAttribute('href', '#money-in-float');
  });
});
