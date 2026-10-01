import { describe, expect, it } from 'vitest';
import { clearDraft, restoreDraft, saveDraft } from './draft';
import { task, tree } from './fixtures';

const key = `ritocode:answer:${task.slug}`;

function storageWith(value: string | null): Storage {
  sessionStorage.clear();
  if (value !== null) {
    sessionStorage.setItem(key, value);
  }
  return sessionStorage;
}

function kept(picks: unknown, step: unknown = 2, version: unknown = 1): string {
  return JSON.stringify({ version, step, picks });
}

describe('the answer kept in the browser', () => {
  it('comes back as it was saved', () => {
    const answer = [
      { card: 'money-in-float', leaves: ['manual.representation'] },
      { card: 'god-class', leaves: [] },
    ];

    expect(saveDraft(sessionStorage, task.slug, { answer, step: 2 })).toBe(true);
    expect(restoreDraft(sessionStorage, task, tree)).toEqual({ answer, step: 2 });
  });

  it('keeps picking nothing as an answer', () => {
    expect(restoreDraft(storageWith(kept([])), task, tree)).toEqual({ answer: [], step: 2 });
  });

  it('is gone once cleared', () => {
    saveDraft(sessionStorage, task.slug, { answer: [], step: 1 });
    clearDraft(sessionStorage, task.slug);

    expect(restoreDraft(sessionStorage, task, tree)).toBeNull();
  });

  it.each([
    ['nothing kept', null],
    ['not JSON', '{not json'],
    ['another version', kept([], 2, 0)],
    ['no step', kept([], 3)],
    ['picks not a list', kept({ card: 'god-class' })],
    ['a card the task does not offer', kept([{ card: 'no-such-card', leaves: [] }])],
    ['a card twice', kept([{ card: 'god-class', leaves: [] }, { card: 'god-class', leaves: [] }])],
    ['a leaf the tree does not have', kept([{ card: 'god-class', leaves: ['manual.rewrite'] }])],
    ['a leaf twice', kept([{ card: 'god-class', leaves: ['manual.split', 'manual.split'] }])],
    ['a leaf that is not a string', kept([{ card: 'god-class', leaves: [1] }])],
  ])('is not restored when it does not fit the task: %s', (_, value) => {
    expect(restoreDraft(storageWith(value), task, tree)).toBeNull();
  });

  it('reports a browser that will not keep it, and never throws', () => {
    const refusing = {
      setItem: () => {
        throw new DOMException('quota', 'QuotaExceededError');
      },
      getItem: () => {
        throw new DOMException('denied', 'SecurityError');
      },
      removeItem: () => {
        throw new DOMException('denied', 'SecurityError');
      },
    } as unknown as Storage;

    expect(saveDraft(refusing, task.slug, { answer: [], step: 1 })).toBe(false);
    expect(saveDraft(null, task.slug, { answer: [], step: 1 })).toBe(false);
    expect(restoreDraft(refusing, task, tree)).toBeNull();
    expect(() => {
      clearDraft(refusing, task.slug);
    }).not.toThrow();
  });
});
