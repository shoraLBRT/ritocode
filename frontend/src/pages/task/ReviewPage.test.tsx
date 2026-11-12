import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, within } from '@testing-library/react';
import { ru } from '../../i18n';
import { renderApp } from '../../test/render';
import { jsonResponse, problemResponse } from '../../test/responses';
import { task, tree } from './fixtures';

const sections = {
  signs: '- Пароль строкой в коде.',
  whyAiDoesIt: null,
  cost: 'Доступ у всех.',
  acceptableWhen: 'Никогда.',
  detection: null,
  treatment: null,
  sources: null,
  counterArguments: null,
};

const catalogue = {
  classes: [],
  cards: task.cards.map((card) => ({ ...card, sections })),
};

// Secrets found with a wrong leaf and a right one, money missed, the god class picked though absent.
const scored = {
  id: 'attempt-1',
  task: task.slug,
  startedAt: '2026-09-30T15:07:41Z',
  step: 'treatment',
  submittedAt: '2026-09-30T15:09:00Z',
  practice: false,
  contentRevision: 'development',
  outcome: { total: 29, maximum: 90, present: 2, found: 1, treated: 1, missed: 1, extra: 1, solved: false },
  revealed: true,
  taskStatus: 'gaveUp',
  answer: {
    picks: [
      { card: 'god-class', leaves: ['manual.split'] },
      { card: 'secrets-in-repo', leaves: ['accept.fits-context', 'manual.representation'] },
    ],
  },
  result: {
    total: 29,
    maximum: 90,
    isCorrect: false,
    cards: [
      {
        card: 'secrets-in-repo',
        outcome: 'found',
        points: 43,
        keyLeaves: ['manual.representation'],
        treatment: { matched: true, matchedLeaves: ['manual.representation'], wrongLeaves: ['accept.fits-context'] },
      },
      { card: 'money-in-float', outcome: 'missed', points: -9, keyLeaves: ['manual.representation'], treatment: null },
      { card: 'god-class', outcome: 'extra', points: -3, keyLeaves: null, treatment: null },
    ],
  },
  review: { notes: { 'money-in-float': 'Копейки расходятся с бухгалтерией.' }, lesson: 'Деньги и секреты от масштаба не зависят.' },
  signalledCards: [],
};

// The same attempt before its answer is revealed: what the API sends then (ADR 0017).
const hidden = { ...scored, revealed: false, taskStatus: 'tried', answer: null, result: null, review: null };

const clean = {
  ...scored,
  outcome: { total: 0, maximum: 0, present: 0, found: 0, treated: 0, missed: 0, extra: 0, solved: true },
  taskStatus: 'solved',
  answer: { picks: [] },
  result: { total: 0, maximum: 0, isCorrect: true, cards: [] },
  review: { notes: {}, lesson: null },
};

function api(attempt: object, taskBody: object = task, signal: () => Response = () => jsonResponse({}, 201), revealed: () => Response = () => jsonResponse(scored)) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const path = new URL(url).pathname.replace('/api/v1', '');

    if (path === '/signals') {
      return Promise.resolve(signal());
    }

    if (path === '/attempts/attempt-1/reveal') {
      return Promise.resolve(revealed());
    }

    const bodies: Record<string, object> = {
      '/me': { id: 'u', username: 'developer' },
      '/attempts/attempt-1': attempt,
      [`/tasks/${task.slug}`]: taskBody,
      '/treatments': tree,
      '/problems': catalogue,
    };
    return Promise.resolve(jsonResponse(bodies[path] ?? {}));
  });
}

async function openReview(attempt: object, taskBody?: object) {
  renderApp(api(attempt, taskBody), `/tasks/${task.slug}/attempts/attempt-1`);
  await screen.findByRole('heading', { level: 2, name: ru.review.title });
}

/** Chooses a line of the result in the sidebar and returns it in full, as shown beside it. */
function line(name: string): HTMLElement {
  fireEvent.click(screen.getByRole('button', { name: new RegExp(name) }));
  return screen.getByRole('article', { name });
}

describe('the review', () => {
  it('gives the score in points, with its composition, in the third step', async () => {
    await openReview(scored);

    expect(screen.getByText('29 из 90')).toBeInTheDocument();
    expect(screen.getByText('Найдено 1 из 2 · лишних выборов: 1 · лечение совпало: 1')).toBeInTheDocument();
    expect(within(screen.getByRole('banner')).getByRole('button', { name: /Разбор/ })).toHaveAttribute('aria-current', 'step');
  });

  it('opens on the first line, and shows a found card with the learner’s leaves beside the author’s, by a mark and a word', async () => {
    await openReview(scored);
    const found = within(screen.getByRole('article', { name: 'Секреты в репозитории' }));

    expect(found.getByText(ru.review.found)).toBeInTheDocument();
    expect(found.getByText(ru.review.foundMark)).toBeInTheDocument();
    expect(found.getByText('43 балла')).toBeInTheDocument();
    expect(found.getByText('в этом контексте это нормально')).toBeInTheDocument();
    expect(found.getAllByText('исправить представление данных')).toHaveLength(2);
  });

  it('shows a missed card with the author’s leaves, the note and the full card', async () => {
    await openReview(scored);
    const missed = within(line('Деньги во float'));

    expect(missed.getByText(ru.review.missed)).toBeInTheDocument();
    expect(missed.getByText(ru.review.missedMark)).toBeInTheDocument();
    expect(missed.queryByText(ru.review.yourLeaves)).not.toBeInTheDocument();
    expect(missed.getByText(ru.review.authorLeaves)).toBeInTheDocument();
    expect(missed.getByText('Копейки расходятся с бухгалтерией.')).toBeInTheDocument();
    expect(missed.getByText('Никогда.')).toBeInTheDocument();
  });

  it('shows an extra pick as not in this task’s answer, with its card and no author’s leaves', async () => {
    await openReview(scored);
    const extra = within(line('Класс-бог'));

    expect(extra.getByText(ru.review.extra)).toBeInTheDocument();
    expect(extra.getByText(ru.review.extraMark)).toBeInTheDocument();
    expect(extra.getByText('-3 балла')).toBeInTheDocument();
    expect(extra.queryByText(ru.review.authorLeaves)).not.toBeInTheDocument();
    expect(extra.getByText('Доступ у всех.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: ru.review.extras })).toBeInTheDocument();
  });

  it('shows the code again on asking', async () => {
    await openReview(scored);

    fireEvent.click(screen.getByRole('button', { name: ru.review.codeTab }));

    expect(screen.getByText('TAX_RATE')).toBeInTheDocument();
    expect(screen.queryByRole('article')).not.toBeInTheDocument();
  });

  it('closes with the lesson, the same code in another context, and trying again', async () => {
    const withSibling = { ...task, sameMaterial: [{ slug: 'flower-shop-chain', title: 'Сеть магазинов', difficulty: 'medium', status: null }] };
    await openReview(scored, withSibling);

    expect(screen.getByText('Деньги и секреты от масштаба не зависят.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Сеть магазинов' })).toHaveAttribute('href', '/tasks/flower-shop-chain');
    expect(screen.getByRole('link', { name: ru.review.tryAgain })).toHaveAttribute('href', `/tasks/${task.slug}`);
  });

  it('says in words that a clean task with nothing picked was answered right', async () => {
    await openReview(clean);

    expect(screen.getByText('0 из 0')).toBeInTheDocument();
    expect(screen.getByText(ru.review.cleanNothing)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: ru.review.findings })).not.toBeInTheDocument();
  });

  it('says a clean task had nothing to find when the learner picked something anyway', async () => {
    const pickedOnClean = {
      ...clean,
      answer: { picks: [{ card: 'god-class', leaves: ['manual.split'] }] },
      outcome: { ...clean.outcome, extra: 1, solved: false },
      result: { total: 0, maximum: 0, isCorrect: false, cards: [{ card: 'god-class', outcome: 'extra', points: -3, keyLeaves: null, treatment: null }] },
    };
    await openReview(pickedOnClean);

    expect(screen.getByText(ru.review.cleanExtra)).toBeInTheDocument();
    expect(within(line('Класс-бог')).getByText(ru.review.extra)).toBeInTheDocument();
  });
});

describe('the review before the answer is revealed', () => {
  it('gives the score, says the task is not solved, and draws the counts — naming no card', async () => {
    await openReview(hidden);

    expect(screen.getByText('29 из 90')).toBeInTheDocument();
    expect(screen.getByText(ru.review.notSolved)).toBeInTheDocument();
    const tally = within(screen.getByRole('complementary', { name: ru.review.tally.title }));
    expect(tally.getByText(ru.review.tally.found).nextElementSibling).toHaveTextContent('1 из 2');
    expect(tally.getByText(ru.review.tally.treated).nextElementSibling).toHaveTextContent('1 из 1');
    expect(tally.getByText(ru.review.tally.missed).nextElementSibling).toHaveTextContent('1');
    expect(tally.getByText(ru.review.tally.extra).nextElementSibling).toHaveTextContent('1');

    for (const name of ['Секреты в репозитории', 'Деньги во float', 'Класс-бог']) {
      expect(screen.queryByText(name)).not.toBeInTheDocument();
    }
    expect(screen.queryByText(ru.review.lesson)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: ru.review.signal })).not.toBeInTheDocument();
  });

  it('keeps trying again first, shows the code, and holds back the same code in another context', async () => {
    const withSibling = { ...task, sameMaterial: [{ slug: 'flower-shop-chain', title: 'Сеть магазинов', difficulty: 'medium', status: null }] };
    await openReview(hidden, withSibling);

    expect(screen.getByRole('link', { name: ru.review.tryAgain })).toHaveAttribute('href', `/tasks/${task.slug}`);
    expect(screen.getByText('TAX_RATE')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Сеть магазинов' })).not.toBeInTheDocument();
  });

  it('asks before revealing, saying the task will show as gave up, and can be left closed', async () => {
    const stub = api(hidden);
    renderApp(stub, `/tasks/${task.slug}/attempts/attempt-1`);
    fireEvent.click(await screen.findByRole('button', { name: ru.review.reveal }));

    expect(screen.getByText(ru.review.revealGivesUp)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: ru.review.revealCancel }));

    expect(screen.queryByText(ru.review.revealGivesUp)).not.toBeInTheDocument();
    expect(stub.mock.calls.some(([input]) => urlOf(input).endsWith('/reveal'))).toBe(false);
  });

  it('opens the whole review once the reveal is confirmed', async () => {
    const stub = api(hidden);
    renderApp(stub, `/tasks/${task.slug}/attempts/attempt-1`);
    fireEvent.click(await screen.findByRole('button', { name: ru.review.reveal }));
    fireEvent.click(screen.getByRole('button', { name: ru.review.revealConfirm }));

    expect(await screen.findByRole('article', { name: 'Секреты в репозитории' })).toBeInTheDocument();
    expect(screen.getByText('Деньги и секреты от масштаба не зависят.')).toBeInTheDocument();
    const call = stub.mock.calls.find(([input]) => urlOf(input).endsWith('/attempts/attempt-1/reveal'));
    expect(call?.[1]?.method).toBe('POST');
  });

  it('says a solved task stays solved when its answer is revealed', async () => {
    const solved = { ...hidden, taskStatus: 'solved', outcome: { ...hidden.outcome, solved: true } };
    await openReview(solved);

    expect(screen.getByText(ru.review.solved)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: ru.review.reveal }));
    expect(screen.getByText(ru.review.revealKeepsSolved)).toBeInTheDocument();
  });

  it('says so when the reveal fails, and stays closed', async () => {
    renderApp(api(hidden, task, undefined, () => problemResponse(500, 'internal_error', 'x')), `/tasks/${task.slug}/attempts/attempt-1`);
    fireEvent.click(await screen.findByRole('button', { name: ru.review.reveal }));
    fireEvent.click(screen.getByRole('button', { name: ru.review.revealConfirm }));

    expect(await screen.findByRole('alert')).toHaveTextContent(ru.review.revealFailed);
    expect(screen.queryByRole('article')).not.toBeInTheDocument();
  });
});

function urlOf(input: RequestInfo | URL): string {
  return typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
}

function signalCalls(stub: ReturnType<typeof api>) {
  return stub.mock.calls.filter(([input]) => urlOf(input).endsWith('/signals'));
}

describe('a signal from an extra pick', () => {
  it('opens a one-line comment under the extra pick only, and sends it for that pick', async () => {
    const umami = { track: vi.fn() };
    window.umami = umami;
    const stub = api(scored);
    renderApp(stub, `/tasks/${task.slug}/attempts/attempt-1`);
    await screen.findByRole('heading', { level: 2, name: ru.review.title });

    // Only the extra pick offers it: a found or missed card is already in the key.
    expect(screen.queryByRole('button', { name: ru.review.signal })).not.toBeInTheDocument();
    const extra = within(line('Класс-бог'));
    expect(screen.getAllByRole('button', { name: ru.review.signal })).toHaveLength(1);

    fireEvent.click(extra.getByRole('button', { name: ru.review.signal }));
    fireEvent.change(extra.getByLabelText(ru.review.signalComment), { target: { value: 'Класс на 400 строк в app.py' } });
    fireEvent.click(extra.getByRole('button', { name: ru.review.signalSend }));

    expect(await extra.findByText(ru.review.signalSent)).toBeInTheDocument();
    expect(extra.queryByRole('button', { name: ru.review.signal })).not.toBeInTheDocument();

    const [call] = signalCalls(stub);
    expect(call?.[1]?.method).toBe('POST');
    expect(JSON.parse(call?.[1]?.body as string)).toEqual({ attempt: 'attempt-1', card: 'god-class', comment: 'Класс на 400 строк в app.py' });

    // The score is the one it was.
    expect(screen.getByText('29 из 90')).toBeInTheDocument();

    // Counted by the card alone: the comment stays with the author.
    expect(umami.track).toHaveBeenCalledWith('signal-sent', { card: 'god-class' });
  });

  it('can be cancelled without sending anything', async () => {
    const stub = api(scored);
    renderApp(stub, `/tasks/${task.slug}/attempts/attempt-1`);
    await screen.findByRole('heading', { level: 2, name: ru.review.title });
    const extra = within(line('Класс-бог'));

    fireEvent.click(extra.getByRole('button', { name: ru.review.signal }));
    fireEvent.click(extra.getByRole('button', { name: ru.review.signalCancel }));

    expect(extra.getByRole('button', { name: ru.review.signal })).toBeInTheDocument();
    expect(signalCalls(stub)).toHaveLength(0);
  });

  it('shows a pick signalled before as sent', async () => {
    await openReview({ ...scored, signalledCards: ['god-class'] });

    expect(within(line('Класс-бог')).getByText(ru.review.signalSent)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: ru.review.signal })).not.toBeInTheDocument();
  });

  it('takes a signal the API already has as sent', async () => {
    renderApp(api(scored, task, () => problemResponse(409, 'signal_already_sent', 'Already.')), `/tasks/${task.slug}/attempts/attempt-1`);
    await screen.findByRole('heading', { level: 2, name: ru.review.title });
    const extra = within(line('Класс-бог'));
    fireEvent.click(extra.getByRole('button', { name: ru.review.signal }));
    fireEvent.click(extra.getByRole('button', { name: ru.review.signalSend }));
    expect(await extra.findByText(ru.review.signalSent)).toBeInTheDocument();
  });

  it('keeps the comment and says why when sending fails', async () => {
    renderApp(api(scored, task, () => problemResponse(429, 'signal_rate_limited', 'At most 10 signals.')), `/tasks/${task.slug}/attempts/attempt-1`);
    await screen.findByRole('heading', { level: 2, name: ru.review.title });
    const extra = within(line('Класс-бог'));
    fireEvent.click(extra.getByRole('button', { name: ru.review.signal }));
    fireEvent.change(extra.getByLabelText(ru.review.signalComment), { target: { value: 'здесь' } });
    fireEvent.click(extra.getByRole('button', { name: ru.review.signalSend }));

    expect(await extra.findByText('At most 10 signals.')).toBeInTheDocument();
    expect(extra.getByLabelText(ru.review.signalComment)).toHaveValue('здесь');
    expect(extra.getByRole('button', { name: ru.review.signalSend })).toBeEnabled();
  });
});
