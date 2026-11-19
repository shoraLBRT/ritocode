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

/**
 * The page the build prerenders for an address the application has none for, which the web image
 * serves with 404. No route has it: the route table's `*` renders the same page on any address.
 */
export const NOT_FOUND_PATH = '/404';

/** The public pages the build prerenders and the sitemap lists. */
export const PRERENDERED_PATHS = ['/', '/problems', '/privacy'] as const;

/**
 * The other fixed addresses of the route table (`src/routes.tsx`): the build writes the application's
 * shell under each of them, and the web image answers 404 for any address it has no file for (#300).
 * Task addresses come from the content export instead — `/tasks/{slug}` and its attempts; `/progress`,
 * an old address, the web image redirects to `/profile` (`deploy/web.Caddyfile`). A test holds this
 * list to the route table.
 */
export const SHELL_PATHS = ['/tasks', '/profile', '/admin', '/admin/signals', '/admin/users', '/admin/attempts', '/admin/system'] as const;
