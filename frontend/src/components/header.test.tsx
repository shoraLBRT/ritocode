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
  { slug: 'flower-shop', title: 'Выручка цветочного магазина', difficulty: 'easy', status: null },
  { slug: 'payment-webhook', title: webhookTask, difficulty: 'medium', status: null },
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

const developer = { id: 'u', username: 'developer', admin: false, createdAt: '2026-03-14T09:26:53+00:00', providers: [] };

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
    await screen.findByLabelText('Профиль developer');
    // The learner's own page is the avatar's, not a section (#261).
    expect(within(sections).queryByRole('link', { name: 'Прогресс' })).not.toBeInTheDocument();
    expect(within(sections).queryByRole('link', { name: ru.header.toProfile })).not.toBeInTheDocument();
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

    const streak = await screen.findByRole('link', { name: 'Серия: 2 дня подряд' });
    expect(streak).toHaveTextContent('2 дн.');
    // It leads to the profile, where the whole year is.
    expect(streak).toHaveAttribute('href', '/profile');
  });

  it('shows a learner with no attempt a streak of nothing, and a guest none at all', async () => {
    const signedIn = renderApp(api().stub, '/privacy');
    expect(await screen.findByRole('link', { name: 'Серия: 0 дней подряд' })).toBeInTheDocument();
    signedIn.unmount();

    renderApp(api({ signedIn: false }).stub, '/privacy');
    await screen.findByText(ru.session.signIn);
    expect(screen.queryByRole('link', { name: /Серия/ })).not.toBeInTheDocument();
  });
});

describe('the phone menu', () => {
  it('opens a sheet with the sections, the theme and the session actions of a learner', async () => {
    renderApp(api().stub, '/privacy');
    await screen.findByLabelText('Профиль developer');
    const button = screen.getByRole('button', { name: ru.header.menu });
    expect(button).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('dialog', { name: ru.header.menu })).not.toBeInTheDocument();

    fireEvent.click(button);

    const sheet = within(screen.getByRole('dialog', { name: ru.header.menu }));
    expect(button).toHaveAttribute('aria-expanded', 'true');
    expect(button).toHaveAttribute('aria-controls', screen.getByRole('dialog', { name: ru.header.menu }).id);
    expect(sheet.getAllByRole('link').map((link) => link.textContent)).toEqual([ru.nav.tasks, ru.nav.problems, ru.header.toProfile]);
    expect(sheet.getByRole('button', { name: ru.app.themeDark })).toBeInTheDocument();
    expect(sheet.getByRole('button', { name: ru.session.signOut })).toBeInTheDocument();
    expect(sheet.getByRole('link', { name: ru.nav.tasks })).toHaveFocus();
  });

  it('offers a guest Начать, and keeps the providers behind the header\'s own Войти', async () => {
    renderApp(api({ signedIn: false }).stub, '/privacy');
    await screen.findByText(ru.session.signIn);
    fireEvent.click(screen.getByRole('button', { name: ru.header.menu }));

    const sheet = within(screen.getByRole('dialog', { name: ru.header.menu }));
    expect(sheet.getByRole('link', { name: ru.header.start })).toHaveAttribute('href', '/tasks/demo-task');
    expect(sheet.queryByRole('button', { name: ru.session.signOut })).not.toBeInTheDocument();
    expect(sheet.queryByText(ru.session.signIn)).not.toBeInTheDocument();
  });

  it('closes on Escape and gives the focus back to the button', () => {
    renderApp(api({ signedIn: false }).stub, '/privacy');
    const button = screen.getByRole('button', { name: ru.header.menu });
    fireEvent.click(button);

    fireEvent.keyDown(document, { key: 'Escape' });

    expect(screen.queryByRole('dialog', { name: ru.header.menu })).not.toBeInTheDocument();
    expect(button).toHaveFocus();
  });

  it('closes on following a link, and keeps Tab inside while open', async () => {
    renderApp(api({ signedIn: false }).stub, '/privacy');
    await screen.findByText(ru.session.signIn);
    fireEvent.click(screen.getByRole('button', { name: ru.header.menu }));
    const dialog = screen.getByRole('dialog', { name: ru.header.menu });
    const items = within(dialog).getAllByRole('link');
    const last = within(dialog).getByRole('link', { name: ru.header.start });

    last.focus();
    fireEvent.keyDown(document, { key: 'Tab' });
    expect(items[0]).toHaveFocus();

    fireEvent.click(within(dialog).getByRole('link', { name: ru.nav.problems }));
    await screen.findByRole('heading', { level: 1, name: ru.problems.title });
    expect(screen.queryByRole('dialog', { name: ru.header.menu })).not.toBeInTheDocument();
  });

  it('closes on a tap outside', () => {
    renderApp(api({ signedIn: false }).stub, '/privacy');
    fireEvent.click(screen.getByRole('button', { name: ru.header.menu }));

    const backdrop = document.querySelector('.app__sheet-backdrop');
    if (backdrop === null) {
      throw new Error('no backdrop');
    }
    fireEvent.click(backdrop);

    expect(screen.queryByRole('dialog', { name: ru.header.menu })).not.toBeInTheDocument();
  });
});

describe('the search overlay', () => {
  it('opens from the icon button with the focus in the field, and closes back to the icon', () => {
    renderApp(api().stub, '/privacy');
    const button = screen.getByRole('button', { name: ru.header.search.open });
    const field = screen.getByRole('combobox', { name: ru.header.search.label });

    fireEvent.click(button);
    expect(screen.getByRole('banner')).toHaveAttribute('data-search', 'open');
    expect(field).toHaveFocus();

    fireEvent.click(screen.getByRole('button', { name: ru.header.search.close }));
    expect(screen.getByRole('banner')).not.toHaveAttribute('data-search');
    expect(button).toHaveFocus();
  });

  it('closes on Escape from the field', () => {
    renderApp(api().stub, '/privacy');
    fireEvent.click(screen.getByRole('button', { name: ru.header.search.open }));

    fireEvent.keyDown(screen.getByRole('combobox', { name: ru.header.search.label }), { key: 'Escape' });

    expect(screen.getByRole('banner')).not.toHaveAttribute('data-search');
  });

  it('has no icon button on the workspace', () => {
    renderApp(api().stub, '/tasks/flower-shop');

    expect(screen.queryByRole('button', { name: ru.header.search.open })).not.toBeInTheDocument();
  });
});

describe('the footer', () => {
  it('lists the sections and the service pages, the profile for a learner', async () => {
    renderApp(api().stub, '/privacy');
    await screen.findByLabelText('Профиль developer');

    const sections = within(screen.getByRole('navigation', { name: ru.app.footer.sections }));
    expect(sections.getAllByRole('link').map((link) => link.textContent)).toEqual([ru.nav.tasks, ru.nav.problems, ru.header.toProfile]);
    const about = within(screen.getByRole('navigation', { name: ru.app.footer.about }));
    expect(about.getByRole('link', { name: ru.app.privacy })).toHaveAttribute('href', '/privacy');
  });

  it('leaves out the profile for a guest', async () => {
    renderApp(api({ signedIn: false }).stub, '/privacy');
    await screen.findByText(ru.session.signIn);

    const sections = within(screen.getByRole('navigation', { name: ru.app.footer.sections }));
    expect(sections.queryByRole('link', { name: ru.header.toProfile })).not.toBeInTheDocument();
  });

  it('opens the feedback window from its Обратная связь', () => {
    renderApp(api({ signedIn: false }).stub, '/privacy');
    expect(screen.queryByRole('dialog', { name: ru.feedback.heading })).not.toBeInTheDocument();

    fireEvent.click(within(screen.getByRole('contentinfo')).getByRole('button', { name: ru.app.footer.feedback }));

    expect(screen.getByRole('dialog', { name: ru.feedback.heading })).toBeInTheDocument();
  });
});
