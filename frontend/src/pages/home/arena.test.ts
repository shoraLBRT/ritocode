import { describe, expect, it } from 'vitest';
import type { AttemptSummary, ProblemCard, StudyMark, TaskStatus, TaskSummary } from '../../api';
import {
  ACTIVITY_DAYS,
  activity,
  activityLevel,
  activitySince,
  dailyTask,
  formatCountdown,
  moscowDate,
  moscowDay,
  nextMoscowMidnight,
  shuffleByDay,
  SUGGESTED_PROBLEMS,
  SUGGESTED_TASKS,
  suggestedProblems,
  suggestedTasks,
  YEAR_DAYS,
  YEAR_WEEKS,
  yearActivity,
  yearSince,
} from './arena';

function taskOf(slug: string, status: TaskStatus | null): TaskSummary {
  return { slug, title: slug, difficulty: 'easy', status };
}

function submittedAt(iso: string | null): AttemptSummary {
  return {
    id: iso ?? 'open',
    task: 'demo',
    startedAt: iso ?? '2026-10-28T09:00:00Z',
    step: 'treatment',
    submittedAt: iso,
    practice: false,
    score: null,
    maxScore: null,
  };
}

describe('the Moscow day', () => {
  it('turns at midnight Moscow time, which is 21:00 UTC', () => {
    const before = moscowDay(new Date('2026-10-27T20:59:59Z'));
    const after = moscowDay(new Date('2026-10-27T21:00:00Z'));

    expect(after - before).toBe(1);
  });
});

describe('the countdown to the next daily task', () => {
  it('ends at the next midnight in Moscow, 21:00 UTC', () => {
    expect(nextMoscowMidnight(new Date('2026-11-04T18:47:20Z')).toISOString()).toBe('2026-11-04T21:00:00.000Z');
    expect(nextMoscowMidnight(new Date('2026-11-04T21:00:00Z')).toISOString()).toBe('2026-11-05T21:00:00.000Z');
    expect(nextMoscowMidnight(new Date('2026-11-04T23:30:00Z')).toISOString()).toBe('2026-11-05T21:00:00.000Z');
  });

  it('reads as hours, minutes and seconds, counting down like a clock', () => {
    expect(formatCountdown(((5 * 60 + 12) * 60 + 40) * 1000)).toBe('05:12:40');
    expect(formatCountdown(24 * 60 * 60 * 1000 - 1)).toBe('23:59:59');
    expect(formatCountdown(24 * 60 * 60 * 1000)).toBe('23:59:59');
    expect(formatCountdown(1999)).toBe('00:00:01');
    expect(formatCountdown(999)).toBe('00:00:00');
    expect(formatCountdown(-5000)).toBe('00:00:00');
  });
});

describe('what the home page suggests', () => {
  const monday = new Date('2026-11-09T09:00:00Z');
  const tuesday = new Date('2026-11-10T09:00:00Z');
  const many = Array.from({ length: 12 }, (_, index): TaskSummary => ({
    slug: `task-${String(index)}`,
    title: `Task ${String(index)}`,
    difficulty: 'easy',
    status: index < 2 ? 'new' : index < 4 ? 'tried' : index < 6 ? 'solved' : index < 8 ? 'gaveUp' : null,
  }));

  it('shuffles by the Moscow day: the same day gives the same order, another day another', () => {
    const items = Array.from({ length: 20 }, (_, index) => index);
    const order = shuffleByDay(items, monday, 1);

    expect([...order].sort((a, b) => a - b)).toEqual(items);
    expect(shuffleByDay(items, new Date('2026-11-09T20:59:59Z'), 1)).toEqual(order);
    expect(shuffleByDay(items, tuesday, 1)).not.toEqual(order);
    expect(shuffleByDay(items, monday, 2)).not.toEqual(order);
  });

  it('suggests open tasks first, then those with the answer open, then solved ones, leaves out the daily task, and stops at the limit', () => {
    const learner = many.slice(0, 8);
    const suggested = suggestedTasks(learner, monday, 'task-0');

    expect(suggested).toHaveLength(SUGGESTED_TASKS);
    expect(suggested.map((task) => task.slug)).not.toContain('task-0');
    expect(suggested.slice(0, 3).every((task) => task.status === 'new' || task.status === 'tried')).toBe(true);
    expect(suggested.slice(3, 5).every((task) => task.status === 'gaveUp')).toBe(true);
    expect(suggested[5]?.status).toBe('solved');
  });

  it('gives a guest a pick of all tasks for the day', () => {
    const guest = many.map((task) => ({ ...task, status: null }));
    const suggested = suggestedTasks(guest, monday, null);

    expect(suggested).toHaveLength(SUGGESTED_TASKS);
    expect(suggestedTasks(guest, monday, null)).toEqual(suggested);
  });

  it('picks a few problems to read', () => {
    const cards = Array.from({ length: 10 }, (_, index) => ({ slug: `card-${String(index)}` }) as ProblemCard);

    expect(suggestedProblems(cards, monday)).toHaveLength(SUGGESTED_PROBLEMS);
    expect(suggestedProblems(cards.slice(0, 2), monday)).toHaveLength(2);
  });
});

describe('the daily task', () => {
  const tasks = [taskOf('a', 'new'), taskOf('b', 'solved'), taskOf('c', 'tried'), taskOf('d', 'gaveUp')];

  it('is the same all day and changes at midnight', () => {
    const morning = dailyTask(tasks, new Date('2026-10-28T06:00:00Z'));
    const evening = dailyTask(tasks, new Date('2026-10-28T20:00:00Z'));
    const nextDay = dailyTask(tasks, new Date('2026-10-28T21:30:00Z'));

    expect(evening).toEqual(morning);
    expect(nextDay).not.toEqual(morning);
  });

  it('is never a task the learner has solved or given up while one is open', () => {
    for (let hour = 0; hour < 24 * 10; hour += 6) {
      const day = dailyTask(tasks, new Date(Date.UTC(2026, 9, 1, hour)));
      expect(['new', 'tried']).toContain(day?.status);
    }
  });

  it('comes from every task for a guest, and for a learner who solved them all', () => {
    const guest = [taskOf('a', null), taskOf('b', null)];
    const done = [taskOf('a', 'solved'), taskOf('b', 'gaveUp')];

    expect(dailyTask(guest, new Date('2026-10-28T12:00:00Z'))).not.toBeNull();
    expect(dailyTask(done, new Date('2026-10-28T12:00:00Z'))).not.toBeNull();
  });

  it('is null without tasks', () => {
    expect(dailyTask([], new Date())).toBeNull();
  });
});

function markedAt(iso: string, card = iso): StudyMark {
  return { card, studiedAt: iso };
}

describe('activity and the streak', () => {
  const now = new Date('2026-10-28T12:00:00Z');

  it('counts submitted attempts by Moscow day, today last', () => {
    const { days } = activity(
      [submittedAt('2026-10-28T08:00:00Z'), submittedAt('2026-10-28T10:00:00Z'), submittedAt('2026-10-27T22:00:00Z'), submittedAt(null)],
      now,
    );

    expect(days).toHaveLength(ACTIVITY_DAYS);
    // 22:00 UTC on the 27th is already the 28th in Moscow.
    expect(days.at(-1)).toBe(3);
    expect(days.slice(0, -1).every((count) => count === 0)).toBe(true);
  });

  it('runs back from today while every day has an attempt', () => {
    const { streak } = activity(
      [submittedAt('2026-10-28T08:00:00Z'), submittedAt('2026-10-27T08:00:00Z'), submittedAt('2026-10-26T08:00:00Z'), submittedAt('2026-10-24T08:00:00Z')],
      now,
    );

    expect(streak).toBe(3);
  });

  it('is not broken by today until today ends', () => {
    const { streak } = activity([submittedAt('2026-10-27T08:00:00Z'), submittedAt('2026-10-26T08:00:00Z')], now);

    expect(streak).toBe(2);
  });

  it('is broken by a day without an attempt', () => {
    const { streak } = activity([submittedAt('2026-10-26T08:00:00Z')], now);

    expect(streak).toBe(0);
  });

  it('counts the days of study marks only, as practice', () => {
    const { days, marks, streak } = activity([], now, [markedAt('2026-10-28T08:00:00Z'), markedAt('2026-10-27T08:00:00Z'), markedAt('2026-10-27T09:00:00Z')]);

    expect(days.slice(-2)).toEqual([2, 1]);
    expect(marks.slice(-2)).toEqual([2, 1]);
    expect(streak).toBe(2);
  });

  it('counts attempts and marks of one day together, and a day of either keeps the streak', () => {
    const { days, marks, streak } = activity(
      [submittedAt('2026-10-28T08:00:00Z'), submittedAt('2026-10-26T08:00:00Z')],
      now,
      [markedAt('2026-10-28T09:00:00Z'), markedAt('2026-10-27T09:00:00Z')],
    );

    expect(days.slice(-3)).toEqual([1, 1, 2]);
    expect(marks.slice(-3)).toEqual([0, 1, 1]);
    expect(streak).toBe(3);
  });

  it('keeps attempts alone as they were, with no mark among the days', () => {
    const { marks, streak } = activity([submittedAt('2026-10-28T08:00:00Z')], now);

    expect(marks.every((count) => count === 0)).toBe(true);
    expect(streak).toBe(1);
  });

  it('shades a day by how many attempts it had', () => {
    expect([0, 1, 2, 3, 4, 5, 9].map(activityLevel)).toEqual([0, 1, 2, 3, 3, 4, 4]);
  });

  it('needs attempts from the start of its first day', () => {
    const since = activitySince(now);

    expect(moscowDay(since)).toBe(moscowDay(now) - ACTIVITY_DAYS + 1);
    expect(moscowDay(new Date(since.getTime() - 1))).toBe(moscowDay(now) - ACTIVITY_DAYS);
  });
});

describe('a year of activity', () => {
  const now = new Date('2026-10-28T12:00:00Z');

  it('lays 53 weeks of days out, today last, and counts the submitted attempts in them', () => {
    const year = yearActivity(
      [
        submittedAt('2026-10-28T08:00:00Z'),
        submittedAt('2026-10-27T22:00:00Z'),
        submittedAt('2026-03-02T08:00:00Z'),
        submittedAt(null),
        // Before the window: not counted.
        submittedAt('2025-10-01T08:00:00Z'),
      ],
      now,
    );

    expect(year.days).toHaveLength(YEAR_WEEKS * 7);
    expect(year.firstDay).toBe(moscowDay(now) - YEAR_DAYS + 1);
    expect(year.days.at(-1)).toBe(2);
    expect(year.total).toBe(3);
    expect(year.activeDays).toBe(2);
  });

  it('finds the longest run of days in a row inside the window', () => {
    const year = yearActivity(
      [
        submittedAt('2026-05-01T08:00:00Z'),
        submittedAt('2026-05-02T08:00:00Z'),
        submittedAt('2026-05-03T08:00:00Z'),
        submittedAt('2026-05-03T09:00:00Z'),
        submittedAt('2026-10-27T08:00:00Z'),
        submittedAt('2026-10-28T08:00:00Z'),
      ],
      now,
    );

    expect(year.longestStreak).toBe(3);
    expect(year.activeDays).toBe(5);
  });

  it('counts study marks as practice of their days, apart from the attempts', () => {
    const year = yearActivity(
      [submittedAt('2026-10-27T08:00:00Z')],
      now,
      [markedAt('2026-10-28T08:00:00Z'), markedAt('2026-10-27T09:00:00Z'), markedAt('2026-10-26T09:00:00Z'), markedAt('2025-10-01T08:00:00Z')],
    );

    expect(year.days.slice(-3)).toEqual([1, 2, 1]);
    expect(year.marks.slice(-3)).toEqual([1, 1, 1]);
    expect(year.total).toBe(1);
    expect(year.studied).toBe(3);
    expect(year.activeDays).toBe(3);
    expect(year.longestStreak).toBe(3);
  });

  it('says nothing was done in a year without an attempt', () => {
    const year = yearActivity([], now);

    expect([year.total, year.activeDays, year.longestStreak]).toEqual([0, 0, 0]);
  });

  it('labels each month at the column that holds its first day, none too close to an edge', () => {
    const { months, firstDay } = yearActivity([], now);
    const labels = months.map(({ column, day }) => `${String(column)}:${moscowDate(day).toISOString().slice(0, 10)}`);

    // The window opens on 2025-10-23; 1 November is two columns from it, too close for October's label.
    expect(moscowDate(firstDay).toISOString().slice(0, 10)).toBe('2025-10-23');
    expect(labels[0]).toBe('1:2025-11-01');
    expect(labels.at(-1)).toBe('49:2026-10-01');
    expect(labels).toHaveLength(12);
  });

  it('labels the first month when its label has room', () => {
    // The window opens on 2025-11-02; 1 December is four columns from it.
    const { months } = yearActivity([], new Date('2026-11-07T12:00:00Z'));

    expect(months[0]?.column).toBe(0);
    expect((months[1]?.column ?? 0) - (months[0]?.column ?? 0)).toBeGreaterThanOrEqual(3);
  });

  it('needs attempts from the start of its first day', () => {
    const since = yearSince(now);

    expect(moscowDay(since)).toBe(moscowDay(now) - YEAR_DAYS + 1);
    expect(moscowDay(new Date(since.getTime() - 1))).toBe(moscowDay(now) - YEAR_DAYS);
  });
});
