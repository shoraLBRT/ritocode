import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { useT } from '../i18n';
import { pageMeta } from './pageMeta';

/**
 * Keeps the document's title and description in step with the route, so a page the build
 * prerendered hands over to the application with its own title, and every route after it sets its
 * own rather than keeping the last one.
 */
export function useDocumentMeta(): void {
  const { pathname } = useLocation();
  const t = useT();

  useEffect(() => {
    const meta = pageMeta(pathname, t);
    document.title = meta.title;

    let description = document.head.querySelector<HTMLMetaElement>('meta[name="description"]');

    if (description === null) {
      description = document.createElement('meta');
      description.name = 'description';
      document.head.append(description);
    }

    description.content = meta.description;
  }, [pathname, t]);
}
