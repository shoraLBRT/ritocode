import { difficulties } from '../../api';
import type { Difficulty, ProblemCatalogue, Progress, StudyMark, TaskSummary } from '../../api';
import { moscowDay } from '../home/arena';

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

export interface Study {
  /** Published cards marked studied. */
  readonly studied: number;
  /** Published cards. */
  readonly total: number;
  /** Studied and total per class, in the catalogue's order. */
  readonly byClass: readonly { readonly class: string; readonly name: string; readonly studied: number; readonly total: number }[];
  /**
   * The cards studied over time, cumulative: one point per Moscow day a card was marked, from the
   * first mark, each with the count studied by the end of that day. Empty with no mark.
   */
  readonly timeline: readonly { readonly day: number; readonly count: number }[];
}

/**
 * Study progress (ADR 0018): the published cards marked studied, in all and per class, and how the
 * count grew. Only marks on a card of the catalogue count — a retired card's are not served, and a
 * card found in a task is not studied by that.
 */
export function studyProgress(catalogue: ProblemCatalogue, marks: readonly StudyMark[]): Study {
  const classOf = new Map(catalogue.cards.map((card) => [card.slug, card.class]));
  const counted = new Map<string, number>();

  for (const mark of marks) {
    if (classOf.has(mark.card) && !counted.has(mark.card)) {
      counted.set(mark.card, moscowDay(new Date(mark.studiedAt)));
    }
  }

  const byClass = catalogue.classes.map((item) => {
    const cards = catalogue.cards.filter((card) => card.class === item.id);
    return { class: item.id, name: item.name, studied: cards.filter((card) => counted.has(card.slug)).length, total: cards.length };
  });

  const perDay = new Map<number, number>();
  for (const day of counted.values()) {
    perDay.set(day, (perDay.get(day) ?? 0) + 1);
  }

  let count = 0;
  const timeline = [...perDay.entries()]
    .sort(([a], [b]) => a - b)
    .map(([day, marked]) => {
      count += marked;
      return { day, count };
    });

  return { studied: counted.size, total: catalogue.cards.length, byClass, timeline };
}

/** The study chart's own box, which the page stretches to the card's width. */
export const CHART_WIDTH = 600;
export const CHART_HEIGHT = 140;
/** Room above the line's highest step, so its stroke is not cut at the top. */
const CHART_TOP = 6;

/**
 * The study chart's line, in its own box: a step up on each day a card was marked, held flat to
 * today at the right edge. The first mark's day is the left edge; a first mark made today spans the
 * box. The count scales to the last step at the top. Nothing to draw without a mark.
 */
export function chartPath(timeline: Study['timeline'], today: number): string {
  const first = timeline[0];
  if (first === undefined) {
    return '';
  }

  const top = timeline.at(-1)?.count ?? first.count;
  const span = Math.max(today - first.day, 1);
  const x = (day: number) => String(Math.round(((day - first.day) / span) * CHART_WIDTH * 100) / 100);
  const y = (count: number) => String(Math.round((CHART_HEIGHT - (count / top) * (CHART_HEIGHT - CHART_TOP)) * 100) / 100);

  let path = `M0 ${y(first.count)}`;
  for (const point of timeline.slice(1)) {
    path += ` H${x(point.day)} V${y(point.count)}`;
  }
  return `${path} H${String(CHART_WIDTH)}`;
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
