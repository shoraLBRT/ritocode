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

    await screen.findByRole('heading', { level: 3, name: 'Секреты в репозитории' });
    const classes = screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent);

    expect(classes).toEqual(['Гигиена и безопасность', 'Предметная область']);
  });

  it('shows a folded card as its class, name and summary, its sections folded in the page', async () => {
    renderApp(api(), '/problems');

    await screen.findByRole('heading', { level: 3, name: 'Деньги во float' });
    const money = byId('money-in-float');

    expect(money).toHaveClass('rc-pcard', 'k-domain');
    expect(money).toHaveTextContent('Предметная область');
    expect(within(money).getByText('Суммы считаются в числах с плавающей точкой.')).toBeInTheDocument();
    // Without JavaScript the sections unfold in place, so they are in the page, in a closed <details>.
    const details = money.querySelector('details');
    expect(details).not.toBeNull();
    expect(details?.open).toBe(false);
    expect(details).toHaveTextContent('Копейки расходятся с бухгалтерией.');
  });

  it('opens a card at the centre with every section, and closes it with Esc, back to the card', async () => {
    renderApp(api(), '/problems');

    const open = await screen.findByLabelText(ru.problems.openCard.replace('{name}', 'Деньги во float'));
    fireEvent.click(open);

    const dialog = screen.getByRole('dialog', { name: 'Деньги во float' });
    expect(dialog).toHaveClass('rc-pcard--open', 'k-domain');
    expect(within(dialog).getByRole('heading', { level: 4, name: ru.problems.sections.acceptableWhen })).toBeInTheDocument();
    expect(within(dialog).getByText('Никогда.')).toBeInTheDocument();
    expect(within(dialog).getAllByRole('listitem')).toHaveLength(2);
    expect(within(dialog).getByText('float').tagName).toBe('CODE');
    // A section the card leaves empty has no heading.
    expect(within(dialog).queryByRole('heading', { level: 4, name: ru.problems.sections.detection })).not.toBeInTheDocument();
    // The details did not unfold in place; the card's place stays as an outline; the address names it.
    expect(byId('money-in-float').querySelector('details')?.open).toBe(false);
    expect(byId('money-in-float')).toHaveClass('rc-pcard--lifted');
    expect(window.location.hash).toBe('#money-in-float');
    expect(within(dialog).getByRole('button', { name: ru.problems.close })).toHaveFocus();

    fireEvent.keyDown(dialog, { key: 'Escape' });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(window.location.hash).toBe('');
    expect(open).toHaveFocus();
  });

  it('closes an open card with its close button or a click outside it', async () => {
    renderApp(api(), '/problems');

    fireEvent.click(await screen.findByLabelText(ru.problems.openCard.replace('{name}', 'Секреты в репозитории')));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: ru.problems.close }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText(ru.problems.openCard.replace('{name}', 'Секреты в репозитории')));
    // The backdrop is the other close control; it is outside the dialog.
    const backdrop = screen.getAllByRole('button', { name: ru.problems.close }).find((button) => !screen.getByRole('dialog').contains(button));
    if (backdrop === undefined) {
      throw new Error('The open card has no backdrop to click.');
    }
    fireEvent.click(backdrop);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('filters by class, counting what each class would show', async () => {
    renderApp(api(), '/problems');

    const filter = within(await screen.findByRole('group', { name: ru.problems.classFilter }));
    expect(filter.getByRole('button', { name: `${ru.problems.allClasses} 2` })).toHaveAttribute('aria-pressed', 'true');
    expect(filter.getByRole('button', { name: 'Разрастание 0' })).toBeInTheDocument();

    fireEvent.click(filter.getByRole('button', { name: 'Предметная область 1' }));
    expect(document.getElementById('money-in-float')).not.toBeNull();
    expect(document.getElementById('secrets-in-repo')).toBeNull();
    expect(screen.getByText('1 карточка')).toBeInTheDocument();

    // The pressed class again shows them all.
    fireEvent.click(filter.getByRole('button', { name: 'Предметная область 1' }));
    expect(document.getElementById('secrets-in-repo')).not.toBeNull();
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

  it('gives every card an anchor, and opens the card the address names', async () => {
    const scrolled: string[] = [];
    vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(function (this: Element) {
      scrolled.push(this.id);
    });

    renderApp(api(), '/problems#money-in-float');

    await waitFor(() => {
      expect(scrolled).toEqual(['money-in-float']);
    });
    expect(screen.getByRole('dialog', { name: 'Деньги во float' })).toBeInTheDocument();
    // The folded card's seal is its anchor, and the open card carries the same link in its band.
    expect(within(byId('money-in-float')).getByRole('link', { name: 'Ссылка на карточку «Деньги во float»' })).toHaveAttribute('href', '#money-in-float');
    expect(within(screen.getByRole('dialog')).getByRole('link', { name: 'Ссылка на карточку «Деньги во float»' })).toHaveAttribute('href', '#money-in-float');
  });
});
