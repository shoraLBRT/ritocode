import type { ProblemCard, TaskPreview } from '../api';
import type { Translate } from '../i18n';

/** What a page says about itself to a browser tab and a search engine. */
export interface PageMeta {
  readonly title: string;
  readonly description: string;
}

/**
 * The title and description of the page at `pathname`, in one table for the two places that set
 * them: the application on every route change, and the build when it prerenders the public pages
 * (docs/SPEC.md §4.1). A page without an entry of its own takes the site's.
 */
export function pageMeta(pathname: string, t: Translate): PageMeta {
  switch (pathname) {
    case '/':
      return { title: t('meta.home.title'), description: t('meta.home.description') };
    case '/problems':
      return { title: t('meta.problems.title'), description: t('meta.problems.description') };
    case '/tasks':
      return { title: t('meta.tasks.title'), description: t('meta.tasks.description') };
    case '/privacy':
      return { title: t('meta.privacy.title'), description: t('meta.privacy.description') };
    case NOT_FOUND_PATH:
      return { title: t('meta.notFound.title'), description: t('notFound.text') };
    case '/profile':
      return { title: t('meta.profile.title'), description: t('app.tagline') };
    default:
      return { title: t('app.name'), description: t('app.tagline') };
  }
}

/** The page of one problem card, `/problems/{slug}`: its name, and its summary as the description. */
export function problemMeta(card: ProblemCard, t: Translate): PageMeta {
  return { title: t('meta.problem.title', { name: card.name }), description: plainText(card.summary) };
}

/** The page of one task, `/tasks/{slug}`: its title, and the brief — what the agent was asked — as the description. */
export function taskMeta(task: TaskPreview, t: Translate): PageMeta {
  return { title: t('meta.task.title', { title: task.title }), description: plainText(task.brief) };
}

/** The longest description a search engine shows whole, near enough. */
const DESCRIPTION_LENGTH = 160;

/**
 * Markdown as one line of plain text for a description: the marks dropped, a link's text kept, and
 * cut at a word with an ellipsis when it runs past what a search engine shows.
 */
export function plainText(markdown: string, length = DESCRIPTION_LENGTH): string {
  const text = markdown
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/^\s{0,3}(?:#{1,6}|>|[-*+]|\d+\.)\s+/gm, '')
    .replace(/[`*_~]/g, '')
    .replace(/\s+/g, ' ')
    .trim();

  if (text.length <= length) {
    return text;
  }

  const cut = text.slice(0, length - 1);
  const space = cut.lastIndexOf(' ');
  return `${(space > length / 2 ? cut.slice(0, space) : cut).replace(/[\s,.;:—-]+$/, '')}…`;
}

/**
 * The page the build prerenders for an address the application has none for, which the web image
 * serves with 404. No route has it: the route table's `*` renders the same page on any address.
 */
export const NOT_FOUND_PATH = '/404';

/**
 * The fixed public pages the build prerenders and the sitemap lists. Besides them it prerenders, from
 * the content export, a page per problem card (`/problems/{slug}`) and per task (`/tasks/{slug}`).
 */
export const PRERENDERED_PATHS = ['/', '/problems', '/tasks', '/privacy'] as const;

/**
 * The other fixed addresses of the route table (`src/routes.tsx`): the build writes the application's
 * shell under each of them, and the web image answers 404 for any address it has no file for (#300).
 * Card and task addresses come from the content export instead — `/problems/{slug}`, `/tasks/{slug}` and its attempts; `/progress`,
 * an old address, the web image redirects to `/profile` (`deploy/web.Caddyfile`). A test holds this
 * list to the route table.
 */
export const SHELL_PATHS = ['/profile', '/admin', '/admin/signals', '/admin/users', '/admin/attempts', '/admin/stats', '/admin/system'] as const;
