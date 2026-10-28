import { describe, expect, it } from 'vitest';
import type { AttemptSummary, TaskSummary } from '../../api';
import { ACTIVITY_DAYS, activity, activityLevel, activitySince, dailyTask, moscowDay } from './arena';

function taskOf(slug: string, solved: boolean | null): TaskSummary {
  return { slug, title: slug, difficulty: 'easy', solved };
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

describe('the daily task', () => {
  const tasks = [taskOf('a', false), taskOf('b', true), taskOf('c', false), taskOf('d', false)];

  it('is the same all day and changes at midnight', () => {
    const morning = dailyTask(tasks, new Date('2026-10-28T06:00:00Z'));
    const evening = dailyTask(tasks, new Date('2026-10-28T20:00:00Z'));
    const nextDay = dailyTask(tasks, new Date('2026-10-28T21:30:00Z'));

    expect(evening).toEqual(morning);
    expect(nextDay).not.toEqual(morning);
  });

  it('is never a task the learner has solved while one is left', () => {
    for (let hour = 0; hour < 24 * 10; hour += 6) {
      const day = dailyTask(tasks, new Date(Date.UTC(2026, 9, 1, hour)));
      expect(day?.solved).toBe(false);
    }
  });

  it('comes from every task for a guest, and for a learner who solved them all', () => {
    const guest = [taskOf('a', null), taskOf('b', null)];
    const done = [taskOf('a', true), taskOf('b', true)];

    expect(dailyTask(guest, new Date('2026-10-28T12:00:00Z'))).not.toBeNull();
    expect(dailyTask(done, new Date('2026-10-28T12:00:00Z'))?.solved).toBe(true);
  });

  it('is null without tasks', () => {
    expect(dailyTask([], new Date())).toBeNull();
  });
});

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

  it('shades a day by how many attempts it had', () => {
    expect([0, 1, 2, 3, 4, 5, 9].map(activityLevel)).toEqual([0, 1, 2, 3, 3, 4, 4]);
  });

  it('needs attempts from the start of its first day', () => {
    const since = activitySince(now);

    expect(moscowDay(since)).toBe(moscowDay(now) - ACTIVITY_DAYS + 1);
    expect(moscowDay(new Date(since.getTime() - 1))).toBe(moscowDay(now) - ACTIVITY_DAYS);
  });
});
