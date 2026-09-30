import { describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse, problemResponse } from '../test/responses';

const classes = [
  { class: 'disproportion', name: 'Несоразмерность', met: 0, found: 0, treatedRight: 0 },
  { class: 'hygiene', name: 'Гигиена и безопасность', met: 3, found: 2, treatedRight: 2 },
  { class: 'domain', name: 'Предметная область', met: 1, found: 1, treatedRight: 0 },
];

const progress = {
  tasks: 2,
  classes,
  cards: [
    { card: 'hardcoded-config', name: 'Хардкод конфигурации', class: 'hygiene', met: 1, found: 1, missed: 0, pickedWhenAbsent: 1, treatedRight: 1 },
    { card: 'secrets-in-repo', name: 'Секреты в репозитории', class: 'hygiene', met: 1, found: 1, missed: 0, pickedWhenAbsent: 0, treatedRight: 1 },
    { card: 'swallowed-error', name: 'Проглоченная ошибка', class: 'hygiene', met: 1, found: 0, missed: 1, pickedWhenAbsent: 0, treatedRight: 0 },
    { card: 'money-in-float', name: 'Деньги не в десятичном типе', class: 'domain', met: 1, found: 1, missed: 0, pickedWhenAbsent: 0, treatedRight: 0 },
    { card: 'gone-card', name: 'gone-card', class: null, met: 1, found: 0, missed: 1, pickedWhenAbsent: 0, treatedRight: 0 },
  ],
};

const empty = { tasks: 0, classes, cards: [] };

function urlOf(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }
  return input instanceof URL ? input.href : input.url;
}

function api(body: object, { signedIn = true } = {}) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = urlOf(input);

    if (url.endsWith('/me/progress')) {
      return Promise.resolve(jsonResponse(body));
    }

    return Promise.resolve(signedIn ? jsonResponse({ id: 'u', username: 'developer' }) : problemResponse(401, 'unauthenticated', 'x'));
  });
}

describe('the progress page', () => {
  it('shows every class with its counts, and says which were not met yet', async () => {
    renderApp(api(progress), '/progress');

    expect(await screen.findByText('2 решённые задачи')).toBeInTheDocument();

    const items = within(screen.getByRole('list', { name: ru.progress.classes })).getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(items[0]).toHaveTextContent('Несоразмерность');
    expect(items[0]).toHaveTextContent(ru.progress.classNotMet);
    expect(items[1]).toHaveTextContent('Найдено 2 из 3 · лечение верно: 2');
    expect(items[2]).toHaveTextContent('Найдено 1 из 1 · лечение верно: 0');
  });

  it('tables the cards by class, in order, each linking to its card in the catalogue', async () => {
    renderApp(api(progress), '/progress');

    const table = within(await screen.findByRole('table'));
    const rows = table.getAllByRole('row').slice(1).map((row) => row.textContent);

    // A header row per class that has cards, then its cards; the unknown card last, in a group of its own.
    expect(rows).toEqual([
      'Гигиена и безопасность',
      'Хардкод конфигурации11011',
      'Секреты в репозитории11001',
      'Проглоченная ошибка10100',
      'Предметная область',
      'Деньги не в десятичном типе11000',
      ru.progress.noClass,
      'gone-card10100',
    ]);

    expect(table.getByRole('link', { name: 'Секреты в репозитории' })).toHaveAttribute('href', '/problems#secrets-in-repo');
    expect(table.queryByRole('link', { name: 'gone-card' })).not.toBeInTheDocument();
    expect(table.getByRole('columnheader', { name: ru.progress.pickedWhenAbsent })).toBeInTheDocument();
  });

  it('points a learner with no attempts to the task catalogue', async () => {
    renderApp(api(empty), '/progress');

    expect(await screen.findByText(ru.progress.empty)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: ru.progress.toTasks })).toHaveAttribute('href', '/tasks');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('is offered in the navigation to a signed-in learner', async () => {
    renderApp(api(empty), '/progress');

    const nav = within(await screen.findByRole('navigation', { name: ru.nav.label }));
    expect(await nav.findByRole('link', { name: ru.nav.progress })).toHaveAttribute('href', '/progress');
  });

  it('is closed to a signed-out visitor, who is not offered it and whose progress is never asked for', async () => {
    const stub = api(progress, { signedIn: false });
    renderApp(stub, '/progress');

    expect(await screen.findByText(ru.session.requiredText)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: ru.nav.progress })).not.toBeInTheDocument();
    expect(stub.mock.calls.some(([input]) => urlOf(input).endsWith('/me/progress'))).toBe(false);
  });
});
