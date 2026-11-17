import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, within } from '@testing-library/react';
import type { AttemptSummary, Me, TaskSummary } from '../../api';
import { ru } from '../../i18n';
import { renderApp } from '../../test/render';
import { jsonResponse, pageOf, problemResponse } from '../../test/responses';

const classes = [
  { class: 'disproportion', name: 'Несоразмерность', met: 0, found: 0, treatedRight: 0 },
  { class: 'hygiene', name: 'Гигиена и безопасность', met: 3, found: 2, treatedRight: 2 },
  { class: 'domain', name: 'Предметная область', met: 1, found: 1, treatedRight: 0 },
];

const progress = {
  tasks: 2,
  notRevealed: 0,
  classes,
  cards: [
    { card: 'hardcoded-config', name: 'Хардкод конфигурации', class: 'hygiene', met: 1, found: 1, missed: 0, pickedWhenAbsent: 1, treatedRight: 1 },
    { card: 'secrets-in-repo', name: 'Секреты в репозитории', class: 'hygiene', met: 1, found: 1, missed: 0, pickedWhenAbsent: 0, treatedRight: 1 },
    { card: 'swallowed-error', name: 'Проглоченная ошибка', class: 'hygiene', met: 1, found: 0, missed: 1, pickedWhenAbsent: 0, treatedRight: 0 },
    { card: 'money-in-float', name: 'Деньги не в десятичном типе', class: 'domain', met: 1, found: 1, missed: 0, pickedWhenAbsent: 0, treatedRight: 0 },
    { card: 'gone-card', name: 'gone-card', class: null, met: 1, found: 0, missed: 1, pickedWhenAbsent: 0, treatedRight: 0 },
  ],
};

const empty = { tasks: 0, notRevealed: 0, classes: classes.map((item) => ({ ...item, met: 0, found: 0, treatedRight: 0 })), cards: [] };

const tasks: TaskSummary[] = [
  { slug: 'easy-1', title: 'Лёгкая один', difficulty: 'easy', status: 'solved' },
  { slug: 'easy-2', title: 'Лёгкая два', difficulty: 'easy', status: 'solved' },
  { slug: 'easy-3', title: 'Лёгкая три', difficulty: 'easy', status: 'tried' },
  { slug: 'medium-1', title: 'Средняя один', difficulty: 'medium', status: 'solved' },
  { slug: 'medium-2', title: 'Средняя два', difficulty: 'medium', status: 'gaveUp' },
  { slug: 'hard-1', title: 'Сложная один', difficulty: 'hard', status: 'new' },
];

const day = 24 * 60 * 60 * 1000;

function attempt(id: string, task: string, daysAgo: number | null, extra: Partial<AttemptSummary> = {}): AttemptSummary {
  const at = new Date(Date.now() - (daysAgo ?? 0) * day).toISOString();
  return {
    id,
    task,
    startedAt: at,
    step: 'treatment',
    submittedAt: daysAgo === null ? null : at,
    practice: false,
    score: daysAgo === null ? null : 4,
    maxScore: daysAgo === null ? null : 6,
    ...extra,
  };
}

const attempts = [
  attempt('a-open', 'easy-3', null),
  attempt('a-practice', 'easy-1', 0, { practice: true, score: 6 }),
  attempt('a-first', 'easy-1', 1),
  attempt('a-gone', 'retired-task', 5),
];

function urlOf(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }
  return input instanceof URL ? input.href : input.url;
}

/** The caller: joined in March 2026, linked to both providers. */
const me: Me = { id: 'u', username: 'developer', admin: false, createdAt: '2026-03-14T09:26:53+00:00', providers: ['github', 'google'] };

function api({
  body = progress,
  list = attempts,
  signedIn = true,
  failProgress = false,
  caller = me,
}: { body?: object; list?: AttemptSummary[]; signedIn?: boolean; failProgress?: boolean; caller?: Me } = {}) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const { pathname } = new URL(urlOf(input));

    switch (pathname) {
      case '/api/v1/me':
        return Promise.resolve(signedIn ? jsonResponse(caller) : problemResponse(401, 'unauthenticated', 'x'));
      case '/api/v1/me/progress':
        return Promise.resolve(failProgress ? problemResponse(500, 'internal', 'Сбой.') : jsonResponse(body));
      case '/api/v1/tasks':
        return Promise.resolve(jsonResponse(pageOf(tasks, 1, 100)));
      case '/api/v1/attempts':
        return Promise.resolve(jsonResponse(pageOf(list, 1, 100)));
      default:
        return Promise.resolve(jsonResponse([]));
    }
  });
}

describe('the profile', () => {
  it('names the learner and offers the way out', async () => {
    renderApp(api(), '/profile');

    expect(await screen.findByRole('heading', { level: 1, name: 'developer' })).toBeInTheDocument();
    const person = within(screen.getByRole('region', { name: 'developer' }));
    expect(person.getByRole('button', { name: ru.session.signOut })).toBeInTheDocument();
    // Today and yesterday each had a submitted attempt.
    expect(await person.findByRole('img', { name: 'Серия: 2 дня подряд' })).toBeInTheDocument();
  });

  it('says since when the learner is here and which providers they sign in with', async () => {
    renderApp(api(), '/profile');

    await screen.findByRole('heading', { level: 1, name: 'developer' });
    const person = within(screen.getByRole('region', { name: 'developer' }));
    // The month in the genitive, as «с» wants it.
    expect(person.getByText('На Ritocode с марта 2026 г.')).toBeInTheDocument();
    const providers = within(person.getByRole('list', { name: ru.profile.person.providers }));
    expect(providers.getAllByRole('listitem').map((item) => item.textContent)).toEqual(['GitHub', 'Google']);
  });

  it('shows no providers for an identity linked to none', async () => {
    renderApp(api({ caller: { ...me, providers: [] } }), '/profile');

    await screen.findByRole('heading', { level: 1, name: 'developer' });
    const person = within(screen.getByRole('region', { name: 'developer' }));
    expect(person.getByText('На Ritocode с марта 2026 г.')).toBeInTheDocument();
    expect(person.queryByRole('list', { name: ru.profile.person.providers })).not.toBeInTheDocument();
  });

  it('counts the tasks solved by difficulty from the task list', async () => {
    renderApp(api(), '/profile');

    const solved = within(await screen.findByRole('region', { name: ru.profile.solved.title }));
    expect(await solved.findByRole('img', { name: 'Решено 3 из 6' })).toBeInTheDocument();
    expect(solved.getByLabelText('Лёгкие: решено 2 из 3')).toBeInTheDocument();
    expect(solved.getByLabelText('Средние: решено 1 из 2')).toBeInTheDocument();
    expect(solved.getByLabelText('Сложные: решено 0 из 1')).toBeInTheDocument();
    expect(solved.getByText('Начато: 1')).toBeInTheDocument();
    expect(solved.getByText('Ответ открыт: 1')).toBeInTheDocument();
  });

  it('sums the diagnosis over the classes and the wrong picks over the cards', async () => {
    renderApp(api(), '/profile');

    const tiles = within(await screen.findByRole('region', { name: ru.profile.diagnosis.title }));
    expect((await tiles.findByText(ru.profile.diagnosis.found)).parentElement).toHaveTextContent('3 из 4');
    expect(tiles.getByText(ru.profile.diagnosis.treatedRight).parentElement).toHaveTextContent('2 из 3');
    const extra = tiles.getByText(ru.profile.diagnosis.pickedWhenAbsent).parentElement;
    expect(extra).toHaveTextContent('1');
    expect(extra).toHaveClass('rc-profile__tile--danger');
    expect(tiles.getByText(ru.profile.diagnosis.note)).toBeInTheDocument();
  });

  it('shows every class with its counts, and says which were not met yet', async () => {
    renderApp(api(), '/profile');

    const card = within(await screen.findByRole('region', { name: ru.profile.classes.title }));
    const items = await card.findAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(items[0]).toHaveTextContent('Несоразмерность');
    expect(items[0]).toHaveTextContent(ru.profile.classes.notMet);
    expect(items[1]).toHaveTextContent('найдено 2 из 3');
    expect(items[1]).toHaveTextContent('лечение верно: 2');
    expect(card.getByRole('img', { name: 'Найдено 1 из 1' })).toBeInTheDocument();
  });

  it('counts a year of activity', async () => {
    renderApp(api(), '/profile');

    const activity = within(await screen.findByRole('region', { name: ru.profile.activity.title }));
    expect(await activity.findByText('3 попытки за год')).toBeInTheDocument();
    expect(activity.getByText('Активных дней: 3')).toBeInTheDocument();
    expect(activity.getByText('Самая длинная серия: 2 дн.')).toBeInTheDocument();
    expect(activity.getByRole('img', { name: /^Карта активности за год: 3 попытки/ })).toBeInTheDocument();
  });

  it('lists the recent submitted attempts, each opening its review', async () => {
    renderApp(api(), '/profile');

    const list = within(await screen.findByRole('region', { name: ru.profile.tabs.label }));
    const first = await list.findByRole('link', { name: /Лёгкая один.*Первая/ });
    expect(first).toHaveAttribute('href', '/tasks/easy-1/attempts/a-first');
    expect(within(first).getByLabelText('Баллы: 4 из 6')).toHaveTextContent('4/6');
    expect(list.getByRole('link', { name: /Лёгкая один.*Практика/ })).toHaveAttribute('href', '/tasks/easy-1/attempts/a-practice');

    // An attempt never submitted is not an answer given: it is not listed.
    expect(list.queryByRole('link', { name: /Лёгкая три/ })).not.toBeInTheDocument();
    expect(list.getAllByRole('link')).toHaveLength(3);

    // A task no longer in the list is named by its slug.
    expect(list.getByRole('link', { name: /retired-task/ })).toHaveAttribute('href', '/tasks/retired-task/attempts/a-gone');
  });

  it('opens the cards tab from the address, and tables the cards by class', async () => {
    renderApp(api(), '/profile?tab=cards');

    const table = within(await screen.findByRole('table'));
    expect(screen.getByRole('button', { name: ru.profile.tabs.cards })).toHaveAttribute('aria-pressed', 'true');
    const rows = table.getAllByRole('row').slice(1).map((row) => row.textContent);

    // A header row per class that has cards, then its cards; the unknown card last, in a group of its own.
    expect(rows).toEqual([
      'Гигиена и безопасность',
      'Хардкод конфигурации11011',
      'Секреты в репозитории11001',
      'Проглоченная ошибка10100',
      'Предметная область',
      'Деньги не в десятичном типе11000',
      ru.profile.cards.noClass,
      'gone-card10100',
    ]);
    expect(table.getByRole('link', { name: 'Секреты в репозитории' })).toHaveAttribute('href', '/problems#secrets-in-repo');
    expect(table.queryByRole('link', { name: 'gone-card' })).not.toBeInTheDocument();

    // Zeros are quiet, a miss is a warning.
    const swallowed = table.getByRole('row', { name: /Проглоченная ошибка/ });
    expect(within(swallowed).getAllByRole('cell')[2]).toHaveClass('rc-profile__count--warn');
    expect(within(swallowed).getAllByRole('cell')[1]).toHaveClass('rc-profile__count--zero');
  });

  it('filters the cards by a class that has some', async () => {
    renderApp(api(), '/profile?tab=cards');

    const filter = within(await screen.findByRole('group', { name: ru.profile.cards.filter }));
    expect(filter.getAllByRole('button').map((button) => button.textContent)).toEqual([ru.profile.cards.all, 'Гигиена и безопасность3', 'Предметная область1']);

    fireEvent.click(filter.getByRole('button', { name: /Предметная область/ }));
    const rows = within(screen.getByRole('table')).getAllByRole('row').slice(1).map((row) => row.textContent);
    expect(rows).toEqual(['Предметная область', 'Деньги не в десятичном типе11000']);

    fireEvent.click(filter.getByRole('button', { name: ru.profile.cards.all }));
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(9);
  });

  it('switches between the tabs', async () => {
    renderApp(api(), '/profile');

    fireEvent.click(await screen.findByRole('button', { name: ru.profile.tabs.cards }));
    expect(await screen.findByRole('table')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: ru.profile.tabs.attempts }));
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(await screen.findByRole('link', { name: /Лёгкая один.*Первая/ })).toBeInTheDocument();
  });

  it('says how many tasks the cards leave out until their answer is revealed', async () => {
    renderApp(api({ body: { ...progress, tasks: 4, notRevealed: 2 } }), '/profile?tab=cards');

    expect(
      await screen.findByText('2 задачи пока не учтены по карточкам: их ответ ещё не открыт. Открой ответ в разборе задачи — и их карточки появятся здесь.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('table')).toBeInTheDocument();
  });

  it('shows the note without a table when no answer is revealed yet', async () => {
    renderApp(api({ body: { tasks: 1, notRevealed: 1, classes, cards: [] } }), '/profile?tab=cards');

    expect(await screen.findByText(/^1 задача пока не учтена по карточкам: её ответ ещё не открыт\./)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('keeps the whole frame for a learner with no attempts, and invites them to the tasks', async () => {
    renderApp(api({ body: empty, list: [] }), '/profile');

    expect(await screen.findByRole('link', { name: ru.profile.start.action })).toHaveAttribute('href', '/tasks');
    expect(screen.getByRole('heading', { level: 1, name: 'developer' })).toBeInTheDocument();
    expect(await screen.findByRole('img', { name: 'Решено 3 из 6' })).toBeInTheDocument();
    expect(await screen.findByRole('img', { name: /^Карта активности за год: 0 попыток/ })).toBeInTheDocument();
    expect(screen.getByText(ru.profile.classes.none)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: ru.profile.tabs.cards })).not.toBeInTheDocument();
  });

  it('keeps the other cards when one read fails', async () => {
    renderApp(api({ failProgress: true }), '/profile');

    expect(await screen.findByRole('img', { name: 'Решено 3 из 6' })).toBeInTheDocument();
    const diagnosis = within(screen.getByRole('region', { name: ru.profile.diagnosis.title }));
    expect(await diagnosis.findByRole('button', { name: ru.state.retry })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'developer' })).toBeInTheDocument();
  });

  it('is where the old progress address leads', async () => {
    renderApp(api(), '/progress');

    expect(await screen.findByRole('heading', { level: 1, name: 'developer' })).toBeInTheDocument();
  });

  it('is closed to a signed-out visitor, whose progress is never asked for', async () => {
    const stub = api({ signedIn: false });
    renderApp(stub, '/profile');

    expect(await screen.findByText(ru.session.requiredText)).toBeInTheDocument();
    expect(stub.mock.calls.some(([input]) => urlOf(input).endsWith('/me/progress'))).toBe(false);
  });
});
