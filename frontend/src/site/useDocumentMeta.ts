import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { useT } from '../i18n';
import { pageMeta } from './pageMeta';
import type { PageMeta } from './pageMeta';

/**
 * Keeps the document's title and description in step with the route, so a page the build
 * prerendered hands over to the application with its own title, and every route after it sets its
 * own rather than keeping the last one.
 */
export function useDocumentMeta(): void {
  const { pathname } = useLocation();
  const t = useT();

  useEffect(() => {
    setDocumentMeta(pageMeta(pathname, t));
  }, [pathname, t]);
}

/**
 * A page whose title comes from what it loaded — a problem card, a task — sets it once it has it,
 * over what `useDocumentMeta` set for the address: that runs when the address changes, while the
 * page is still loading, so the page's own comes after it. `null` while there is nothing to say.
 */
export function usePageMeta(meta: PageMeta | null): void {
  const title = meta?.title;
  const description = meta?.description;

  useEffect(() => {
    if (title !== undefined && description !== undefined) {
      setDocumentMeta({ title, description });
    }
  }, [title, description]);
}

function setDocumentMeta(meta: PageMeta): void {
  document.title = meta.title;

  let description = document.head.querySelector<HTMLMetaElement>('meta[name="description"]');

  if (description === null) {
    description = document.createElement('meta');
    description.name = 'description';
    document.head.append(description);
  }

  description.content = meta.description;
}
