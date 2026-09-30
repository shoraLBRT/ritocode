import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import { ru } from '../../i18n';
import { renderApp } from '../../test/render';
import { jsonResponse, pageOf, problemResponse } from '../../test/responses';
import { task, tree } from './fixtures';

const submitted = {
  id: 'attempt-1',
  task: task.slug,
  startedAt: '2026-09-30T15:07:41Z',
  step: 'treatment',
  submittedAt: '2026-09-30T15:09:00Z',
  practice: false,
  contentRevision: 'development',
  answer: { picks: [{ card: 'money-in-float', leaves: ['manual.representation'] }] },
  result: {
    total: 36,
    maximum: 90,
    isCorrect: false,
    cards: [
      { card: 'secrets-in-repo', outcome: 'missed', points: -9, keyLeaves: ['auto.secrets'] },
      { card: 'money-in-float', outcome: 'found', points: 45, keyLeaves: ['manual.representation'] },
    ],
  },
};

interface Call {
  readonly method: string;
  readonly url: string;
  readonly body: unknown;
}

/** The development host's API, recording every call; `openAttempt` is an attempt left open earlier. */
function api({ signedIn = true, openAttempt = false } = {}) {
  const calls: Call[] = [];

  const fetch = vi.fn<typeof globalThis.fetch>().mockImplementation((input, init) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const method = init?.method ?? 'GET';
    calls.push({ method, url, body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined });

    const path = new URL(url).pathname.replace('/api/v1', '');

    if (path === '/me') {
      return Promise.resolve(signedIn ? jsonResponse({ id: 'u', username: 'developer' }) : problemResponse(401, 'unauthenticated', 'x'));
    }
    if (path === `/tasks/${task.slug}`) {
      return Promise.resolve(jsonResponse(task));
    }
    if (path === '/treatments') {
      return Promise.resolve(jsonResponse(tree));
    }
    if (path === '/attempts' && method === 'GET') {
      const open = { id: 'attempt-0', task: task.slug, startedAt: '', step: 'diagnosis', submittedAt: null, practice: false, score: null, maxScore: null };
      return Promise.resolve(jsonResponse(pageOf(openAttempt ? [open] : [], 1, 1)));
    }
    if (path === '/attempts' && method === 'POST') {
      return Promise.resolve(jsonResponse({ ...submitted, submittedAt: null, result: null, answer: null, step: 'diagnosis' }, 201));
    }
    if (path.startsWith('/attempts/') && method === 'PATCH') {
      return Promise.resolve(jsonResponse({ ...submitted, submittedAt: null, result: null, answer: null }));
    }
    if (path.endsWith('/submit')) {
      return Promise.resolve(jsonResponse(submitted));
    }
    if (path === '/attempts/attempt-1') {
      return Promise.resolve(jsonResponse(submitted));
    }

    return Promise.resolve(problemResponse(404, 'not_found', `No stub for ${method} ${path}`));
  });

  return { fetch, calls };
}

async function openTask(stub: ReturnType<typeof api>) {
  renderApp(stub.fetch, `/tasks/${task.slug}`);
  await screen.findByRole('heading', { level: 1, name: task.title });
}

describe('the task screen', () => {
  it('shows the context, the brief, the material and step 1', async () => {
    await openTask(api());

    expect(screen.getByText(task.context)).toBeInTheDocument();
    expect(screen.getByText(task.brief)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /revenue\.py/ })).toHaveAttribute('aria-current', 'true');
    expect(screen.getByText('TAX_RATE')).toBeInTheDocument();
    expect(screen.getByText('Зависимости: psycopg')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: ru.task.step1 })).toBeInTheDocument();
  });

  it('solves a task: picks, treats, checks, and lands on the result', async () => {
    const stub = api();
    await openTask(stub);

    // A signed-in learner works in an attempt, started when there is no open one.
    await waitFor(() => {
      expect(stub.calls.some((call) => call.method === 'POST' && call.url.endsWith('/attempts'))).toBe(true);
    });

    fireEvent.click(screen.getByRole('checkbox', { name: /Деньги во float/ }));
    fireEvent.click(screen.getByRole('button', { name: ru.task.toStep2 }));

    // Step 2 is recorded; the check waits for a leaf on every picked card.
    await waitFor(() => {
      expect(stub.calls).toContainEqual({ method: 'PATCH', url: 'http://api.test/api/v1/attempts/attempt-1', body: { step: 'treatment' } });
    });
    const check = screen.getByRole('button', { name: ru.task.check });
    expect(check).toBeDisabled();
    expect(screen.getByText('Отметьте хотя бы одно лечение для каждой карточки: Деньги во float.')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('checkbox', { name: 'исправить представление данных', hidden: true }));
    expect(check).toBeEnabled();
    fireEvent.click(check);

    expect(await screen.findByText('36 из 90')).toBeInTheDocument();
    expect(stub.calls).toContainEqual({
      method: 'POST',
      url: 'http://api.test/api/v1/attempts/attempt-1/submit',
      body: { picks: [{ card: 'money-in-float', leaves: ['manual.representation'] }] },
    });
    expect(screen.getByText(ru.result.missed)).toBeInTheDocument();
  });

  it('checks an answer that picked nothing', async () => {
    await openTask(api());

    fireEvent.click(screen.getByRole('button', { name: ru.task.toStep2 }));

    expect(screen.getByText(ru.task.nothingPicked)).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: ru.task.check })).toBeEnabled();
    });
  });

  it('goes back to step 1 with the picks kept', async () => {
    await openTask(api());

    fireEvent.click(screen.getByRole('checkbox', { name: /Класс-бог/ }));
    fireEvent.click(screen.getByRole('button', { name: ru.task.toStep2 }));
    fireEvent.click(screen.getByRole('button', { name: ru.task.backToStep1 }));

    expect(screen.getByRole('checkbox', { name: /Класс-бог/ })).toBeChecked();
  });

  it('resumes the open attempt at the task rather than starting another', async () => {
    const stub = api({ openAttempt: true });
    await openTask(stub);

    fireEvent.click(screen.getByRole('button', { name: ru.task.toStep2 }));

    await waitFor(() => {
      expect(stub.calls.some((call) => call.method === 'PATCH' && call.url.endsWith('/attempts/attempt-0'))).toBe(true);
    });
    expect(stub.calls.some((call) => call.method === 'POST')).toBe(false);
  });

  it('lets a signed-out visitor work through both steps, but not check', async () => {
    const stub = api({ signedIn: false });
    await openTask(stub);

    fireEvent.click(screen.getByRole('button', { name: ru.task.toStep2 }));

    expect(await screen.findByText(ru.task.signInToCheck)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: ru.task.check })).toBeDisabled();
    expect(stub.calls.some((call) => call.url.includes('/attempts'))).toBe(false);
  });

  it('never asks for a card in full', async () => {
    const stub = api();
    await openTask(stub);
    fireEvent.click(screen.getByRole('button', { name: ru.task.toStep2 }));

    await waitFor(() => {
      expect(stub.calls.some((call) => call.method === 'PATCH')).toBe(true);
    });
    expect(stub.calls.some((call) => call.url.includes('/problems'))).toBe(false);
  });

  it('switches between the three areas by tab at phone width', async () => {
    await openTask(api());

    const code = screen.getByRole('tab', { name: ru.task.codeTab });
    fireEvent.click(code);

    expect(code).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('heading', { level: 2, name: ru.task.material }).closest('section')).toHaveClass('task-screen__area--active');
  });
});
