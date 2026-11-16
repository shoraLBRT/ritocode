import { describe, expect, it } from 'vitest';
import { faceLine, lanes } from './lanes';
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

describe('faceLine', () => {
  it('says a found card is treated when any one of the author’s leaves was chosen', () => {
    expect(faceLine(found, ['manual.handle-errors', 'manual.validate'])).toEqual({ kind: 'treated' });
  });

  it('says a found card is mistreated when leaves were chosen but none fits', () => {
    expect(faceLine({ ...found, treatment: { matched: false, matchedLeaves: [], wrongLeaves: ['brief.scale'] } }, ['brief.scale'])).toEqual({
      kind: 'mistreated',
    });
  });

  it('says a found card is untreated when nothing was chosen', () => {
    expect(faceLine({ ...found, treatment: null }, [])).toEqual({ kind: 'untreated' });
  });

  it('works the verdict out from the picks when the result does not split them', () => {
    expect(faceLine({ ...found, treatment: null }, ['auto.linter'])).toEqual({ kind: 'treated' });
    expect(faceLine({ ...found, treatment: null }, ['brief.scale'])).toEqual({ kind: 'mistreated' });
  });

  it('gives a missed card the author’s first leaf and how many more', () => {
    expect(faceLine(missed, [])).toEqual({ kind: 'cure', leaf: 'brief.scale', more: 0 });
    expect(faceLine({ ...missed, keyLeaves: ['a', 'b', 'c'] }, [])).toEqual({ kind: 'cure', leaf: 'a', more: 2 });
  });

  it('says an extra pick is not in the code', () => {
    expect(faceLine(extra, ['manual.handle-errors'])).toEqual({ kind: 'absent' });
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
