import { describe, expect, it } from 'vitest';
import type { ProblemCard, ProblemCatalogue } from '../../api';
import { moscowDay } from '../home/arena';
import { chartPath, studyProgress } from './profile';

const sections = { signs: null, whyAiDoesIt: null, cost: null, acceptableWhen: null, detection: null, treatment: null, sources: null, counterArguments: null };

function card(slug: string, cls: string): ProblemCard {
  return { slug, class: cls, name: slug, summary: slug, keywords: [], sections };
}

const catalogue: ProblemCatalogue = {
  classes: [
    { id: 'hygiene', name: 'Гигиена', description: null },
    { id: 'domain', name: 'Предметная область', description: null },
  ],
  cards: [card('a', 'hygiene'), card('b', 'hygiene'), card('c', 'domain')],
};

describe('study progress', () => {
  it('counts the published cards marked studied, in all and per class in the catalogue order', () => {
    const study = studyProgress(catalogue, [
      { card: 'a', studiedAt: '2026-11-01T08:00:00Z' },
      { card: 'c', studiedAt: '2026-11-03T08:00:00Z' },
      // A card not in the catalogue is not counted.
      { card: 'retired', studiedAt: '2026-11-02T08:00:00Z' },
    ]);

    expect(study.studied).toBe(2);
    expect(study.total).toBe(3);
    expect(study.byClass).toEqual([
      { class: 'hygiene', name: 'Гигиена', studied: 1, total: 2 },
      { class: 'domain', name: 'Предметная область', studied: 1, total: 1 },
    ]);
  });

  it('grows cumulatively, one point per Moscow day with a mark, from the first one', () => {
    const study = studyProgress(catalogue, [
      { card: 'a', studiedAt: '2026-11-01T08:00:00Z' },
      // 22:00 UTC on the 1st is the 2nd in Moscow.
      { card: 'b', studiedAt: '2026-11-01T22:00:00Z' },
      { card: 'c', studiedAt: '2026-11-02T08:00:00Z' },
    ]);
    const first = moscowDay(new Date('2026-11-01T08:00:00Z'));

    expect(study.timeline).toEqual([
      { day: first, count: 1 },
      { day: first + 1, count: 3 },
    ]);
  });

  it('has nothing to chart without a mark', () => {
    const study = studyProgress(catalogue, []);

    expect(study).toMatchObject({ studied: 0, total: 3, timeline: [] });
    expect(study.byClass.every((item) => item.studied === 0)).toBe(true);
  });
});

describe('the study chart', () => {
  it('steps up on each day with a mark and holds flat to today at the right edge', () => {
    expect(
      chartPath(
        [
          { day: 100, count: 1 },
          { day: 102, count: 2 },
        ],
        104,
      ),
    ).toBe('M0 73 H300 V6 H600');
  });

  it('spans the box when the first mark is today', () => {
    expect(chartPath([{ day: 100, count: 4 }], 100)).toBe('M0 6 H600');
  });

  it('draws nothing without a mark', () => {
    expect(chartPath([], 100)).toBe('');
  });
});
