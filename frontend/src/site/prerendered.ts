import { createContext, useContext } from 'react';
import type { Page, ProblemCard, ProblemCatalogue, ProblemClass, TaskSummary } from '../api';

/** A card as the arena shows it among the problems to read: no sections. */
export type ArenaCard = Pick<ProblemCard, 'slug' | 'class' | 'name' | 'summary'>;

/** What the arena needs of the catalogue: its classes and every card's name and summary. */
export interface ArenaCatalogue {
  readonly classes: readonly ProblemClass[];
  readonly cards: readonly ArenaCard[];
}

/**
 * What a prerendered page was rendered from (#310), written into the page as JSON so the application
 * hydrates the markup with the same data instead of fetching it again. `path` is the address the
 * page was rendered for: the data is used only there, so a page served under another address — the
 * task page under an attempt's address, `404.html` under any — is rendered afresh.
 *
 * Each page carries only what it shows, all of it already in its markup: the catalogue on
 * `/problems`; the card and the other cards of its class on a card's page; the classes and the
 * cards' names and summaries on `/`, with the instant it was built for, since the problems to read
 * change with the day; the tasks' slugs, titles and difficulties on `/tasks`, as a guest's first page of `GET /tasks`. Never a task's key.
 */
export interface PrerenderedData {
  readonly path: string;
  readonly catalogue?: ProblemCatalogue;
  readonly arena?: { readonly catalogue: ArenaCatalogue; readonly builtAt: string };
  readonly tasks?: Page<TaskSummary>;
}

/** The id of the `<script type="application/json">` that holds a page's {@link PrerenderedData}. */
export const PRERENDERED_ID = 'rc-prerendered';

export const PrerenderedContext = createContext<PrerenderedData | null>(null);

/** The data the page at `path` was prerendered from, or `null` when this page was not. */
export function usePrerendered(path: string): PrerenderedData | null {
  const data = useContext(PrerenderedContext);
  return data !== null && data.path === path ? data : null;
}

/** The page's own data, when it was prerendered for `pathname`; `null` for a bare shell or another address. */
export function readPrerendered(document: Document, pathname: string): PrerenderedData | null {
  const text = document.getElementById(PRERENDERED_ID)?.textContent;

  if (text === undefined || text === null) {
    return null;
  }

  try {
    const data = JSON.parse(text) as PrerenderedData;
    return typeof data === 'object' && data !== null && data.path === pathname ? data : null;
  } catch {
    return null;
  }
}

/** The data as the `<script>` that carries it, safe whatever a card's text holds: no `</script>`, no comment opener. */
export function prerenderedScript(data: PrerenderedData): string {
  const json = JSON.stringify(data).replaceAll('<', '\\u003c').replaceAll('>', '\\u003e').replaceAll('&', '\\u0026');
  return `<script type="application/json" id="${PRERENDERED_ID}">${json}</script>`;
}
