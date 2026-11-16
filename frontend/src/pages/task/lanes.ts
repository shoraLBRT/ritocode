import type { AttemptResult } from '../../api';

/** One line of a revealed result: a card of the key, found or missed, or an extra pick. */
export type Line = AttemptResult['cards'][number];

export type Outcome = Line['outcome'];

/** The three lanes of the review's centre, in their order, each with its lines in the result's order. */
export function lanes(lines: readonly Line[]): { readonly outcome: Outcome; readonly lines: readonly Line[] }[] {
  return (['found', 'missed', 'extra'] as const).map((outcome) => ({ outcome, lines: lines.filter((line) => line.outcome === outcome) }));
}

/**
 * The one conclusion on a lane card's face (docs/DESIGN.md › The review, #256): for a found card,
 * whether the treatment fits — any one of the author's leaves is enough — or was not chosen; for a
 * missed card, the author's first leaf and how many more; for an extra pick, that the key does not
 * see it here. Everything else opens with the card.
 */
export type FaceLine =
  | { readonly kind: 'treated' | 'mistreated' | 'untreated' | 'absent' }
  | { readonly kind: 'cure'; readonly leaf: string | undefined; readonly more: number };

export function faceLine(line: Line, picked: readonly string[]): FaceLine {
  if (line.outcome === 'extra') {
    return { kind: 'absent' };
  }

  const key = line.keyLeaves ?? [];
  if (line.outcome === 'missed') {
    return { kind: 'cure', leaf: key[0], more: Math.max(0, key.length - 1) };
  }

  const right = line.treatment?.matchedLeaves ?? picked.filter((leaf) => key.includes(leaf));
  if (right.length > 0) {
    return { kind: 'treated' };
  }
  return picked.length === 0 ? { kind: 'untreated' } : { kind: 'mistreated' };
}
