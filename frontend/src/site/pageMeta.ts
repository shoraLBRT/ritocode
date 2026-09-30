import type { Translate } from '../i18n';

/** What a page says about itself to a browser tab and a search engine. */
export interface PageMeta {
  readonly title: string;
  readonly description: string;
}

/**
 * The title and description of the page at `pathname`, in one table for the two places that set
 * them: the application on every route change, and the build when it prerenders `/` and `/problems`
 * (docs/SPEC.md §4.1). A page without an entry of its own takes the site's.
 */
export function pageMeta(pathname: string, t: Translate): PageMeta {
  switch (pathname) {
    case '/':
      return { title: t('meta.home.title'), description: t('meta.home.description') };
    case '/problems':
      return { title: t('meta.problems.title'), description: t('meta.problems.description') };
    default:
      return { title: t('app.name'), description: t('app.tagline') };
  }
}

/** The public pages the build prerenders and the sitemap lists. */
export const PRERENDERED_PATHS = ['/', '/problems'] as const;
