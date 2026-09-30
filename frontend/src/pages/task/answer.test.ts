import { describe, expect, it } from 'vitest';
import { canCheck, cardsWithoutLeaves, toggleCard, toggleLeaf } from './answer';

describe('the answer', () => {
  it('picks a card and un-picks it with its leaves', () => {
    const picked = toggleLeaf(toggleCard([], 'god-class'), 'god-class', 'manual.split');

    expect(picked).toEqual([{ card: 'god-class', leaves: ['manual.split'] }]);
    expect(toggleCard(picked, 'god-class')).toEqual([]);
  });

  it('ticks and unticks a leaf, and leaves a card that is not picked alone', () => {
    const answer = toggleCard([], 'god-class');

    expect(toggleLeaf(toggleLeaf(answer, 'god-class', 'rule.limits'), 'god-class', 'rule.limits')).toEqual(answer);
    expect(toggleLeaf(answer, 'money-in-float', 'manual.representation')).toEqual(answer);
  });
});

describe('the check rule', () => {
  it('allows checking an answer that picked nothing', () => {
    expect(canCheck([])).toBe(true);
  });

  it('needs a leaf for every picked card', () => {
    const answer = toggleLeaf(toggleCard(toggleCard([], 'god-class'), 'money-in-float'), 'god-class', 'manual.split');

    expect(canCheck(answer)).toBe(false);
    expect(cardsWithoutLeaves(answer)).toEqual(['money-in-float']);
    expect(canCheck(toggleLeaf(answer, 'money-in-float', 'manual.representation'))).toBe(true);
  });
});
