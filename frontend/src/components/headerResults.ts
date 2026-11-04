import type { ProblemCard, TaskSummary } from '../api';
import { rank, stems } from '../pages/task/search';

/**
 * What the header's search finds (docs/DESIGN.md › The arena): tasks by their title, problems by
 * name, keywords and summary, each ranked by the task screen's search so the two never disagree
 * about what a word matches. Pure, so the ranking is tested without a screen.
 */

/** How many of each kind the list shows; the full catalogue is a link away. */
export const RESULTS_PER_KIND = 5;

export interface HeaderResult {
  readonly kind: 'task' | 'problem';
  readonly slug: string;
  readonly title: string;
  /** Where choosing it goes. */
  readonly to: string;
}

export interface HeaderResults {
  readonly tasks: readonly HeaderResult[];
  readonly problems: readonly HeaderResult[];
}

export const NO_RESULTS: HeaderResults = { tasks: [], problems: [] };

export function headerResults(tasks: readonly TaskSummary[], cards: readonly ProblemCard[], query: string): HeaderResults {
  const words = stems(query);

  if (words.length === 0) {
    return NO_RESULTS;
  }

  return {
    tasks: rank(tasks, words, (task) => ({ name: task.title, summary: '', keywords: '' }))
      .slice(0, RESULTS_PER_KIND)
      .map((task) => ({ kind: 'task', slug: task.slug, title: task.title, to: `/tasks/${task.slug}` })),
    problems: rank(cards, words, (card) => ({ name: card.name, summary: card.summary, keywords: card.keywords.join(' ') }))
      .slice(0, RESULTS_PER_KIND)
      .map((card) => ({ kind: 'problem', slug: card.slug, title: card.name, to: `/problems#${card.slug}` })),
  };
}
