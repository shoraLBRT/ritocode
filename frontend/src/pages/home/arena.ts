import type { AttemptSummary, TaskSummary } from '../../api';

/**
 * The arithmetic of the home page (docs/SPEC.md §4.1, §4.7): which task is today's, and the activity
 * and streak of a learner. Pure, so the page and its tests agree on every date.
 */

const DAY_MS = 24 * 60 * 60 * 1000;

/** Moscow is UTC+3 all year: Russia has not moved its clocks since 2014. */
const MOSCOW_OFFSET_MS = 3 * 60 * 60 * 1000;

/** How many days the activity map shows: twelve weeks. */
export const ACTIVITY_DAYS = 84;

/** The day `instant` falls on in Moscow, counted from 1970-01-01. */
export function moscowDay(instant: Date): number {
  return Math.floor((instant.getTime() + MOSCOW_OFFSET_MS) / DAY_MS);
}

/**
 * Today's task: chosen by the Moscow day among the tasks the learner has not solved — all of them
 * for a guest, whose `solved` is null, and for a learner who has solved everything. The same list
 * on the same day always gives the same task; it changes at midnight Moscow time.
 */
export function dailyTask(tasks: readonly TaskSummary[], now: Date): TaskSummary | null {
  const unsolved = tasks.filter((task) => task.solved !== true);
  const pool = unsolved.length > 0 ? unsolved : tasks;

  if (pool.length === 0) {
    return null;
  }

  return pool[moscowDay(now) % pool.length] ?? null;
}

export interface Activity {
  /** Attempts submitted on each of the last {@link ACTIVITY_DAYS} days, oldest first, today last. */
  readonly days: readonly number[];
  /** Days in a row with an attempt, up to today; today without one does not break it yet. */
  readonly streak: number;
}

/** Counts submitted attempts by Moscow day; an attempt never submitted is not practice yet. */
export function activity(attempts: readonly AttemptSummary[], now: Date): Activity {
  const today = moscowDay(now);
  const perDay = new Map<number, number>();

  for (const attempt of attempts) {
    if (attempt.submittedAt !== null) {
      const day = moscowDay(new Date(attempt.submittedAt));
      perDay.set(day, (perDay.get(day) ?? 0) + 1);
    }
  }

  const days = Array.from({ length: ACTIVITY_DAYS }, (_, index) => perDay.get(today - ACTIVITY_DAYS + 1 + index) ?? 0);

  let day = (perDay.get(today) ?? 0) > 0 ? today : today - 1;
  let streak = 0;

  while ((perDay.get(day) ?? 0) > 0) {
    streak += 1;
    day -= 1;
  }

  return { days, streak };
}

/** The shade of a day on the activity map: none, one, two, three or four, five and more. */
export function activityLevel(count: number): 0 | 1 | 2 | 3 | 4 {
  if (count >= 5) return 4;
  if (count >= 3) return 3;
  return count as 0 | 1 | 2;
}

/** The earliest instant the activity map needs attempts from: the start of its first day in Moscow. */
export function activitySince(now: Date): Date {
  return new Date((moscowDay(now) - ACTIVITY_DAYS + 1) * DAY_MS - MOSCOW_OFFSET_MS);
}
