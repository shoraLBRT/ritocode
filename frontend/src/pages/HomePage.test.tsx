import { describe, expect, it, vi } from 'vitest';
import { act, screen, within } from '@testing-library/react';
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
  cards: [
    { slug: 'secrets-in-repo', class: 'hygiene', name: 'Секреты в репозитории', summary: 'Пароль или токен прямо в коде.' },
    { slug: 'money-in-float', class: 'domain', name: 'Деньги во float', summary: 'Суммы в числах с плавающей точкой.' },
    { slug: 'naive-datetime', class: 'domain', name: 'Время без пояса', summary: 'Дата и время без часового пояса.' },
  ],
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
  const status = (value: string) => (signedIn ? value : null);
  return [
    { slug: 'demo-task', title: 'Выручка цветочного магазина', difficulty: 'easy', status: status('solved') },
    { slug: 'orders', title: 'Выгрузка заказов', difficulty: 'easy', status: status('tried') },
    { slug: 'webhook', title: 'Пополнение баланса', difficulty: 'medium', status: status('new') },
    { slug: 'invoices', title: 'Счета дизайн-студии', difficulty: 'hard', status: status('gaveUp') },
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

  it('shows a daily task still open to the learner: neither solved nor with its answer revealed', async () => {
    renderApp(api({ signedIn: true }), '/');

    const daily = await screen.findByRole('article', { name: (name) => name !== '' });
    const title = within(daily).getByRole('heading', { level: 2 }).textContent;

    expect(['Выгрузка заказов', 'Пополнение баланса']).toContain(title);
    expect(within(daily).getByRole('link', { name: ru.home.daily.start })).toBeInTheDocument();
  });

  it('counts down to Moscow midnight, then shows the task of the next day', async () => {
    // Only the interval and the clock are faked: the page's requests and testing-library's waiting
    // keep real timers. Two seconds before midnight in Moscow (21:00 UTC).
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    vi.setSystemTime(new Date('2026-11-04T20:59:58Z'));
    try {
      renderApp(api({ signedIn: true }), '/');

      const daily = await screen.findByRole('article', { name: (name) => name !== '' });
      const title = () => within(daily).getByRole('heading', { level: 2 }).textContent;
      const today = title();
      expect(within(daily).getByText('00:00:02')).toHaveAttribute('dateTime', 'PT00H00M02S');
      expect(daily).toHaveTextContent(`${ru.home.daily.countdown} 00:00:02`);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });
      expect(within(daily).getByText('00:00:01')).toBeInTheDocument();
      expect(title()).toBe(today);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });
      expect(within(daily).getByText('23:59:59')).toBeInTheDocument();
      expect(title()).not.toBe(today);
      expect(['Выгрузка заказов', 'Пополнение баланса', 'Счета дизайн-студии']).toContain(title());
    } finally {
      vi.useRealTimers();
    }
  });

  it('suggests tasks without classes or a difficulty filter, leaving out the daily task', async () => {
    renderApp(api(), '/');

    const panel = await screen.findByRole('region', { name: ru.home.table.title });
    const table = await within(panel).findByRole('table');
    const daily = await screen.findByRole('article', { name: (name) => name !== '' });
    const today = within(daily).getByRole('heading', { level: 2 }).textContent ?? '';

    // Four tasks, one of them today's: a header row and three suggestions.
    expect(within(table).getAllByRole('row')).toHaveLength(4);
    expect(within(table).queryByRole('link', { name: today })).not.toBeInTheDocument();
    expect(within(table).queryByRole('columnheader', { name: ru.taskTable.status })).not.toBeInTheDocument();
    expect(screen.queryByRole('group', { name: ru.tasks.difficultyFilter })).not.toBeInTheDocument();
    expect(within(panel).getByRole('link', { name: ru.home.table.more })).toHaveAttribute('href', '/tasks');
  });

  it('suggests problems to read, each opening at its card in the catalogue', async () => {
    renderApp(api(), '/');

    const panel = await screen.findByRole('region', { name: ru.home.reading.title });
    const card = await within(panel).findByRole('link', { name: /Деньги во float/ });

    expect(card).toHaveAttribute('href', '/problems#money-in-float');
    expect(card).toHaveTextContent('Предметная область');
    expect(card).toHaveTextContent('Суммы в числах с плавающей точкой.');
    expect(within(panel).getAllByRole('listitem')).toHaveLength(3);
    expect(within(panel).getByRole('link', { name: ru.home.reading.more })).toHaveAttribute('href', '/problems');
  });

  it('no longer explains how a task goes, and the lead asks for nothing more', () => {
    renderApp(api(), '/');

    expect(screen.queryByRole('heading', { name: 'Как устроена задача' })).not.toBeInTheDocument();
    expect(ru.home.lead).not.toContain('Писать ничего не нужно');
    expect(ru.home.lead).not.toContain('настоящих проектах');
    expect(ru.home.demo).toBe('Начать с лёгкой задачи');
  });

  it('asks a guest to sign in instead of showing progress', async () => {
    renderApp(api(), '/');

    expect(await screen.findByRole('heading', { level: 2, name: ru.home.guest.title })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 2, name: ru.home.progress.title })).not.toBeInTheDocument();
  });

  it('shows a signed-in learner what is solved, their progress by class and their activity', async () => {
    renderApp(api({ signedIn: true }), '/');

    expect(await screen.findByRole('columnheader', { name: ru.taskTable.status })).toBeInTheDocument();
    expect(await screen.findByRole('img', { name: ru.taskTable.statuses.solved })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: ru.taskTable.statuses.gaveUp })).toBeInTheDocument();
    expect(await screen.findByText(ru.home.progress.note)).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Найдено 1 из 2' })).toBeInTheDocument();
    expect(await screen.findByText(ru.home.activity.noStreak)).toBeInTheDocument();
  });

  it('opens the demo task for a visitor who is not signed in', async () => {
    renderApp(api(), '/');

    screen.getByRole('link', { name: ru.home.demo }).click();

    expect(await screen.findByRole('heading', { level: 1, name: task.title })).toBeInTheDocument();
    expect(await within(screen.getByRole('banner')).findByText(ru.session.signIn)).toBeInTheDocument();
  });
});
