import { describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import { ru } from '../../i18n';
import { renderApp } from '../../test/render';
import { jsonResponse } from '../../test/responses';
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
};

const clean = {
  ...scored,
  answer: { picks: [] },
  result: { total: 0, maximum: 0, isCorrect: true, cards: [] },
  review: { notes: {}, lesson: null },
};

function api(attempt: object, taskBody: object = task) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const path = new URL(url).pathname.replace('/api/v1', '');
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
  await screen.findByRole('heading', { level: 1, name: ru.review.title });
}

function line(name: string): HTMLElement {
  const heading = screen.getByRole('heading', { level: 3, name: new RegExp(name) });
  const article = heading.closest('article');

  if (article === null) {
    throw new Error(`No review line for ${name}.`);
  }

  return article;
}

describe('the review', () => {
  it('gives the score in points, with its composition', async () => {
    await openReview(scored);

    expect(screen.getByText('29 из 90')).toBeInTheDocument();
    expect(screen.getByText('Найдено 1 из 2 · лишних выборов: 1 · лечение совпало: 1')).toBeInTheDocument();
  });

  it('shows a found card with the learner’s leaves beside the author’s, by a mark and a word', async () => {
    await openReview(scored);
    const found = within(line('Секреты в репозитории'));

    expect(found.getByText(ru.review.found)).toBeInTheDocument();
    expect(found.getByText(ru.review.foundMark)).toBeInTheDocument();
    expect(found.getByText('43 балла')).toBeInTheDocument();
    expect(found.getByText('в этом контексте это нормально, исправить представление данных')).toBeInTheDocument();
    expect(found.getByText('исправить представление данных')).toBeInTheDocument();
  });

  it('shows a missed card with the author’s leaves and note, and the full card on expanding', async () => {
    await openReview(scored);
    const missed = within(line('Деньги во float'));

    expect(missed.getByText(ru.review.missed)).toBeInTheDocument();
    expect(missed.getByText(ru.review.missedMark)).toBeInTheDocument();
    expect(missed.queryByText(ru.review.yourLeaves)).not.toBeInTheDocument();
    expect(missed.getByText(ru.review.authorLeaves)).toBeInTheDocument();
    expect(missed.getByText('Копейки расходятся с бухгалтерией.')).toBeInTheDocument();

    const card = missed.getByText(ru.review.fullCard).closest('details');
    expect(card).not.toBeNull();
    expect(within(card as HTMLElement).getByText('Никогда.')).toBeInTheDocument();
  });

  it('shows an extra pick as not in this task’s answer, with its card and no author’s leaves', async () => {
    await openReview(scored);
    const extra = within(line('Класс-бог'));

    expect(extra.getByText(ru.review.extra)).toBeInTheDocument();
    expect(extra.getByText(ru.review.extraMark)).toBeInTheDocument();
    expect(extra.getByText('-3 балла')).toBeInTheDocument();
    expect(extra.queryByText(ru.review.authorLeaves)).not.toBeInTheDocument();
    expect(extra.getByText(ru.review.fullCard)).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: ru.review.extras })).toBeInTheDocument();
  });

  it('closes with the lesson, the same code in another context, and trying again', async () => {
    const withSibling = { ...task, sameMaterial: [{ slug: 'flower-shop-chain', title: 'Сеть магазинов', difficulty: 'medium', solved: null }] };
    await openReview(scored, withSibling);

    expect(screen.getByText('Деньги и секреты от масштаба не зависят.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Сеть магазинов' })).toHaveAttribute('href', '/tasks/flower-shop-chain');
    expect(screen.getByRole('link', { name: ru.review.tryAgain })).toHaveAttribute('href', `/tasks/${task.slug}`);
  });

  it('says in words that a clean task with nothing picked was answered right', async () => {
    await openReview(clean);

    expect(screen.getByText('0 из 0')).toBeInTheDocument();
    expect(screen.getByText(ru.review.cleanNothing)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 2, name: ru.review.findings })).not.toBeInTheDocument();
  });

  it('says a clean task had nothing to find when the learner picked something anyway', async () => {
    const pickedOnClean = {
      ...clean,
      answer: { picks: [{ card: 'god-class', leaves: ['manual.split'] }] },
      result: { total: 0, maximum: 0, isCorrect: false, cards: [{ card: 'god-class', outcome: 'extra', points: -3, keyLeaves: null, treatment: null }] },
    };
    await openReview(pickedOnClean);

    expect(screen.getByText(ru.review.cleanExtra)).toBeInTheDocument();
    expect(within(line('Класс-бог')).getByText(ru.review.extra)).toBeInTheDocument();
  });
});
