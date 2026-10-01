import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import type { AdminAttempt, AdminSignal, AdminUser } from '../../api';
import { ru } from '../../i18n';
import { renderApp } from '../../test/render';
import { jsonResponse, pageOf, problemResponse } from '../../test/responses';

const learner = { id: 'u-1', username: 'ann', email: 'ann@example.test' };

const openSignal: AdminSignal = {
  id: 's-1',
  attempt: 'a-1',
  task: 'flower-shop-daily-revenue',
  card: 'money-in-float',
  cardName: 'Деньги не в десятичном типе',
  learner,
  comment: 'Сумма в float, строка 12.',
  createdAt: '2026-10-01T09:30:00Z',
  resolvedAt: null,
};

const users: AdminUser[] = [
  { id: 'u-1', email: 'ann@example.test', username: 'ann', providers: ['github', 'google'], registeredAt: '2026-09-30T10:00:00Z', attempts: 3, tasksSolved: 2 },
  { id: 'u-2', email: 'bob@example.test', username: 'bob', providers: [], registeredAt: '2026-09-29T10:00:00Z', attempts: 0, tasksSolved: 0 },
];

const attempts: AdminAttempt[] = [
  {
    id: 'a-3',
    task: 'flower-shop-daily-revenue',
    learner,
    startedAt: '2026-10-01T10:00:00Z',
    step: 'treatment',
    submittedAt: null,
    practice: false,
    timeTakenSeconds: null,
    score: null,
    maxScore: null,
  },
  {
    id: 'a-2',
    task: 'flower-shop-daily-revenue',
    learner,
    startedAt: '2026-10-01T09:00:00Z',
    step: 'treatment',
    submittedAt: '2026-10-01T09:04:05Z',
    practice: true,
    timeTakenSeconds: 245,
    score: 7,
    maxScore: 30,
  },
  {
    id: 'a-1',
    task: 'flower-shop-daily-revenue',
    learner: { id: 'u-gone', username: null, email: null },
    startedAt: '2026-10-01T08:00:00Z',
    step: 'treatment',
    submittedAt: '2026-10-01T08:10:00Z',
    practice: false,
    timeTakenSeconds: 600,
    score: 30,
    maxScore: 30,
  },
];

function urlOf(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }
  return input instanceof URL ? input.href : input.url;
}

/** The API as an admin, a signed-in non-admin or a visitor sees it; admin lists answer only the first. */
function api(caller: 'admin' | 'learner' | 'visitor', { resolveFails = false } = {}) {
  let resolved = false;

  return vi.fn<typeof globalThis.fetch>().mockImplementation((input, init) => {
    const url = new URL(urlOf(input));

    if (url.pathname.endsWith('/me')) {
      return Promise.resolve(
        caller === 'visitor' ? problemResponse(401, 'unauthenticated', 'x') : jsonResponse({ id: 'me', username: 'developer', admin: caller === 'admin' }),
      );
    }

    if (caller !== 'admin') {
      return Promise.resolve(problemResponse(404, 'not_found', 'Nothing is served at this address.'));
    }

    if (url.pathname.endsWith('/admin/signals/s-1/resolve') && init?.method === 'POST') {
      if (resolveFails) {
        return Promise.resolve(problemResponse(500, 'internal_error', 'x'));
      }
      resolved = true;
      return Promise.resolve(jsonResponse({ ...openSignal, resolvedAt: '2026-10-01T11:00:00Z' }));
    }

    if (url.pathname.endsWith('/admin/signals')) {
      const status = url.searchParams.get('status');
      const open = resolved ? [] : [openSignal];
      const closed = resolved ? [{ ...openSignal, resolvedAt: '2026-10-01T11:00:00Z' }] : [];
      return Promise.resolve(jsonResponse(pageOf(status === 'resolved' ? closed : open, 1, 50)));
    }

    if (url.pathname.endsWith('/admin/users')) {
      return Promise.resolve(jsonResponse(pageOf(users, 1, 50)));
    }

    if (url.pathname.endsWith('/admin/attempts')) {
      const status = url.searchParams.get('status');
      const shown = attempts.filter(
        (attempt) =>
          (status !== 'open' || attempt.submittedAt === null)
          && (url.searchParams.get('user') === null || attempt.learner.id === url.searchParams.get('user')),
      );
      return Promise.resolve(jsonResponse(pageOf(shown, 1, 50)));
    }

    return Promise.resolve(problemResponse(404, 'not_found', 'x'));
  });
}

function requested(fetchStub: ReturnType<typeof api>, path: string): URL[] {
  return fetchStub.mock.calls.map(([input]) => new URL(urlOf(input))).filter((url) => url.pathname.endsWith(path));
}

describe('the admin area', () => {
  it('is offered in the header to an admin only', async () => {
    renderApp(api('admin'), '/tasks');
    expect(await screen.findByRole('link', { name: ru.nav.admin })).toHaveAttribute('href', '/admin');
  });

  it.each(['learner', 'visitor'] as const)('shows a %s the page of an unknown address, and asks the API nothing', async (caller) => {
    const fetchStub = api(caller);
    renderApp(fetchStub, '/admin/signals');

    expect(await screen.findByRole('heading', { name: ru.notFound.title })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: ru.nav.admin })).not.toBeInTheDocument();
    expect(requested(fetchStub, '/admin/signals')).toHaveLength(0);
  });

  it('opens on the signals', async () => {
    renderApp(api('admin'), '/admin');
    expect(await screen.findByRole('heading', { name: ru.admin.signals, level: 1 })).toBeInTheDocument();
  });
});

describe('the signals page', () => {
  it('lists the open signals with task, card, learner and comment, and resolves one', async () => {
    const fetchStub = api('admin');
    renderApp(fetchStub, '/admin/signals');

    const table = within(await screen.findByRole('table'));
    expect(table.getByRole('link', { name: 'flower-shop-daily-revenue' })).toHaveAttribute('href', '/tasks/flower-shop-daily-revenue');
    expect(table.getByRole('link', { name: 'Деньги не в десятичном типе' })).toHaveAttribute('href', '/problems#money-in-float');
    expect(table.getByText('ann@example.test')).toBeInTheDocument();
    expect(table.getByText('Сумма в float, строка 12.')).toBeInTheDocument();
    expect(requested(fetchStub, '/admin/signals')[0]?.searchParams.get('status')).toBe('open');

    fireEvent.click(table.getByRole('button', { name: ru.admin.signalsPage.resolve }));

    // Resolved, the list is read again and the signal has left it.
    expect(await screen.findByText(ru.admin.signalsPage.empty)).toBeInTheDocument();
    expect(fetchStub.mock.calls.some(([input, init]) => urlOf(input).endsWith('/admin/signals/s-1/resolve') && init?.method === 'POST')).toBe(true);
  });

  it('says so when resolving fails, and keeps the signal', async () => {
    renderApp(api('admin', { resolveFails: true }), '/admin/signals');

    fireEvent.click(await screen.findByRole('button', { name: ru.admin.signalsPage.resolve }));

    expect(await screen.findByRole('alert')).toHaveTextContent(ru.admin.signalsPage.resolveFailed);
    expect(screen.getByRole('button', { name: ru.admin.signalsPage.resolve })).toBeEnabled();
  });

  it('shows the resolved ones when asked', async () => {
    const fetchStub = api('admin');
    renderApp(fetchStub, '/admin/signals');

    fireEvent.change(await screen.findByRole('combobox', { name: ru.admin.signalsPage.status }), { target: { value: 'resolved' } });

    await waitFor(() => {
      expect(requested(fetchStub, '/admin/signals').at(-1)?.searchParams.get('status')).toBe('resolved');
    });
    expect(await screen.findByText(ru.admin.signalsPage.empty)).toBeInTheDocument();
  });
});

describe('the users page', () => {
  it('lists every user with providers and counts, linking to their attempts', async () => {
    renderApp(api('admin'), '/admin/users');

    const table = within(await screen.findByRole('table'));
    const [ann, bob] = table.getAllByRole('row').slice(1) as [HTMLElement, HTMLElement];

    expect(ann).toHaveTextContent('ann@example.test');
    expect(ann).toHaveTextContent('github, google');
    expect(within(ann).getByRole('link', { name: `${ru.admin.usersPage.showAttempts}: ann@example.test` })).toHaveAttribute('href', '/admin/attempts?user=u-1');
    expect(bob).toHaveTextContent(ru.admin.usersPage.noProvider);
    expect(within(bob).queryByRole('link')).not.toBeInTheDocument();
  });
});

describe('the attempts page', () => {
  it('shows where an abandoned attempt stopped, and a submitted one as first or practice with its time and score', async () => {
    renderApp(api('admin'), '/admin/attempts');

    const rows = within(await screen.findByRole('table')).getAllByRole('row').slice(1);

    expect(rows[0]).toHaveTextContent(ru.admin.attemptsPage.notSubmitted);
    expect(rows[0]).toHaveTextContent(ru.admin.attemptsPage.stepName.treatment);
    expect(rows[1]).toHaveTextContent(ru.admin.attemptsPage.practice);
    expect(rows[1]).toHaveTextContent('4 мин 5 с');
    expect(rows[1]).toHaveTextContent('7 из 30');
    expect(rows[2]).toHaveTextContent(ru.admin.attemptsPage.first);
    expect(rows[2]).toHaveTextContent(ru.admin.unknownUser);
  });

  it("narrows to one learner from the address, and can show everyone's again", async () => {
    const fetchStub = api('admin');
    renderApp(fetchStub, '/admin/attempts?user=u-1&status=open');

    expect(await screen.findByText(ru.admin.attemptsPage.forLearner.replace('{learner}', 'ann@example.test'))).toBeInTheDocument();
    const asked = requested(fetchStub, '/admin/attempts')[0];
    expect(asked?.searchParams.get('user')).toBe('u-1');
    expect(asked?.searchParams.get('status')).toBe('open');
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(2);

    fireEvent.click(screen.getByRole('link', { name: ru.admin.attemptsPage.allLearners }));

    await waitFor(() => {
      expect(requested(fetchStub, '/admin/attempts').at(-1)?.searchParams.get('user')).toBeNull();
    });
    expect(requested(fetchStub, '/admin/attempts').at(-1)?.searchParams.get('status')).toBe('open');
  });
});
