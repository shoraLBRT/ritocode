import type { AttemptResult } from '../../api';

/** One line of a revealed result: a card of the key, found or missed, or an extra pick. */
export type Line = AttemptResult['cards'][number];

/**
 * How a treatment leaf reads on the face of a card in the review (docs/DESIGN.md › The review):
 * chosen and in the key, chosen though the key does not list it, in the key but not chosen — which
 * is not a mistake, as any one of the author's leaves is enough — or a leaf of an extra pick, which
 * the key has nothing to say about.
 */
export type LeafMark = 'right' | 'extra' | 'unchosen' | 'plain';

export interface FaceLeaf {
  readonly leaf: string;
  readonly mark: LeafMark;
}

/** The leaves on a card's face: the right ones first, then the learner's extra ones, then the author's others. */
export function faceLeaves(line: Line, picked: readonly string[]): FaceLeaf[] {
  if (line.outcome === 'extra') {
    return picked.map((leaf) => ({ leaf, mark: 'plain' }));
  }

  const key = line.keyLeaves ?? [];
  if (line.outcome === 'missed') {
    return key.map((leaf) => ({ leaf, mark: 'unchosen' }));
  }

  const right = line.treatment?.matchedLeaves ?? picked.filter((leaf) => key.includes(leaf));
  const wrong = line.treatment?.wrongLeaves ?? picked.filter((leaf) => !key.includes(leaf));
  return [
    ...right.map((leaf): FaceLeaf => ({ leaf, mark: 'right' })),
    ...wrong.map((leaf): FaceLeaf => ({ leaf, mark: 'extra' })),
    ...key.filter((leaf) => !right.includes(leaf)).map((leaf): FaceLeaf => ({ leaf, mark: 'unchosen' })),
  ];
}

export type Outcome = Line['outcome'];

/** The three lanes of the review's centre, in their order, each with its lines in the result's order. */
export function lanes(lines: readonly Line[]): { readonly outcome: Outcome; readonly lines: readonly Line[] }[] {
  return (['found', 'missed', 'extra'] as const).map((outcome) => ({ outcome, lines: lines.filter((line) => line.outcome === outcome) }));
}
