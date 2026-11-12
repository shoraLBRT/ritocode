import { describe, expect, it } from 'vitest';
import type { TaskStatus, TaskSummary } from '../../api';
import { nextTask } from './next';

function list(...statuses: (TaskStatus | null)[]): TaskSummary[] {
  return statuses.map((status, index) => ({ slug: `t${String(index)}`, title: `Task ${String(index)}`, difficulty: 'easy', status }));
}

describe('the next task after a review', () => {
  it('is the next one in the list that is not solved, skipping the solved ones', () => {
    expect(nextTask(list('new', 'solved', 'solved', 'tried', 'new'), 't0')?.slug).toBe('t3');
  });

  it('counts a task tried, given up on or never opened as unsolved', () => {
    expect(nextTask(list('solved', 'tried'), 't0')?.slug).toBe('t1');
    expect(nextTask(list('solved', 'gaveUp'), 't0')?.slug).toBe('t1');
    expect(nextTask(list('solved', 'new'), 't0')?.slug).toBe('t1');
  });

  it('carries on from the start of the list past its end', () => {
    expect(nextTask(list('new', 'solved', 'tried', 'solved'), 't2')?.slug).toBe('t0');
    expect(nextTask(list('solved', 'tried', 'new'), 't2')?.slug).toBe('t1');
  });

  it('never offers the task under review, whatever its status', () => {
    expect(nextTask(list('tried', 'solved', 'solved'), 't0')).toBeUndefined();
    expect(nextTask(list('tried'), 't0')).toBeUndefined();
  });

  it('offers nothing when every other task is solved', () => {
    expect(nextTask(list('solved', 'new', 'solved'), 't1')).toBeUndefined();
    expect(nextTask([], 't0')).toBeUndefined();
  });

  it('is simply the next task in the list for a signed-out learner', () => {
    expect(nextTask(list(null, null, null), 't1')?.slug).toBe('t2');
    expect(nextTask(list(null, null, null), 't2')?.slug).toBe('t0');
  });

  it('starts from the top when the task under review is no longer in the list', () => {
    expect(nextTask(list('solved', 'new', 'tried'), 'retired')?.slug).toBe('t1');
  });
});
