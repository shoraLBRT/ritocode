import { useCallback, useEffect, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useMatch, useNavigate } from 'react-router';
import { useT } from '../i18n';
import { readSignInReturn, SignInLinks, SignInReturnContext, useSession, withoutSignInReturn } from '../session';
import type { SignInError, SignInReturnState } from '../session';
import { useDocumentMeta, useSiteConfig } from '../site';
import { ThemeToggle } from '../theme';
import { HeaderSearch } from './HeaderSearch';
import { HeaderSlotContext } from './HeaderSlot';
import { HeaderStreak } from './HeaderStreak';

/**
 * The frame every route renders inside: a header with the primary navigation and who is signed
 * in, a main region, and a footer. Routes below it render into the {@link Outlet}, so navigation
 * state and the chrome around it survive a route change instead of being remounted.
 *
 * It also receives the browser back from a sign-in (`src/session/signInReturn.ts`): read once, on
 * the first render — a return is a full page load — and taken out of the address. A failure is
 * shown above the page; a task's request to be checked is handed to the task through context.
 *
 * The header has three parts: the brand and the sections, a centre a page may fill — the task
 * screen puts its steps there (`HeaderSlot`) — and the search, the theme and the session. On the
 * task screen and its review the frame becomes a workspace (docs/DESIGN.md): the sections and the
 * search step aside so the steps own the header, and the page fills the window instead of a
 * reading column.
 *
 * At phone width the header wraps rather than scrolls: brand and sections on the first line, the
 * session below them, the search on a line of its own (styles.css).
 */
export function AppLayout() {
  const t = useT();
  useDocumentMeta();
  const location = useLocation();
  const navigate = useNavigate();
  // Progress is a signed-in page, so it is offered only to a signed-in learner; the admin area only to an admin.
  const session = useSession();
  const signedIn = session.status === 'signedIn';
  const admin = session.status === 'signedIn' && session.user.admin;

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
  const solving = useMatch('/tasks/:slug') !== null;
  const reviewing = useMatch('/tasks/:slug/attempts/:id') !== null;
  const workspace = solving || reviewing;
  const [slot, setSlot] = useState<HTMLElement | null>(null);

  return (
    <div className={workspace ? 'app app--workspace' : 'app'}>
      <a className="skip-link" href="#main">
        {t('app.skipToContent')}
      </a>

      <header className="app__header">
        <div className="app__header-start">
          <Link className="app__brand" to="/">
            <span className="app__mark" aria-hidden="true" />
            {t('app.name')}
          </Link>
          {!workspace && <Navigation signedIn={signedIn} admin={admin} />}
        </div>
        <div className="app__header-center" ref={setSlot} />
        <div className="app__header-end">
          {!workspace && <HeaderSearch />}
          <ThemeToggle />
          <SessionStatus returnPath={location.pathname + withoutSignInReturn(location.search) + location.hash} />
        </div>
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
          <HeaderSlotContext value={slot}>
            <Outlet />
          </HeaderSlotContext>
        </SignInReturnContext>
      </main>

      <footer className="app__footer">
        <span>{t('app.tagline')}</span>
        <Link to="/privacy">{t('app.privacy')}</Link>
      </footer>
    </div>
  );
}

/**
 * The sections (docs/DESIGN.md › The arena); progress only for a signed-in learner, the admin area
 * only for an admin. The brand is the way home.
 */
function Navigation({ signedIn, admin }: { signedIn: boolean; admin: boolean }) {
  const t = useT();

  return (
    <nav className="app__nav" aria-label={t('nav.label')}>
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
      {admin && (
        <NavLink to="/admin" className={navClass}>
          {t('nav.admin')}
        </NavLink>
      )}
    </nav>
  );
}

/**
 * The header's right end (docs/DESIGN.md › The arena). Signed in: the streak, and the avatar — the
 * username's first letter — opening a menu with the username and a way out. A guest: *Войти*,
 * opening the providers with a way back to this page, and *Начать*, the demo task. Says nothing
 * while it does not know yet. Each menu is a native disclosure, which opens from the keyboard and
 * closes again with no script of its own; keyed by the page, so following a link in it closes it.
 */
function SessionStatus({ returnPath }: { returnPath: string }) {
  const session = useSession();
  const t = useT();
  const { demoTask } = useSiteConfig();
  const [signingOut, setSigningOut] = useState(false);
  const [failed, setFailed] = useState(false);

  switch (session.status) {
    case 'signedIn':
      return (
        <div className="app__session">
          <HeaderStreak />
          <details className="app__menu" key={returnPath}>
            <summary className="app__avatar-button" aria-label={t('header.profile', { username: session.user.username })}>
              <span className="rc-avatar" aria-hidden="true">
                {initial(session.user.username)}
              </span>
            </summary>
            <div className="app__menu-popup">
              <p className="app__menu-user">{session.user.username}</p>
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
                <p className="app__session-error" role="alert">
                  {t('session.signOutFailed')}
                </p>
              )}
            </div>
          </details>
        </div>
      );
    case 'signedOut':
      return (
        <div className="app__session">
          <details className="app__menu" key={returnPath}>
            <summary className="button button--small">{t('session.signIn')}</summary>
            <div className="app__menu-popup">
              <SignInLinks returnPath={returnPath} />
            </div>
          </details>
          <Link className="button button--small button--primary" to={`/tasks/${demoTask}`}>
            {t('header.start')}
          </Link>
        </div>
      );
    case 'loading':
    case 'error':
      return null;
  }
}

/** The avatar's letter: the username's first, as a person would write it. */
function initial(username: string): string {
  return (Array.from(username)[0] ?? '?').toLocaleUpperCase('ru');
}

function navClass({ isActive }: { isActive: boolean }): string {
  return isActive ? 'app__nav-link app__nav-link--active' : 'app__nav-link';
}
