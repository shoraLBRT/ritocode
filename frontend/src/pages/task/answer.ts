import type { Pick } from '../../api';

/**
 * The learner's answer while they work: the picked cards, in the order they were picked, each with
 * its leaves. Pure functions, so the rules of steps 1 and 2 are tested without a screen.
 */
export type Answer = readonly Pick[];

/** Picks a card, or un-picks it with its leaves. */
export function toggleCard(answer: Answer, card: string): Answer {
  return answer.some((pick) => pick.card === card)
    ? answer.filter((pick) => pick.card !== card)
    : [...answer, { card, leaves: [] }];
}

/** Ticks a leaf for a picked card, or unticks it. A card that is not picked is left alone. */
export function toggleLeaf(answer: Answer, card: string, leaf: string): Answer {
  return answer.map((pick) =>
    pick.card !== card
      ? pick
      : { card, leaves: pick.leaves.includes(leaf) ? pick.leaves.filter((each) => each !== leaf) : [...pick.leaves, leaf] },
  );
}

/**
 * The check rule (docs/SPEC.md §4.4): every picked card needs at least one leaf before the answer
 * can be checked. Picking nothing is an answer, and can be checked.
 */
export function cardsWithoutLeaves(answer: Answer): string[] {
  return answer.filter((pick) => pick.leaves.length === 0).map((pick) => pick.card);
}

export function canCheck(answer: Answer): boolean {
  return cardsWithoutLeaves(answer).length === 0;
}
