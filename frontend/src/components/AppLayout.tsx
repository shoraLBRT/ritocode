import { Link, NavLink, Outlet } from 'react-router';
import { useT } from '../i18n';
import { useSession } from '../session';

/**
 * The frame every route renders inside: a header with the primary navigation and who is signed
 * in, a main region, and a footer. Routes below it render into the {@link Outlet}, so navigation
 * state and the chrome around it survive a route change instead of being remounted.
 *
 * At phone width the header wraps rather than scrolls: brand and navigation on the first line,
 * the session below them (styles.css).
 */
export function AppLayout() {
  const t = useT();
  // Progress is a signed-in page, so it is offered only to a signed-in learner.
  const signedIn = useSession().status === 'signedIn';

  return (
    <div className="app">
      <a className="skip-link" href="#main">
        {t('app.skipToContent')}
      </a>

      <header className="app__header">
        <Link className="app__brand" to="/">
          {t('app.name')}
        </Link>
        <nav className="app__nav" aria-label={t('nav.label')}>
          <NavLink to="/" end className={navClass}>
            {t('nav.home')}
          </NavLink>
          <NavLink to="/tasks" className={navClass}>
            {t('nav.tasks')}
          </NavLink>
          <NavLink to="/problems" className={navClass}>
            {t('nav.problems')}
          </NavLink>
          {signedIn && (
            <NavLink to="/progress" className={navClass}>
              {t('nav.progress')}
            </NavLink>
          )}
        </nav>
        <SessionStatus />
      </header>

      <main className="app__main" id="main">
        <Outlet />
      </main>

      <footer className="app__footer">
        <span>{t('app.tagline')}</span>
      </footer>
    </div>
  );
}

/** Who is signed in, or that nobody is. Says nothing while it does not know yet. */
function SessionStatus() {
  const session = useSession();
  const t = useT();

  switch (session.status) {
    case 'signedIn':
      return <span className="app__session">{t('session.signedInAs', { username: session.user.username })}</span>;
    case 'signedOut':
      return <span className="app__session">{t('session.signedOut')}</span>;
    case 'loading':
    case 'error':
      return null;
  }
}

function navClass({ isActive }: { isActive: boolean }): string {
  return isActive ? 'app__nav-link app__nav-link--active' : 'app__nav-link';
}
