import type { Pick, TaskDetail, TreatmentTree } from '../../api';
import type { Answer } from './answer';

/**
 * The answer kept in the browser while the learner works (docs/SPEC.md §4.6), so that it survives
 * the sign-in round trip and a reload, and is checked once they are back.
 *
 * Kept in the tab's session storage: the provider's pages open in the same tab and return to it,
 * and nothing lingers once the tab is closed. One entry per task, written on every change and
 * removed once the answer is checked.
 *
 * A saved answer is restored only if it still fits the task as the API serves it now — every card
 * one the task offers, every leaf one of the tree, nothing twice. That stands in for keying by the
 * content revision, which `GET /tasks/{slug}` does not carry: an answer that no longer fits is
 * treated as lost, and one that fits is one the server will accept.
 */
export interface Draft {
  readonly answer: Answer;
  readonly step: 1 | 2;
}

const KEY_PREFIX = 'ritocode:answer:';

/** Bumped when the stored shape changes, so an old entry reads as no entry. */
const VERSION = 1;

/** The tab's session storage, or null where the browser will not give it — blocked storage, no window. */
export function draftStorage(): Storage | null {
  try {
    return typeof window === 'undefined' ? null : window.sessionStorage;
  } catch {
    return null;
  }
}

/** Keeps the answer; false when the browser would not, so the page can say so before sign-in. */
export function saveDraft(storage: Storage | null, task: string, draft: Draft): boolean {
  if (storage === null) {
    return false;
  }

  try {
    storage.setItem(KEY_PREFIX + task, JSON.stringify({ version: VERSION, step: draft.step, picks: draft.answer }));
    return true;
  } catch {
    // Full, or refused in a private mode.
    return false;
  }
}

export function clearDraft(storage: Storage | null, task: string): void {
  try {
    storage?.removeItem(KEY_PREFIX + task);
  } catch {
    // Nothing kept that could be removed.
  }
}

/** The saved answer to `task`, if there is one and it still fits; null otherwise. */
export function restoreDraft(storage: Storage | null, task: TaskDetail, tree: TreatmentTree): Draft | null {
  let text: string | null;
  try {
    text = storage?.getItem(KEY_PREFIX + task.slug) ?? null;
  } catch {
    return null;
  }

  if (text === null) {
    return null;
  }

  try {
    return fit(JSON.parse(text) as unknown, task, tree);
  } catch {
    return null;
  }
}

function fit(value: unknown, task: TaskDetail, tree: TreatmentTree): Draft | null {
  if (!isRecord(value) || value.version !== VERSION || (value.step !== 1 && value.step !== 2) || !Array.isArray(value.picks)) {
    return null;
  }

  const offered = new Set(task.cards.map((card) => card.slug));
  const leaves = new Set(tree.branches.flatMap((branch) => branch.leaves.map((leaf) => leaf.id)));
  const picked = new Set<string>();
  const answer: Pick[] = [];

  for (const pick of value.picks as unknown[]) {
    if (!isRecord(pick) || typeof pick.card !== 'string' || !offered.has(pick.card) || picked.has(pick.card) || !Array.isArray(pick.leaves)) {
      return null;
    }

    const ticked = pick.leaves as unknown[];
    if (!ticked.every((leaf): leaf is string => typeof leaf === 'string' && leaves.has(leaf)) || new Set(ticked).size !== ticked.length) {
      return null;
    }

    picked.add(pick.card);
    answer.push({ card: pick.card, leaves: ticked });
  }

  return { answer, step: value.step };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
