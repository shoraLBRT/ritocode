import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import type { ProblemCard, TaskSummary } from '../api';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse, pageOf, problemResponse } from '../test/responses';
import { headerResults, RESULTS_PER_KIND } from './headerResults';

/**
 * The arena's header (docs/DESIGN.md › The arena): the search over tasks and problems, the streak
 * and the avatar. The client is stubbed at `fetch`.
 */

const webhookTask = 'Пополнение баланса через вебхук';

const tasks: TaskSummary[] = [
  { slug: 'flower-shop', title: 'Выручка цветочного магазина', difficulty: 'easy', solved: null },
  { slug: 'payment-webhook', title: webhookTask, difficulty: 'medium', solved: null },
];

const sections = {
  signs: null,
  whyAiDoesIt: null,
  cost: null,
  acceptableWhen: null,
  detection: null,
  treatment: null,
  sources: null,
  counterArguments: null,
};

const secrets: ProblemCard = { slug: 'secrets-in-repo', class: 'hygiene', name: 'Секреты в репозитории', summary: 'Пароли и токены прямо в коде.', keywords: ['пароль'], sections };

const cards: ProblemCard[] = [
  secrets,
  { slug: 'money-in-float', class: 'domain', name: 'Деньги во float', summary: 'Суммы в числах с плавающей точкой.', keywords: ['копейки', 'выручка'], sections },
  { slug: 'webhook-replay', class: 'domain', name: 'Повтор вебхука', summary: 'Один платёж зачисляется дважды.', keywords: ['идемпотентность'], sections },
];

const developer = { id: 'u', username: 'developer' };

function urlOf(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }
  return input instanceof URL ? input.href : input.url;
}

function attempt(submittedAt: string | null) {
  return { id: submittedAt ?? 'open', task: 'flower-shop', startedAt: submittedAt ?? new Date().toISOString(), step: 'diagnosis', submittedAt, practice: false, score: null, maxScore: null };
}

function api({ signedIn = true, attempts = [] as ReturnType<typeof attempt>[], lists = true } = {}) {
  const calls: string[] = [];
  const stub = vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = new URL(urlOf(input));
    calls.push(url.pathname);

    switch (url.pathname) {
      case '/api/v1/me':
        return Promise.resolve(signedIn ? jsonResponse(developer) : problemResponse(401, 'unauthenticated', 'x'));
      case '/api/v1/tasks':
        return Promise.resolve(lists ? jsonResponse(pageOf(tasks, 1, 100)) : problemResponse(503, 'unavailable', 'x'));
      case '/api/v1/problems':
        return Promise.resolve(lists ? jsonResponse({ classes: [], cards }) : problemResponse(503, 'unavailable', 'x'));
      case '/api/v1/attempts':
        return Promise.resolve(jsonResponse(pageOf(attempts, 1, 100)));
      default:
        return Promise.resolve(jsonResponse({}));
    }
  });
  return { stub, calls };
}

afterEach(() => {
  vi.useRealTimers();
});

describe('what the header search finds', () => {
  it('ranks tasks by title and problems by name, keywords and summary', () => {
    const found = headerResults(tasks, cards, 'вебхук');

    expect(found.tasks.map((item) => item.to)).toEqual(['/tasks/payment-webhook']);
    expect(found.problems.map((item) => item.to)).toEqual(['/problems#webhook-replay']);
  });

  it('matches a word in another form, and a keyword the card never shows', () => {
    expect(headerResults(tasks, cards, 'выручку').problems.map((item) => item.slug)).toEqual(['money-in-float']);
    expect(headerResults(tasks, cards, 'выручку').tasks.map((item) => item.slug)).toEqual(['flower-shop']);
  });

  it('finds nothing for a query with no word worth searching', () => {
    expect(headerResults(tasks, cards, '  и ')).toEqual({ tasks: [], problems: [] });
  });

  it('shows at most a handful of each kind', () => {
    const many = Array.from({ length: 9 }, (_, index) => ({ ...secrets, slug: `secret-${String(index)}` }));

    expect(headerResults(tasks, many, 'секреты').problems).toHaveLength(RESULTS_PER_KIND);
  });
});

describe('the header search', () => {
  it('reads the lists on the first focus, not with every page', async () => {
    const { stub, calls } = api();
    renderApp(stub, '/privacy');
    await screen.findByLabelText('Профиль developer');

    expect(calls).not.toContain('/api/v1/problems');
    fireEvent.focus(screen.getByRole('combobox', { name: ru.header.search.label }));
    await waitFor(() => {
      expect(calls).toContain('/api/v1/problems');
    });
  });

  it('lists what matches, grouped, and opens the chosen problem', async () => {
    renderApp(api().stub, '/privacy');
    const field = screen.getByRole('combobox', { name: ru.header.search.label });

    fireEvent.focus(field);
    fireEvent.change(field, { target: { value: 'вебхук' } });

    const list = within(screen.getByRole('listbox', { name: ru.header.search.results }));
    expect(await list.findByRole('group', { name: ru.header.search.tasks })).toHaveTextContent(webhookTask);
    expect(list.getByRole('group', { name: ru.header.search.problems })).toHaveTextContent('Повтор вебхука');
    expect(field).toHaveAttribute('aria-expanded', 'true');

    fireEvent.click(list.getByRole('option', { name: 'Повтор вебхука' }));

    expect(await screen.findByRole('heading', { level: 1, name: ru.problems.title })).toBeInTheDocument();
    expect(field).toHaveValue('');
    expect(field).toHaveAttribute('aria-expanded', 'false');
  });

  it('moves through the list with the arrow keys, wrapping, and opens it with Enter', async () => {
    renderApp(api().stub, '/privacy');
    const field = screen.getByRole('combobox', { name: ru.header.search.label });

    fireEvent.focus(field);
    fireEvent.change(field, { target: { value: 'вебхук' } });
    const task = await screen.findByRole('option', { name: webhookTask });
    const problem = screen.getByRole('option', { name: 'Повтор вебхука' });

    fireEvent.keyDown(field, { key: 'ArrowUp' });
    expect(problem).toHaveAttribute('aria-selected', 'true');
    expect(field).toHaveAttribute('aria-activedescendant', problem.id);
    fireEvent.keyDown(field, { key: 'ArrowDown' });
    expect(task).toHaveAttribute('aria-selected', 'true');

    fireEvent.keyDown(field, { key: 'Enter' });
    await waitFor(() => {
      expect(field).toHaveValue('');
    });
  });

  it('says when nothing matches, and closes on Escape', async () => {
    renderApp(api().stub, '/privacy');
    const field = screen.getByRole('combobox', { name: ru.header.search.label });

    fireEvent.focus(field);
    fireEvent.change(field, { target: { value: 'кенгуру' } });

    expect(await screen.findByText(ru.header.search.empty)).toBeInTheDocument();
    fireEvent.keyDown(field, { key: 'Escape' });
    expect(field).toHaveAttribute('aria-expanded', 'false');
  });

  it('says when the lists could not be read', async () => {
    renderApp(api({ lists: false }).stub, '/privacy');
    const field = screen.getByRole('combobox', { name: ru.header.search.label });

    fireEvent.focus(field);
    fireEvent.change(field, { target: { value: 'вебхук' } });

    expect(await screen.findByText(ru.header.search.failed)).toBeInTheDocument();
  });

  it('is reached with Ctrl K from anywhere on the page', () => {
    renderApp(api().stub, '/privacy');

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });

    expect(screen.getByRole('combobox', { name: ru.header.search.label })).toHaveFocus();
  });

  it('steps aside on the task workspace, whose header belongs to the steps', () => {
    renderApp(api().stub, '/tasks/flower-shop');

    expect(screen.queryByRole('combobox', { name: ru.header.search.label })).not.toBeInTheDocument();
  });
});

describe('the header on the task workspace', () => {
  it('keeps the sections beside the steps, so a review is one click from the task list and the catalogue', async () => {
    renderApp(api().stub, '/tasks/flower-shop/attempts/a1');

    const sections = screen.getByRole('navigation', { name: ru.nav.label });
    expect(within(sections).getByRole('link', { name: ru.nav.tasks })).toHaveAttribute('href', '/tasks');
    expect(within(sections).getByRole('link', { name: ru.nav.problems })).toHaveAttribute('href', '/problems');
    expect(await within(sections).findByRole('link', { name: ru.nav.progress })).toHaveAttribute('href', '/progress');
  });

  it('keeps them while a task is being solved too', () => {
    renderApp(api({ signedIn: false }).stub, '/tasks/flower-shop');

    const sections = screen.getByRole('navigation', { name: ru.nav.label });
    expect(within(sections).getByRole('link', { name: ru.nav.tasks })).toBeInTheDocument();
    expect(within(sections).getByRole('link', { name: ru.nav.problems })).toBeInTheDocument();
  });
});

describe('the header streak', () => {
  it('counts the days in a row with a submitted attempt', async () => {
    const day = 24 * 60 * 60 * 1000;
    const now = Date.now();
    const attempts = [attempt(new Date(now).toISOString()), attempt(new Date(now - day).toISOString()), attempt(new Date(now - 3 * day).toISOString())];
    renderApp(api({ attempts }).stub, '/privacy');

    const streak = await screen.findByRole('img', { name: 'Серия: 2 дня подряд' });
    expect(streak).toHaveTextContent('2 дн.');
  });

  it('shows a learner with no attempt a streak of nothing, and a guest none at all', async () => {
    const signedIn = renderApp(api().stub, '/privacy');
    expect(await screen.findByRole('img', { name: 'Серия: 0 дней подряд' })).toBeInTheDocument();
    signedIn.unmount();

    renderApp(api({ signedIn: false }).stub, '/privacy');
    await screen.findByText(ru.session.signIn);
    expect(screen.queryByRole('img', { name: /Серия/ })).not.toBeInTheDocument();
  });
});
