import { useEffect, useState } from 'react';
import { isRouteErrorResponse, useRouteError } from 'react-router';
import { useT } from '../i18n';
import { NotFoundPage } from '../pages/NotFoundPage';
import { reportError } from './reporter';

/**
 * The route table's error boundary (#294): a page whose render threw lands here instead of on a blank
 * screen. The error is reported, and the page shows the report's request id, which `/admin/errors` lists
 * beside it — what a learner quotes in feedback finds the error. A router's own answer (an unknown address)
 * is the not-found page, not an error.
 */
export function RouteErrorPage() {
  const error = useRouteError();

  if (isRouteErrorResponse(error) && error.status === 404) {
    return <NotFoundPage />;
  }

  return <ErrorReport error={error} />;
}

function ErrorReport({ error }: { error: unknown }) {
  const t = useT();
  const [requestId, setRequestId] = useState<string | null>(null);

  useEffect(() => {
    let current = true;
    void reportError(error).then((id) => {
      if (current) {
        setRequestId(id);
      }
    });
    return () => {
      current = false;
    };
  }, [error]);

  return (
    <main className="page rc-notfound rc-error-page">
      <h1>{t('errorPage.title')}</h1>
      <p className="rc-notfound__text">{t('errorPage.text')}</p>
      {requestId !== null && (
        <p className="rc-error-page__id">
          <code>{t('errorPage.requestId', { id: requestId })}</code>
        </p>
      )}
      <nav className="rc-notfound__ways" aria-label={t('notFound.ways')}>
        {/* Plain links, not the router's: after a render error a full reload is what starts the page over. */}
        <a
          className="rc-notfound__way rc-notfound__way--primary"
          href={typeof window === 'undefined' ? '/' : window.location.pathname}
        >
          <span className="rc-notfound__way-label">{t('errorPage.reload')}</span>
          <span className="rc-notfound__arrow" aria-hidden="true" />
        </a>
        <a className="rc-notfound__way" href="/">
          <span className="rc-notfound__way-label">{t('errorPage.home')}</span>
          <span className="rc-notfound__arrow" aria-hidden="true" />
        </a>
      </nav>
    </main>
  );
}
