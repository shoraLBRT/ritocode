import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import type { AdminAttempt, AdminSignal, AdminUser, CardStatsReport, SystemReport, TaskStatsReport } from '../../api';
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

const systemReport: SystemReport = {
  api: { version: '0.1.0', commit: 'ac1caf1b2c3d4e5f60718293a4b5c6d7e8f90123' },
  content: { revision: '9f8e7d6c5b4a', ingestedAt: '2026-11-19T08:00:00Z' },
  database: { status: 'ok', sizeBytes: 12_582_912 },
  migrations: [
    { module: 'attempts', status: 'upToDate', applied: 7, last: '20261101_AddStudied', pending: [] },
    { module: 'content', status: 'pending', applied: 3, last: '20261001_Initial', pending: ['20261119_AddRevision'] },
  ],
  dependencies: [
    { name: 'postgresql', status: 'ok' },
    { name: 'umami', status: 'failing' },
    { name: 'feedbackGitHubApp', status: 'notConfigured' },
  ],
  backup: null,
  checkedAt: '2026-11-19T09:00:00Z',
};

const band = { maxLines: 300, minutesFrom: 10, minutesTo: 15 };

const taskStats: TaskStatsReport = {
  tasks: [
    {
      task: 'flower-shop-daily-revenue',
      title: 'Выручка цветочного магазина',
      difficulty: 'medium',
      published: true,
      lines: 210,
      band,
      firstAttempts: 6,
      practiceAttempts: 2,
      learners: 6,
      solvedShare: 0.1,
      revealedShare: 0.5,
      time: { count: 6, medianSeconds: 1500, lowerQuartileSeconds: 900, upperQuartileSeconds: 1800 },
      open: { diagnosis: 2, treatment: 1 },
      meanScoreShare: 0.42,
      rarelySolved: true,
    },
    {
      task: 'invoice-mailer-monthly',
      title: 'Счёт клиенту по почте',
      difficulty: 'easy',
      published: true,
      lines: 60,
      band: { maxLines: 80, minutesFrom: 5, minutesTo: 5 },
      firstAttempts: 9,
      practiceAttempts: 0,
      learners: 9,
      solvedShare: 0.9,
      revealedShare: 0.1,
      time: { count: 9, medianSeconds: 240, lowerQuartileSeconds: 200, upperQuartileSeconds: 300 },
      open: { diagnosis: 0, treatment: 0 },
      meanScoreShare: 0.8,
      rarelySolved: false,
    },
  ],
};

const cardStats: CardStatsReport = {
  firstAttempts: 15,
  cards: [
    { card: 'money-in-float', name: 'Деньги не в десятичном типе', class: 'correctness', className: 'Корректность', met: 6, found: 2, missed: 4, pickedWhenAbsent: 5, treatedRight: 1, pickedMoreThanFound: true },
    { card: 'secrets-in-repo', name: 'Секреты в репозитории', class: 'hygiene', className: 'Гигиена', met: 9, found: 8, missed: 1, pickedWhenAbsent: 0, treatedRight: 7, pickedMoreThanFound: false },
  ],
};

/** The site's own `version.json`: the web image's commit, or a 404 as the development server answers. */
function stubVersionFile(commit: string | null) {
  vi.stubGlobal(
    'fetch',
    vi.fn<typeof globalThis.fetch>().mockResolvedValue(commit === null ? new Response('', { status: 404 }) : jsonResponse({ commit })),
  );
}

beforeEach(() => {
  stubVersionFile(null);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

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
        caller === 'visitor' ? problemResponse(401, 'unauthenticated', 'x') : jsonResponse({ id: 'me', username: 'developer', admin: caller === 'admin', createdAt: '2026-03-14T09:26:53+00:00', providers: [] }),
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

    if (url.pathname.endsWith('/admin/stats/tasks')) {
      return Promise.resolve(jsonResponse(taskStats));
    }

    if (url.pathname.endsWith('/admin/stats/cards')) {
      return Promise.resolve(jsonResponse(cardStats));
    }

    if (url.pathname.endsWith('/admin/system')) {
      return Promise.resolve(jsonResponse(systemReport));
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

  it.each(['/admin', '/admin/signals', '/admin/users', '/admin/attempts', '/admin/system'])('has one h1, the area name, at %s', async (path) => {
    renderApp(api('admin'), path);

    expect(await screen.findByRole('heading', { name: ru.admin.title, level: 1 })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });

  it('offers the six sections as links, the current one marked', async () => {
    renderApp(api('admin'), '/admin/users');

    const nav = within(await screen.findByRole('navigation', { name: ru.admin.nav }));
    expect(nav.getAllByRole('link').map((link) => link.getAttribute('href'))).toEqual(['/admin', '/admin/signals', '/admin/users', '/admin/attempts', '/admin/stats', '/admin/system']);
    expect(nav.getByRole('link', { name: ru.admin.users })).toHaveAttribute('aria-current', 'page');
    expect(nav.getByRole('link', { name: ru.admin.overview })).not.toHaveAttribute('aria-current');
  });

  it('shows the number of open signals on the signals tab', async () => {
    renderApp(api('admin'), '/admin/users');

    const tab = await screen.findByRole('link', { name: new RegExp(ru.admin.signals) });
    await waitFor(() => {
      expect(tab).toHaveTextContent(`${ru.admin.signals}1`);
    });
  });

  it('shows no number on the signals tab when it cannot be read', async () => {
    const base = api('admin');
    const failing = vi
      .fn<typeof globalThis.fetch>()
      .mockImplementation((input, init) => (urlOf(input).includes('/admin/signals') ? Promise.resolve(problemResponse(500, 'internal_error', 'x')) : base(input, init)));
    renderApp(failing, '/admin/users');

    expect(await screen.findByRole('table')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: ru.admin.signals })).toBeInTheDocument();
  });
});

describe('the overview', () => {
  /** The API with a count of its own on every list: the overview reads them as `totalItems`. */
  function counted() {
    const base = api('admin');
    return vi.fn<typeof globalThis.fetch>().mockImplementation((input, init) => {
      const url = new URL(urlOf(input));
      if (url.pathname.endsWith('/admin/signals') && url.searchParams.get('pageSize') === '5') {
        return Promise.resolve(jsonResponse(pageOf([openSignal], 1, 5, 7)));
      }
      if (url.pathname.endsWith('/admin/users') && url.searchParams.get('pageSize') === '1') {
        return Promise.resolve(jsonResponse(pageOf(users.slice(0, 1), 1, 1, 42)));
      }
      if (url.pathname.endsWith('/admin/attempts') && url.searchParams.get('pageSize') === '1') {
        return Promise.resolve(jsonResponse(pageOf(attempts.slice(0, 1), 1, 1, url.searchParams.get('status') === 'submitted' ? 12 : 3)));
      }
      return base(input, init);
    });
  }

  it('shows four counts, each a link to its list', async () => {
    renderApp(counted(), '/admin');

    const tiles = within(await screen.findByRole('list', { name: ru.admin.overviewPage.tiles }));
    const tile = (label: string, count: string) => tiles.findByRole('link', { name: new RegExp(`^${label}\\s*${count}`) });

    expect(await tile(ru.admin.overviewPage.signals, '7')).toHaveAttribute('href', '/admin/signals');
    expect(await tile(ru.admin.overviewPage.users, '42')).toHaveAttribute('href', '/admin/users');
    expect(await tile(ru.admin.overviewPage.submitted, '12')).toHaveAttribute('href', '/admin/attempts?status=submitted');
    expect(await tile(ru.admin.overviewPage.notSubmitted, '3')).toHaveAttribute('href', '/admin/attempts?status=open');
  });

  it('lists the newest open signals under the tiles, with a link to all of them', async () => {
    renderApp(counted(), '/admin');

    const table = within(await screen.findByRole('table'));
    expect(table.getByRole('link', { name: 'flower-shop-daily-revenue' })).toHaveAttribute('href', '/tasks/flower-shop-daily-revenue');
    expect(table.getByText('ann@example.test')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: ru.admin.overviewPage.allSignals })).toHaveAttribute('href', '/admin/signals');
  });

  it('says so calmly when there are no open signals', async () => {
    const base = api('admin');
    const none = vi
      .fn<typeof globalThis.fetch>()
      .mockImplementation((input, init) => (urlOf(input).includes('/admin/signals') ? Promise.resolve(jsonResponse(pageOf([], 1, 5))) : base(input, init)));
    renderApp(none, '/admin');

    expect(await screen.findByText(ru.admin.overviewPage.empty)).toBeInTheDocument();
  });

  /** A report of two UTC days with something on the second. */
  const activity = {
    from: '2026-11-18',
    to: '2026-11-19',
    bucket: 'day',
    buckets: [
      { start: '2026-11-18', end: '2026-11-18', activeLearners: 0, newUsers: 0, attemptsStarted: 0, attemptsSubmitted: 0, answersRevealed: 0, signalsSent: 0, cardsStudied: 0 },
      { start: '2026-11-19', end: '2026-11-19', activeLearners: 3, newUsers: 2, attemptsStarted: 5, attemptsSubmitted: 4, answersRevealed: 1, signalsSent: 1, cardsStudied: 6 },
    ],
    summary: { dailyActive: 3, weeklyActive: 4, monthlyActive: 9, learners: 4, returnedAfterWeek: 1 },
  };

  function withActivity() {
    const base = counted();
    return vi
      .fn<typeof globalThis.fetch>()
      .mockImplementation((input, init) => (urlOf(input).includes('/admin/stats/activity') ? Promise.resolve(jsonResponse(activity)) : base(input, init)));
  }

  it('charts the activity over 30 UTC days by default, and 90 by week', async () => {
    const fetchStub = withActivity();
    renderApp(fetchStub, '/admin');

    expect(await screen.findByText(ru.admin.activity.dau)).toBeInTheDocument();
    expect(screen.getByText('25%')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: `${ru.admin.activity.cardsStudied}: 0, 6` })).toBeInTheDocument();
    expect(screen.getByText(ru.admin.activity.noteDay)).toBeInTheDocument();

    const first = requested(fetchStub, '/admin/stats/activity')[0];
    expect(first?.searchParams.get('bucket')).toBe('day');
    const days = (Date.parse(first?.searchParams.get('to') ?? '') - Date.parse(first?.searchParams.get('from') ?? '')) / 86_400_000;
    expect(days).toBe(29);

    fireEvent.click(screen.getByRole('button', { name: '90 дн.' }));
    await waitFor(() => {
      expect(requested(fetchStub, '/admin/stats/activity').at(-1)?.searchParams.get('bucket')).toBe('week');
    });
  });

  it('keeps the tiles when the activity fails', async () => {
    renderApp(counted(), '/admin');

    expect(await screen.findByText(ru.admin.activity.failed)).toBeInTheDocument();
    expect(await screen.findByRole('list', { name: ru.admin.overviewPage.tiles })).toBeInTheDocument();
  });

  it('keeps the page when one count fails', async () => {
    const base = counted();
    const partial = vi
      .fn<typeof globalThis.fetch>()
      .mockImplementation((input, init) => (urlOf(input).includes('/admin/users') ? Promise.resolve(problemResponse(500, 'internal_error', 'x')) : base(input, init)));
    renderApp(partial, '/admin');

    const tiles = within(await screen.findByRole('list', { name: ru.admin.overviewPage.tiles }));
    expect(await tiles.findByText(ru.admin.overviewPage.tileFailed)).toBeInTheDocument();
    expect(await tiles.findByRole('link', { name: new RegExp(`^${ru.admin.overviewPage.signals}\\s*7`) })).toBeInTheDocument();
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

    // Resolved, the list is read again and the signal has left it; the tab's count follows.
    expect(await screen.findByText(ru.admin.signalsPage.empty)).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByRole('link', { name: new RegExp(ru.admin.signals) })).toHaveTextContent(`${ru.admin.signals}0`);
    });
    expect(fetchStub.mock.calls.some(([input, init]) => urlOf(input).endsWith('/admin/signals/s-1/resolve') && init?.method === 'POST')).toBe(true);
  });

  it('says so when resolving fails, and keeps the signal', async () => {
    renderApp(api('admin', { resolveFails: true }), '/admin/signals');

    fireEvent.click(await screen.findByRole('button', { name: ru.admin.signalsPage.resolve }));

    expect(await screen.findByRole('alert')).toHaveTextContent(ru.admin.signalsPage.resolveFailed);
    expect(screen.getByRole('button', { name: ru.admin.signalsPage.resolve })).toBeEnabled();
    expect(screen.getByText('Сумма в float, строка 12.')).toBeInTheDocument();
  });

  it('shows the resolved ones when asked', async () => {
    const fetchStub = api('admin');
    renderApp(fetchStub, '/admin/signals');

    const filter = within(await screen.findByRole('group', { name: ru.admin.signalsPage.status }));
    expect(filter.getByRole('button', { name: new RegExp(ru.admin.signalsPage.open) })).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(filter.getByRole('button', { name: ru.admin.signalsPage.resolved }));

    await waitFor(() => {
      expect(requested(fetchStub, '/admin/signals').at(-1)?.searchParams.get('status')).toBe('resolved');
    });
    expect(await screen.findByText(ru.admin.signalsPage.empty)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: ru.admin.signalsPage.resolved })).toHaveAttribute('aria-pressed', 'true');

    // Back to the default, the filter asks for the open ones again.
    fireEvent.click(screen.getByRole('button', { name: new RegExp(ru.admin.signalsPage.open) }));
    await waitFor(() => {
      expect(requested(fetchStub, '/admin/signals').at(-1)?.searchParams.get('status')).toBe('open');
    });
  });
});

describe('the users page', () => {
  it('lists every user with providers and counts, linking to their attempts', async () => {
    renderApp(api('admin'), '/admin/users');

    const table = within(await screen.findByRole('table'));
    const [ann, bob] = table.getAllByRole('row').slice(1) as [HTMLElement, HTMLElement];

    expect(ann).toHaveTextContent('ann@example.test');
    expect(within(ann).getByText('github')).toBeInTheDocument();
    expect(within(ann).getByText('google')).toBeInTheDocument();
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

  it('filters by what was submitted, in the address', async () => {
    const fetchStub = api('admin');
    renderApp(fetchStub, '/admin/attempts');

    const filter = within(await screen.findByRole('group', { name: ru.admin.attemptsPage.status }));
    expect(filter.getByRole('button', { name: ru.admin.attemptsPage.all })).toHaveAttribute('aria-pressed', 'true');

    for (const [label, status] of [
      [ru.admin.attemptsPage.open, 'open'],
      [ru.admin.attemptsPage.submitted, 'submitted'],
      [ru.admin.attemptsPage.all, 'all'],
    ] as const) {
      fireEvent.click(filter.getByRole('button', { name: label }));
      await waitFor(() => {
        expect(requested(fetchStub, '/admin/attempts').at(-1)?.searchParams.get('status')).toBe(status);
      });
      expect(filter.getByRole('button', { name: label })).toHaveAttribute('aria-pressed', 'true');
    }
  });

  it('narrows to one task or one card from the address, as the statistics link them', async () => {
    const fetchStub = api('admin');
    renderApp(fetchStub, '/admin/attempts?task=flower-shop-daily-revenue&card=money-in-float');

    expect(await screen.findByText(ru.admin.attemptsPage.forTask.replace('{task}', 'flower-shop-daily-revenue'))).toBeInTheDocument();
    expect(screen.getByText(ru.admin.attemptsPage.forCard.replace('{card}', 'money-in-float'))).toBeInTheDocument();
    const asked = requested(fetchStub, '/admin/attempts')[0];
    expect(asked?.searchParams.get('task')).toBe('flower-shop-daily-revenue');
    expect(asked?.searchParams.get('card')).toBe('money-in-float');

    fireEvent.click(screen.getByRole('link', { name: ru.admin.attemptsPage.allCards }));

    await waitFor(() => {
      expect(requested(fetchStub, '/admin/attempts').at(-1)?.searchParams.get('card')).toBeNull();
    });
    expect(requested(fetchStub, '/admin/attempts').at(-1)?.searchParams.get('task')).toBe('flower-shop-daily-revenue');
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

describe('the statistics page', () => {
  it('shows each task with its figures against its band, marks the rarely solved one, and links to its attempts', async () => {
    renderApp(api('admin'), '/admin/stats');

    const flower = await screen.findByRole('link', { name: 'Выручка цветочного магазина' });
    expect(flower).toHaveAttribute('href', '/admin/attempts?task=flower-shop-daily-revenue');

    const row = flower.closest('tr');
    expect(row).not.toBeNull();
    const cells = within(row as HTMLElement);
    expect(cells.getByText(ru.admin.statsPage.rarelySolved)).toBeInTheDocument();
    expect(cells.getByText('10%')).toBeInTheDocument();
    expect(cells.getByText('25:00')).toBeInTheDocument();
    expect(cells.getByText('15:00–30:00')).toBeInTheDocument();
    expect(cells.getByText('оценка 10–15 мин · 210/300 строк')).toBeInTheDocument();
    expect(cells.getByText('2 · 1')).toBeInTheDocument();
  });

  it('sorts a table by the heading pressed, and turns the order when it is pressed again', async () => {
    renderApp(api('admin'), '/admin/stats');

    const table = (await screen.findByRole('link', { name: 'Выручка цветочного магазина' })).closest('table') as HTMLElement;
    const titles = () => within(table).getAllByRole('link').map((link) => link.textContent);

    // By first attempts, most first.
    expect(titles()).toEqual(['Счёт клиенту по почте', 'Выручка цветочного магазина']);

    fireEvent.click(within(table).getByRole('button', { name: ru.admin.statsPage.time }));
    expect(titles()).toEqual(['Выручка цветочного магазина', 'Счёт клиенту по почте']);
    expect(within(table).getByRole('columnheader', { name: new RegExp(ru.admin.statsPage.time) })).toHaveAttribute('aria-sort', 'descending');

    fireEvent.click(within(table).getByRole('button', { name: ru.admin.statsPage.time }));
    expect(titles()).toEqual(['Счёт клиенту по почте', 'Выручка цветочного магазина']);
  });

  it('shows each card with its measures, marks one picked when absent more often than found, and links to its attempts', async () => {
    renderApp(api('admin'), '/admin/stats');

    const money = await screen.findByRole('link', { name: 'Деньги не в десятичном типе' });
    const table = money.closest('table') as HTMLElement;
    expect(money).toHaveAttribute('href', '/admin/attempts?card=money-in-float');
    expect(within(money.closest('tr') as HTMLElement).getByText(ru.admin.statsPage.pickedMoreThanFound)).toBeInTheDocument();
    expect(within(table).getAllByText(ru.admin.statsPage.pickedMoreThanFound)).toHaveLength(1);
  });
});

describe('the system page', () => {
  it('names the builds, the content, each dependency and the database', async () => {
    stubVersionFile('0123456789abcdef0123');
    renderApp(api('admin'), '/admin/system');

    const deployed = within(await screen.findByRole('region', { name: ru.admin.systemPage.deployed }));
    expect(deployed.getByText('0.1.0')).toBeInTheDocument();
    expect(deployed.getByText('ac1caf1b2c3d')).toHaveAttribute('title', systemReport.api.commit);
    expect(await deployed.findByText('0123456789ab')).toHaveAttribute('title', '0123456789abcdef0123');
    expect(deployed.getByText('9f8e7d6c5b4a')).toBeInTheDocument();

    const health = within(screen.getByRole('region', { name: ru.admin.systemPage.health }));
    expect(health.getByText(ru.admin.systemPage.dependency.postgresql).nextElementSibling).toHaveTextContent(ru.admin.systemPage.status.ok);
    expect(health.getByText(ru.admin.systemPage.dependency.umami).nextElementSibling).toHaveTextContent(ru.admin.systemPage.status.failing);
    expect(health.getByText(ru.admin.systemPage.dependency.feedbackGitHubApp).nextElementSibling).toHaveTextContent(ru.admin.systemPage.status.notConfigured);
    expect(health.getByText(ru.admin.systemPage.databaseSize).nextElementSibling).toHaveTextContent(/12/);
  });

  it('shows the backup as absent and an unknown web commit calmly, not as errors', async () => {
    renderApp(api('admin'), '/admin/system');

    const health = within(await screen.findByRole('region', { name: ru.admin.systemPage.health }));
    expect(health.getByText(ru.admin.systemPage.noBackup)).toBeInTheDocument();
    const deployed = within(screen.getByRole('region', { name: ru.admin.systemPage.deployed }));
    expect(deployed.getByText(ru.admin.systemPage.webCommit).nextElementSibling).toHaveTextContent(ru.admin.systemPage.unknown);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('lists each module with its migrations, and says which wait', async () => {
    renderApp(api('admin'), '/admin/system');

    const table = within(await screen.findByRole('table'));
    const [, upToDate, pending] = table.getAllByRole('row');
    expect(table.getAllByRole('row')).toHaveLength(3);
    expect(upToDate).toHaveTextContent(`attempts${ru.admin.systemPage.upToDate}720261101_AddStudied`);
    expect(pending).toContainElement(table.getByText('ждёт 1'));
    expect(table.getByText('ждёт 1')).toHaveAttribute('title', '20261119_AddRevision');
  });
});
