import { Outlet, useLocation } from 'react-router';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { useT } from '../i18n';
import { useSession } from './SessionContext';
import { SignInLinks } from './SignInLinks';

/**
 * A layout route for the pages that need a signed-in learner — progress, a review of an attempt.
 * Its children render only for a signed-in caller; anyone else is told why the page is closed and
 * offered the providers, which bring them back to this page once signed in.
 */
export function RequireSignIn() {
  const session = useSession();
  const t = useT();
  const location = useLocation();

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
          <SignInLinks returnPath={location.pathname + location.search} />
        </section>
      );
    case 'signedIn':
      return <Outlet />;
  }
}
