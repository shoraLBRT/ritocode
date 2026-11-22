import type { AttemptSummary, ProblemCard, StudyMark, TaskSummary } from '../../api';

/**
 * The arithmetic of the home page (docs/SPEC.md §4.1, §4.7): which task is today's, and the activity
 * and streak of a learner. Pure, so the page and its tests agree on every date.
 *
 * A day's practice is its submitted attempts and the cards marked studied on it (ADR 0018): either
 * makes the day active, and the day's count is both. A mark taken back is no longer served, so it
 * no longer counts.
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

/** The instant the next Moscow day starts — when today's task gives way to the next one. */
export function nextMoscowMidnight(now: Date): Date {
  return new Date((moscowDay(now) + 1) * DAY_MS - MOSCOW_OFFSET_MS);
}

/**
 * The time left as `HH:MM:SS`, counting down by whole seconds the way a clock does: `00:00:00` is
 * shown for the last second, and a whole day reads `23:59:59`. A negative time reads as zero.
 */
export function formatCountdown(ms: number): string {
  const total = Math.min(DAY_MS / 1000 - 1, Math.max(0, Math.floor(ms / 1000)));
  const parts = [Math.floor(total / 3600), Math.floor(total / 60) % 60, total % 60];
  return parts.map((part) => String(part).padStart(2, '0')).join(':');
}

/**
 * Whether a task is still the learner's to solve: new or tried (ADR 0017). Solved is done, and gave
 * up has its answer open — practice, not a challenge. For a guest, whose `status` is null, every task.
 */
export function isOpen(task: TaskSummary): boolean {
  return task.status === null || task.status === 'new' || task.status === 'tried';
}

/**
 * Today's task: chosen by the Moscow day among the tasks still open to the learner (`isOpen`) — all
 * of them for a guest, and for a learner who has none open. The same list on the same day always
 * gives the same task; it changes at midnight Moscow time.
 */
export function dailyTask(tasks: readonly TaskSummary[], now: Date): TaskSummary | null {
  const open = tasks.filter(isOpen);
  const pool = open.length > 0 ? open : tasks;

  if (pool.length === 0) {
    return null;
  }

  return pool[moscowDay(now) % pool.length] ?? null;
}

/** How many tasks and problems the home page suggests; the rest are a link away. */
export const SUGGESTED_TASKS = 6;
export const SUGGESTED_PROBLEMS = 4;

/**
 * A pseudo-random order fixed by the Moscow day: the same list on the same day comes back in the
 * same order, so the prerendered page and the application agree, and the order changes at midnight.
 * `salt` keeps two lists shuffled on the same day independent of each other.
 */
export function shuffleByDay<T>(items: readonly T[], now: Date, salt: number): T[] {
  // mulberry32, seeded by the day and the salt.
  let seed = (Math.imul(moscowDay(now), 2654435761) + salt) >>> 0;
  const next = () => {
    seed = (seed + 0x6d2b79f5) >>> 0;
    let value = seed;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };

  const result = [...items];
  for (let index = result.length - 1; index > 0; index -= 1) {
    const other = Math.floor(next() * (index + 1));
    const held = result[index] as T;
    result[index] = result[other] as T;
    result[other] = held;
  }
  return result;
}

/**
 * The tasks the home page suggests: the learner's open ones first (new or tried), then those whose
 * answer is open, then solved ones, if there are too few, each group in the day's order — for a
 * guest, whose `status` is null, simply the day's pick. Today's task is left out: it has a card of
 * its own.
 */
export function suggestedTasks(tasks: readonly TaskSummary[], now: Date, today: string | null): TaskSummary[] {
  const order = shuffleByDay(
    tasks.filter((task) => task.slug !== today),
    now,
    1,
  );
  return [
    ...order.filter(isOpen),
    ...order.filter((task) => task.status === 'gaveUp'),
    ...order.filter((task) => task.status === 'solved'),
  ].slice(0, SUGGESTED_TASKS);
}

/** A few problems to read today, in the day's order. */
export function suggestedProblems<C extends Pick<ProblemCard, 'slug'>>(cards: readonly C[], now: Date): C[] {
  return shuffleByDay(cards, now, 2).slice(0, SUGGESTED_PROBLEMS);
}

export interface Activity {
  /** Attempts submitted and cards marked studied on each of the last {@link ACTIVITY_DAYS} days, oldest first, today last. */
  readonly days: readonly number[];
  /** The cards marked studied on each of those days: the part of {@link days} that is not attempts. */
  readonly marks: readonly number[];
  /** Days in a row with practice, up to today; today without any does not break it yet. */
  readonly streak: number;
}

/** Counts per Moscow day: submitted attempts (one never submitted is not practice yet) and study marks. */
function perDay(attempts: readonly AttemptSummary[], marks: readonly StudyMark[]): { attempts: Map<number, number>; marks: Map<number, number> } {
  const byDay = { attempts: new Map<number, number>(), marks: new Map<number, number>() };

  for (const attempt of attempts) {
    if (attempt.submittedAt !== null) {
      const day = moscowDay(new Date(attempt.submittedAt));
      byDay.attempts.set(day, (byDay.attempts.get(day) ?? 0) + 1);
    }
  }

  for (const mark of marks) {
    const day = moscowDay(new Date(mark.studiedAt));
    byDay.marks.set(day, (byDay.marks.get(day) ?? 0) + 1);
  }

  return byDay;
}

/** The streak and twelve weeks of practice: submitted attempts and study marks, by Moscow day. */
export function activity(attempts: readonly AttemptSummary[], now: Date, marks: readonly StudyMark[] = []): Activity {
  const today = moscowDay(now);
  const byDay = perDay(attempts, marks);
  const count = (day: number) => (byDay.attempts.get(day) ?? 0) + (byDay.marks.get(day) ?? 0);
  const first = today - ACTIVITY_DAYS + 1;

  const days = Array.from({ length: ACTIVITY_DAYS }, (_, index) => count(first + index));
  const marked = Array.from({ length: ACTIVITY_DAYS }, (_, index) => byDay.marks.get(first + index) ?? 0);

  let day = count(today) > 0 ? today : today - 1;
  let streak = 0;

  while (count(day) > 0) {
    streak += 1;
    day -= 1;
  }

  return { days, marks: marked, streak };
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

/** How many weeks the profile's year of activity shows: 53 columns of seven days, today last. */
export const YEAR_WEEKS = 53;

/** The days the year's map shows. */
export const YEAR_DAYS = YEAR_WEEKS * 7;

/** The columns a month's label takes, so two labels never meet. */
const MONTH_LABEL_COLUMNS = 3;

/** The calendar date of a Moscow day, as an instant at UTC midnight: read it with `timeZone: 'UTC'`. */
export function moscowDate(day: number): Date {
  return new Date(day * DAY_MS);
}

export interface YearActivity {
  /** The Moscow day of the map's first cell; cell `i` is day `firstDay + i`, today the last. */
  readonly firstDay: number;
  /** Attempts submitted and cards marked studied on each of the {@link YEAR_DAYS} days, oldest first, today last. */
  readonly days: readonly number[];
  /** The cards marked studied on each of those days. */
  readonly marks: readonly number[];
  /** Attempts submitted inside the window. */
  readonly total: number;
  /** Cards marked studied inside the window. */
  readonly studied: number;
  /** Days inside the window with practice: a submitted attempt or a study mark. */
  readonly activeDays: number;
  /** The longest run of days in a row with practice inside the window. */
  readonly longestStreak: number;
  /** Where each month's label starts: the column that holds its first day, and that day. */
  readonly months: readonly { readonly column: number; readonly day: number }[];
}

/**
 * A year of activity for the profile, by the home page's rules: Moscow days, submitted attempts and
 * study marks. The columns are weeks of seven consecutive days, the last one ending today, as on the home
 * page. A month is labelled at the column holding its first day; the window's first month too when
 * its label has room before the next one, and none so close to the end that it would not fit.
 */
export function yearActivity(attempts: readonly AttemptSummary[], now: Date, marks: readonly StudyMark[] = []): YearActivity {
  const today = moscowDay(now);
  const firstDay = today - YEAR_DAYS + 1;
  const byDay = perDay(attempts, marks);
  const tried = Array.from({ length: YEAR_DAYS }, (_, index) => byDay.attempts.get(firstDay + index) ?? 0);
  const marked = Array.from({ length: YEAR_DAYS }, (_, index) => byDay.marks.get(firstDay + index) ?? 0);
  const days = tried.map((count, index) => count + (marked[index] ?? 0));

  let total = 0;
  let studied = 0;
  let activeDays = 0;
  let longestStreak = 0;
  let run = 0;

  days.forEach((count, index) => {
    total += tried[index] ?? 0;
    studied += marked[index] ?? 0;
    run = count > 0 ? run + 1 : 0;
    if (count > 0) activeDays += 1;
    longestStreak = Math.max(longestStreak, run);
  });

  const months: { column: number; day: number }[] = [];

  for (let index = 0; index < YEAR_DAYS; index += 1) {
    const day = firstDay + index;
    const column = Math.floor(index / 7);
    if (moscowDate(day).getUTCDate() === 1 && column <= YEAR_WEEKS - MONTH_LABEL_COLUMNS) {
      months.push({ column, day });
    }
  }

  if ((months[0]?.column ?? YEAR_WEEKS) >= MONTH_LABEL_COLUMNS) {
    months.unshift({ column: 0, day: firstDay });
  }

  return { firstDay, days, marks: marked, total, studied, activeDays, longestStreak, months };
}

/** The earliest instant the year's map needs attempts from: the start of its first day in Moscow. */
export function yearSince(now: Date): Date {
  return new Date((moscowDay(now) - YEAR_DAYS + 1) * DAY_MS - MOSCOW_OFFSET_MS);
}
