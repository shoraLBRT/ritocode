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
 * What a prerendered page was rendered from (#310), written next to the page as a JSON file so the
 * application hydrates the markup with the same data instead of fetching it from the API. `path` is
 * the address the page was rendered for: the data is used only there, so a page served under another
 * address — the task page under an attempt's address, `404.html` under any — is rendered afresh.
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

/**
 * The id of the `<link rel="preload" as="fetch">` in a page's head that names the file holding its
 * {@link PrerenderedData}. The data is a file of its own rather than part of the page (#310): on
 * `/problems` it is the whole catalogue a second time, and inside the document it held back the
 * first paint.
 */
export const PRERENDERED_ID = 'rc-prerendered';

export const PrerenderedContext = createContext<PrerenderedData | null>(null);

/** The data the page at `path` was prerendered from, or `null` when this page was not. */
export function usePrerendered(path: string): PrerenderedData | null {
  const data = useContext(PrerenderedContext);
  return data !== null && data.path === path ? data : null;
}

/**
 * The page's own data, when it was prerendered for `pathname`: read from the file the page names,
 * which the browser has been fetching since it read the page's head. `null` for a bare shell, another
 * address, or a file that does not arrive or does not parse — the application then renders the page
 * afresh, which is slower but right.
 *
 * `fetch` is the platform's: the file is a static asset of the build, not the API (`ApiClient`).
 */
export async function loadPrerendered(document: Document, pathname: string, fetch: typeof globalThis.fetch): Promise<PrerenderedData | null> {
  const href = document.getElementById(PRERENDERED_ID)?.getAttribute('href') ?? null;

  if (href === null) {
    return null;
  }

  try {
    // As the preload asked for it — CORS mode, same-origin credentials (`crossorigin="anonymous"`) —
    // so the browser hands over the response it already has instead of asking again.
    const response = await fetch(href, { credentials: 'same-origin' });

    if (!response.ok) {
      return null;
    }

    const data = (await response.json()) as unknown;
    return typeof data === 'object' && data !== null && (data as PrerenderedData).path === pathname ? (data as PrerenderedData) : null;
  } catch {
    return null;
  }
}

/** The `<link>` in a page's head that names its data's file, so the browser fetches it while it reads the page. */
export function prerenderedLink(href: string): string {
  return `<link rel="preload" id="${PRERENDERED_ID}" href="${href}" as="fetch" type="application/json" crossorigin="anonymous" />`;
}
