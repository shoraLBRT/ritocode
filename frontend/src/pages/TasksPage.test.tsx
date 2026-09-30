import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, within } from '@testing-library/react';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse, pageOf, problemResponse } from '../test/responses';

const tasks = [
  { slug: 'flower-shop', title: 'Выручка цветочного магазина', difficulty: 'easy', solved: true },
  { slug: 'invoice-mailer', title: 'Счёт клиенту по почте', difficulty: 'easy', solved: false },
  { slug: 'billing-core', title: 'Ядро биллинга', difficulty: 'hard', solved: false },
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

async function list() {
  return within(await screen.findByRole('list'));
}

describe('the task catalogue', () => {
  it('lists every task with its difficulty and the time it takes, linking to the task', async () => {
    const stub = api(tasks);
    renderApp(stub, '/tasks');

    const items = (await list()).getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(items[2]).toHaveTextContent(ru.tasks.difficulty.hard);
    expect(items[2]).toHaveTextContent(ru.tasks.time.hard);
    expect(screen.getByRole('link', { name: 'Выручка цветочного магазина' })).toHaveAttribute('href', '/tasks/flower-shop');

    // The whole catalogue in one request.
    expect(stub.mock.calls.some(([input]) => urlOf(input).includes('/tasks?pageSize=100'))).toBe(true);
  });

  it('marks the tasks a signed-in learner solved, and filters by it', async () => {
    renderApp(api(tasks), '/tasks');

    const solvedBadges = (await list()).getAllByText(ru.tasks.solved);
    expect(solvedBadges).toHaveLength(1);

    fireEvent.change(screen.getByLabelText(ru.tasks.solvedFilter), { target: { value: 'unsolved' } });

    expect((await list()).getAllByRole('listitem').map((item) => item.textContent)).not.toContain(
      expect.stringContaining('Выручка цветочного магазина'),
    );
    expect(screen.getByText('2 задачи')).toBeInTheDocument();
  });

  it('filters by difficulty, and says when nothing is left', async () => {
    renderApp(api(tasks), '/tasks');
    await list();

    fireEvent.change(screen.getByLabelText(ru.tasks.difficultyFilter), { target: { value: 'hard' } });
    expect((await list()).getAllByRole('listitem')).toHaveLength(1);
    expect(screen.getByText('1 задача')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(ru.tasks.difficultyFilter), { target: { value: 'medium' } });
    expect(await screen.findByText(ru.tasks.noMatch)).toBeInTheDocument();
  });

  it('offers no solved filter to a signed-out visitor, whom the API tells nothing about it', async () => {
    renderApp(api(tasks.map((task) => ({ ...task, solved: null })), { signedIn: false }), '/tasks');

    expect((await list()).getAllByRole('listitem')).toHaveLength(3);
    expect(screen.queryByLabelText(ru.tasks.solvedFilter)).not.toBeInTheDocument();
    expect(screen.queryByText(ru.tasks.solved)).not.toBeInTheDocument();
  });

  it('says so when there are no tasks yet', async () => {
    renderApp(api([]), '/tasks');

    expect(await screen.findByText(ru.tasks.empty)).toBeInTheDocument();
  });
});
