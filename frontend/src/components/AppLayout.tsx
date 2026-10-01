import { useCallback, useEffect, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router';
import { useT } from '../i18n';
import { readSignInReturn, SignInLinks, SignInReturnContext, useSession, withoutSignInReturn } from '../session';
import type { SignInError, SignInReturnState } from '../session';
import { useDocumentMeta } from '../site';

/**
 * The frame every route renders inside: a header with the primary navigation and who is signed
 * in, a main region, and a footer. Routes below it render into the {@link Outlet}, so navigation
 * state and the chrome around it survive a route change instead of being remounted.
 *
 * It also receives the browser back from a sign-in (`src/session/signInReturn.ts`): read once, on
 * the first render — a return is a full page load — and taken out of the address. A failure is
 * shown above the page; a task's request to be checked is handed to the task through context.
 *
 * At phone width the header wraps rather than scrolls: brand and navigation on the first line,
 * the session below them (styles.css).
 */
export function AppLayout() {
  const t = useT();
  useDocumentMeta();
  const location = useLocation();
  const navigate = useNavigate();
  // Progress is a signed-in page, so it is offered only to a signed-in learner.
  const signedIn = useSession().status === 'signedIn';

  const [returned] = useState(() => readSignInReturn(location.pathname, location.search));
  const [signInError, setSignInError] = useState<SignInError | null>(returned.error);
  const [checkRequestedAt, setCheckRequestedAt] = useState(returned.checkRequestedAt);
  const consumeCheck = useCallback(() => {
    setCheckRequestedAt(null);
  }, []);

  useEffect(() => {
    if (returned.error !== null || returned.checkRequestedAt !== null) {
      void navigate({ pathname: location.pathname, search: withoutSignInReturn(location.search), hash: location.hash }, { replace: true });
    }
    // Once, for the address the application loaded with.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const signInReturn: SignInReturnState = { checkRequestedAt, consumeCheck };

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
        <SessionStatus returnPath={location.pathname + withoutSignInReturn(location.search) + location.hash} />
      </header>

      {signInError !== null && (
        <div className="notice notice--error" role="alert">
          <p>{t(`session.signInError.${signInError}`)}</p>
          <button
            type="button"
            className="button"
            onClick={() => {
              setSignInError(null);
            }}
          >
            {t('session.dismiss')}
          </button>
        </div>
      )}

      <main className="app__main" id="main">
        <SignInReturnContext value={signInReturn}>
          <Outlet />
        </SignInReturnContext>
      </main>

      <footer className="app__footer">
        <span>{t('app.tagline')}</span>
      </footer>
    </div>
  );
}

/**
 * Who is signed in and a way out, or a way in that brings the visitor back to this page. Says
 * nothing while it does not know yet.
 */
function SessionStatus({ returnPath }: { returnPath: string }) {
  const session = useSession();
  const t = useT();
  const [signingOut, setSigningOut] = useState(false);
  const [failed, setFailed] = useState(false);

  switch (session.status) {
    case 'signedIn':
      return (
        <div className="app__session">
          <span>{t('session.signedInAs', { username: session.user.username })}</span>
          <button
            type="button"
            className="button button--small"
            disabled={signingOut}
            onClick={() => {
              setSigningOut(true);
              setFailed(false);
              session.signOut().then(
                () => {
                  setSigningOut(false);
                },
                () => {
                  setSigningOut(false);
                  setFailed(true);
                },
              );
            }}
          >
            {signingOut ? t('session.signingOut') : t('session.signOut')}
          </button>
          {failed && (
            <span className="app__session-error" role="alert">
              {t('session.signOutFailed')}
            </span>
          )}
        </div>
      );
    case 'signedOut':
      return (
        <div className="app__session">
          <span>{t('session.signedOut')}</span>
          {/* A native disclosure: opens from the keyboard and closes again with no script of its own. */}
          <details className="app__sign-in">
            <summary className="button button--small button--primary">{t('session.signIn')}</summary>
            <div className="app__sign-in-menu">
              <SignInLinks returnPath={returnPath} />
            </div>
          </details>
        </div>
      );
    case 'loading':
    case 'error':
      return null;
  }
}

function navClass({ isActive }: { isActive: boolean }): string {
  return isActive ? 'app__nav-link app__nav-link--active' : 'app__nav-link';
}
