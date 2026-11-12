import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, within } from '@testing-library/react';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse, pageOf, problemResponse } from '../test/responses';

const tasks = [
  { slug: 'flower-shop', title: 'Выручка цветочного магазина', difficulty: 'easy', status: 'solved' },
  { slug: 'invoice-mailer', title: 'Счёт клиенту по почте', difficulty: 'easy', status: 'tried' },
  { slug: 'billing-core', title: 'Ядро биллинга', difficulty: 'hard', status: 'gaveUp' },
];

function urlOf(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }
  return input instanceof URL ? input.href : input.url;
}

function api(items: object[], { signedIn = true } = {}) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = urlOf(input);

    if (url.includes('/me')) {
      return Promise.resolve(signedIn ? jsonResponse({ id: 'u', username: 'developer' }) : problemResponse(401, 'unauthenticated', 'x'));
    }

    return Promise.resolve(jsonResponse(pageOf(items, 1, 100)));
  });
}

/** The table's body rows, once the tasks have arrived. */
async function rows() {
  const table = await screen.findByRole('table');
  return within(table).getAllByRole('row').slice(1);
}

function tab(group: string, name: RegExp) {
  return within(screen.getByRole('group', { name: group })).getByRole('button', { name });
}

describe('the task catalogue', () => {
  it('lists every task with its difficulty and the time it takes, linking to the task', async () => {
    const stub = api(tasks);
    renderApp(stub, '/tasks');

    const body = await rows();
    expect(body).toHaveLength(3);
    expect(body[2]).toHaveTextContent(ru.tasks.difficulty.hard);
    expect(body[2]).toHaveTextContent(ru.tasks.time.hard);
    expect(screen.getByRole('link', { name: 'Выручка цветочного магазина' })).toHaveAttribute('href', '/tasks/flower-shop');

    // The whole catalogue in one request.
    expect(stub.mock.calls.some(([input]) => urlOf(input).includes('/tasks?pageSize=100'))).toBe(true);
  });

  it('marks each task with the signed-in learner’s status in words, and filters by the four statuses', async () => {
    renderApp(api([...tasks, { slug: 'orders', title: 'Выгрузка заказов', difficulty: 'medium', status: 'new' }]), '/tasks');

    expect(await screen.findByRole('columnheader', { name: ru.taskTable.status })).toBeInTheDocument();
    for (const status of ['new', 'tried', 'solved', 'gaveUp'] as const) {
      expect(screen.getByRole('img', { name: ru.taskTable.statuses[status] })).toBeInTheDocument();
      expect(tab(ru.tasks.statusFilter, new RegExp(ru.tasks.statuses[status]))).toHaveTextContent('1');
    }

    fireEvent.click(tab(ru.tasks.statusFilter, new RegExp(ru.tasks.statuses.gaveUp)));

    expect(await rows()).toHaveLength(1);
    expect(screen.getByRole('link', { name: 'Ядро биллинга' })).toBeInTheDocument();
    expect(screen.getByText('1 задача')).toBeInTheDocument();
  });

  it('filters by difficulty, counting each tab, and says when nothing is left', async () => {
    renderApp(api(tasks), '/tasks');
    await rows();

    expect(tab(ru.tasks.difficultyFilter, /Лёгкая/)).toHaveTextContent('2');
    expect(tab(ru.tasks.difficultyFilter, /Средняя/)).toHaveTextContent('0');

    fireEvent.click(tab(ru.tasks.difficultyFilter, /Сложная/));
    expect(await rows()).toHaveLength(1);
    expect(screen.getByText('1 задача')).toBeInTheDocument();

    fireEvent.click(tab(ru.tasks.difficultyFilter, /Средняя/));
    expect(await screen.findByText(ru.tasks.noMatch)).toBeInTheDocument();
  });

  it('opens with the filters the address names, ignoring values it does not know', async () => {
    renderApp(api(tasks), '/tasks?difficulty=easy&status=solved');

    expect(await rows()).toHaveLength(1);
    expect(tab(ru.tasks.difficultyFilter, /Лёгкая/)).toHaveAttribute('aria-pressed', 'true');
    expect(tab(ru.tasks.statusFilter, /Решённые/)).toHaveAttribute('aria-pressed', 'true');
  });

  it('shows every task when the address names a filter that does not exist', async () => {
    renderApp(api(tasks), '/tasks?difficulty=impossible&status=maybe');

    expect(await rows()).toHaveLength(3);
    expect(tab(ru.tasks.difficultyFilter, new RegExp(`^${ru.tasks.all}`))).toHaveAttribute('aria-pressed', 'true');
  });

  it('offers no status to a signed-out visitor, whom the API tells nothing about it', async () => {
    renderApp(api(tasks.map((task) => ({ ...task, status: null })), { signedIn: false }), '/tasks?status=solved');

    expect(await rows()).toHaveLength(3);
    expect(screen.queryByRole('group', { name: ru.tasks.statusFilter })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: ru.taskTable.status })).not.toBeInTheDocument();
  });

  it('says so when there are no tasks yet', async () => {
    renderApp(api([]), '/tasks');

    expect(await screen.findByText(ru.tasks.empty)).toBeInTheDocument();
  });
});
