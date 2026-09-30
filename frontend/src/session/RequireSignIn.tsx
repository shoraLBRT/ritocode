import { Outlet } from 'react-router';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { useT } from '../i18n';
import { useSession } from './SessionContext';

/**
 * A layout route for the pages that need a signed-in learner — progress, a review of an attempt.
 * Its children render only for a signed-in caller; anyone else is told why the page is closed.
 * There is nowhere to sign in yet: the button arrives with sign-in itself in
 * [#7](https://github.com/shoraLBRT/ritocode/issues/7), and the return to the page with #127.
 */
export function RequireSignIn() {
  const session = useSession();
  const t = useT();

  switch (session.status) {
    case 'loading':
      return <LoadingState />;
    case 'error':
      return <ErrorState error={session.error} onRetry={session.retry} />;
    case 'signedOut':
      return (
        <section className="page">
          <h1>{t('session.requiredTitle')}</h1>
          <p>{t('session.requiredText')}</p>
        </section>
      );
    case 'signedIn':
      return <Outlet />;
  }
}
