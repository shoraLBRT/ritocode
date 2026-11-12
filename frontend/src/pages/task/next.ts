import type { TaskSummary } from '../../api';

/**
 * The task *Следующая задача* leads to from a review (#199): the first task after `current`, in the
 * order of the `/tasks` list, that the learner has not solved — any status but `solved`, so a task
 * they tried, gave up on or never opened. Past the end of the list it carries on from the start, and
 * it never offers `current` itself. A signed-out caller has no statuses, so that is simply the next
 * task in the list.
 *
 * `undefined` when every other task is solved: the review then offers no next task, and the header's
 * *Задачи* is the way to the list. A task missing from the list (retired since the attempt) starts
 * the search at the top.
 */
export function nextTask(tasks: readonly TaskSummary[], current: string): TaskSummary | undefined {
  const at = tasks.findIndex((task) => task.slug === current);
  const after = [...tasks.slice(at + 1), ...tasks.slice(0, Math.max(at, 0))];
  return after.find((task) => task.slug !== current && task.status !== 'solved');
}
