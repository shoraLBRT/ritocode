import { describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse, pageOf, problemResponse } from '../test/responses';
import { task } from './task/fixtures';

function urlOf(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }
  return input instanceof URL ? input.href : input.url;
}

const catalogue = {
  classes: [
    { id: 'hygiene', name: 'Гигиена и безопасность', description: null },
    { id: 'domain', name: 'Предметная область', description: null },
  ],
  cards: [{ slug: 'secrets-in-repo' }, { slug: 'money-in-float' }, { slug: 'naive-datetime' }],
};

const progress = {
  tasks: 1,
  classes: [
    { class: 'hygiene', name: 'Гигиена и безопасность', met: 2, found: 1, treatedRight: 1 },
    { class: 'domain', name: 'Предметная область', met: 1, found: 1, treatedRight: 0 },
  ],
  cards: [],
};

function tasksFor(signedIn: boolean) {
  const solved = (value: boolean) => (signedIn ? value : null);
  return [
    { slug: 'demo-task', title: 'Выручка цветочного магазина', difficulty: 'easy', solved: solved(true) },
    { slug: 'orders', title: 'Выгрузка заказов', difficulty: 'easy', solved: solved(false) },
    { slug: 'webhook', title: 'Пополнение баланса', difficulty: 'medium', solved: solved(false) },
    { slug: 'invoices', title: 'Счета дизайн-студии', difficulty: 'hard', solved: solved(false) },
  ];
}

function api({ signedIn = false } = {}) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = new URL(urlOf(input));

    switch (url.pathname) {
      case '/api/v1/me':
        return Promise.resolve(signedIn ? jsonResponse({ id: 'u', username: 'developer' }) : problemResponse(401, 'unauthenticated', 'x'));
      case '/api/v1/tasks':
        return Promise.resolve(jsonResponse(pageOf(tasksFor(signedIn), 1, 100)));
      case '/api/v1/problems':
        return Promise.resolve(jsonResponse(catalogue));
      case '/api/v1/me/progress':
        return Promise.resolve(jsonResponse(progress));
      case '/api/v1/attempts':
        return Promise.resolve(jsonResponse(pageOf([], 1, 100)));
      case '/api/v1/tasks/demo-task':
        return Promise.resolve(jsonResponse({ ...task, slug: 'demo-task' }));
      default:
        return Promise.resolve(jsonResponse({}));
    }
  });
}

describe('the arena', () => {
  it('says what Ritocode teaches, with the real counts', async () => {
    renderApp(api(), '/');

    expect(screen.getByRole('heading', { level: 1, name: ru.home.title })).toBeInTheDocument();
    expect(screen.getByText(ru.home.lead)).toBeInTheDocument();

    const stats = within(screen.getByRole('group', { name: ru.home.stats }));
    expect(await stats.findByText('4')).toBeInTheDocument();
    expect(await stats.findByText('3')).toBeInTheDocument();
    expect(stats.getByText('2')).toBeInTheDocument();
  });

  it('links to the configured demo task, the task catalogue and the problem catalogue', () => {
    renderApp(api(), '/');
    const actions = within(screen.getByRole('navigation', { name: ru.home.actions }));

    expect(actions.getByRole('link', { name: ru.home.demo })).toHaveAttribute('href', '/tasks/demo-task');
    expect(actions.getByRole('link', { name: ru.home.tasks })).toHaveAttribute('href', '/tasks');
    expect(actions.getByRole('link', { name: ru.home.problems })).toHaveAttribute('href', '/problems');
  });

  it('shows a daily task the learner has not solved', async () => {
    renderApp(api({ signedIn: true }), '/');

    const daily = await screen.findByRole('article', { name: (name) => name !== '' });
    const title = within(daily).getByRole('heading', { level: 2 }).textContent;

    expect(['Выгрузка заказов', 'Пополнение баланса', 'Счета дизайн-студии']).toContain(title);
    expect(within(daily).getByRole('link', { name: ru.home.daily.start })).toBeInTheDocument();
  });

  it('lists the tasks without classes, and filters them by difficulty', async () => {
    renderApp(api(), '/');

    const table = await screen.findByRole('table');
    expect(within(table).getAllByRole('row')).toHaveLength(5);
    expect(within(table).queryByRole('columnheader', { name: ru.home.table.status })).not.toBeInTheDocument();

    within(screen.getByRole('group', { name: ru.home.table.filter })).getByRole('button', { name: /Сложная/ }).click();

    expect(await within(table).findByRole('link', { name: 'Счета дизайн-студии' })).toBeInTheDocument();
    expect(within(table).getAllByRole('row')).toHaveLength(2);
  });

  it('asks a guest to sign in instead of showing progress', async () => {
    renderApp(api(), '/');

    expect(await screen.findByRole('heading', { level: 2, name: ru.home.guest.title })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 2, name: ru.home.progress.title })).not.toBeInTheDocument();
  });

  it('shows a signed-in learner what is solved, their progress by class and their activity', async () => {
    renderApp(api({ signedIn: true }), '/');

    expect(await screen.findByRole('columnheader', { name: ru.home.table.status })).toBeInTheDocument();
    expect(await screen.findByRole('img', { name: ru.home.table.solved })).toBeInTheDocument();
    expect(await screen.findByText(ru.home.progress.note)).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Найдено 1 из 2' })).toBeInTheDocument();
    expect(await screen.findByText(ru.home.activity.noStreak)).toBeInTheDocument();
  });

  it('opens the demo task for a visitor who is not signed in', async () => {
    renderApp(api(), '/');

    screen.getByRole('link', { name: ru.home.demo }).click();

    expect(await screen.findByRole('heading', { level: 1, name: task.title })).toBeInTheDocument();
    expect(await screen.findByText(ru.session.signedOut)).toBeInTheDocument();
  });
});
