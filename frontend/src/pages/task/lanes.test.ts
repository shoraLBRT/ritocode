import { describe, expect, it } from 'vitest';
import { faceLeaves, lanes } from './lanes';
import type { Line } from './lanes';

const found: Line = {
  card: 'sql-injection',
  outcome: 'found',
  points: 3,
  keyLeaves: ['manual.validate', 'auto.linter', 'rule.conventions'],
  treatment: { matched: true, matchedLeaves: ['manual.validate', 'auto.linter'], wrongLeaves: ['manual.handle-errors'] },
};
const missed: Line = { card: 'unbounded-load', outcome: 'missed', points: 0, keyLeaves: ['brief.scale'], treatment: null };
const extra: Line = { card: 'swallowed-exception', outcome: 'extra', points: -1, keyLeaves: null, treatment: null };

describe('faceLeaves', () => {
  it('puts the right leaves first, then the extra ones, then the author’s others as not chosen', () => {
    expect(faceLeaves(found, ['manual.handle-errors', 'manual.validate', 'auto.linter'])).toEqual([
      { leaf: 'manual.validate', mark: 'right' },
      { leaf: 'auto.linter', mark: 'right' },
      { leaf: 'manual.handle-errors', mark: 'extra' },
      { leaf: 'rule.conventions', mark: 'unchosen' },
    ]);
  });

  it('shows the author’s leaves of a missed card as not chosen', () => {
    expect(faceLeaves(missed, [])).toEqual([{ leaf: 'brief.scale', mark: 'unchosen' }]);
  });

  it('shows the learner’s leaves of an extra pick plainly, as the key says nothing of them', () => {
    expect(faceLeaves(extra, ['manual.handle-errors'])).toEqual([{ leaf: 'manual.handle-errors', mark: 'plain' }]);
  });

  it('works the marks out from the picks when the result does not split them', () => {
    expect(faceLeaves({ ...found, treatment: null }, ['auto.linter', 'brief.scale']).map((each) => each.mark)).toEqual([
      'right',
      'extra',
      'unchosen',
      'unchosen',
    ]);
  });
});

describe('lanes', () => {
  it('sorts the lines into found, missed and extra, keeping empty lanes', () => {
    expect(lanes([extra, found]).map((lane) => [lane.outcome, lane.lines.map((line) => line.card)])).toEqual([
      ['found', ['sql-injection']],
      ['missed', []],
      ['extra', ['swallowed-exception']],
    ]);
  });
});
