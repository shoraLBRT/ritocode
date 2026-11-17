import { difficulties } from '../../api';
import type { Difficulty, Progress, TaskSummary } from '../../api';

/**
 * The profile's arithmetic (docs/SPEC.md §4.7): what the task list and the progress add up to. Pure,
 * so the page and its tests agree. Everything is counted from data the API already gives.
 */

export interface Solved {
  readonly solved: number;
  readonly total: number;
  /** Solved and total per difficulty, easy first. */
  readonly byDifficulty: readonly { readonly difficulty: Difficulty; readonly solved: number; readonly total: number }[];
  /** Tasks started and not solved yet, the answer still closed. */
  readonly tried: number;
  /** Tasks whose answer the learner revealed before solving them (ADR 0017). */
  readonly gaveUp: number;
}

/** The tasks solved out of all, by difficulty, with the started and the revealed counted aside. */
export function solvedTasks(tasks: readonly TaskSummary[]): Solved {
  const byDifficulty = difficulties.map((difficulty) => {
    const level = tasks.filter((task) => task.difficulty === difficulty);
    return { difficulty, solved: level.filter((task) => task.status === 'solved').length, total: level.length };
  });

  return {
    solved: byDifficulty.reduce((sum, level) => sum + level.solved, 0),
    total: tasks.length,
    byDifficulty,
    tried: tasks.filter((task) => task.status === 'tried').length,
    gaveUp: tasks.filter((task) => task.status === 'gaveUp').length,
  };
}

export interface Diagnosis {
  readonly met: number;
  readonly found: number;
  readonly treatedRight: number;
  readonly pickedWhenAbsent: number;
}

/** The diagnosis tiles: summed over the classes, and the wrong picks over the cards. */
export function diagnosis(progress: Progress): Diagnosis {
  return {
    met: progress.classes.reduce((sum, item) => sum + item.met, 0),
    found: progress.classes.reduce((sum, item) => sum + item.found, 0),
    treatedRight: progress.classes.reduce((sum, item) => sum + item.treatedRight, 0),
    pickedWhenAbsent: progress.cards.reduce((sum, card) => sum + card.pickedWhenAbsent, 0),
  };
}

/**
 * The month and year of a date in the reader's zone, as a phrase after «с» wants it: in Russian the
 * month in the genitive («марта 2026 г.»), which `Intl` gives only next to a day — so the date is
 * formatted with its day and the day is dropped with the separator that follows or precedes it.
 */
export function monthAndYear(value: string, locale: string): string {
  const parts = new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', year: 'numeric' }).formatToParts(new Date(value));
  const day = parts.findIndex((part) => part.type === 'day');
  if (day === -1) {
    return parts.map((part) => part.value).join('');
  }

  const separator = parts[day + 1]?.type === 'literal' ? day + 1 : day - 1;
  return parts
    .filter((_, index) => index !== day && index !== separator)
    .map((part) => part.value)
    .join('')
    .trim();
}

/** A share as a whole percentage, 0–100; nothing of nothing is 0. */
export function percent(part: number, whole: number): number {
  return whole === 0 ? 0 : Math.round((part / whole) * 100);
}
