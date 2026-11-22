import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useMatch, useNavigate } from 'react-router';
import { FeedbackWidget } from '../feedback';
import { useT } from '../i18n';
import { readSignInReturn, SignInLinks, SignInReturnContext, useSession, withoutSignInReturn } from '../session';
import type { SignInError, SignInReturnState } from '../session';
import { useDocumentMeta, useSiteConfig } from '../site';
import { ThemeToggle } from '../theme';
import { ApiUnavailableBanner } from './ApiUnavailableBanner';
import { AppFooter } from './AppFooter';
import { HeaderSearch } from './HeaderSearch';
import { HeaderSlotContext } from './HeaderSlot';
import { HeaderStreak } from './HeaderStreak';
import { MobileMenu } from './MobileMenu';
import { SignOutButton } from './SignOutButton';
import { useSections } from './sections';

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
 * task screen and its review the frame becomes a workspace (docs/DESIGN.md): the search steps
 * aside so the steps have the centre, the sections stay — a review is one click from the task list
 * and the catalogue (#199) — and the page fills the window instead of a reading column.
 *
 * Elsewhere the header is sticky and gains a shadow once the page has scrolled (`data-scrolled`).
 * At phone width (48 rem) it is one row: the brand, the search as an icon opening the field over the
 * header — below 64 rem —, the session, and a menu button opening the sections, the theme and the
 * session's actions in a sheet (`MobileMenu`). Both controls are always in the markup; styles.css
 * hides the one a width does not use with `display: none`.
 */
export function AppLayout() {
  const t = useT();
  useDocumentMeta();
  const location = useLocation();
  const navigate = useNavigate();
  // The page the visitor landed on is already in place — prerendered, it was painted before any
  // script ran — so only a page navigated to rises in (#310): an entrance on the first page would
  // repaint it, and its largest paint with it, once the animation ends.
  const [landing] = useState(location.key);
  const navigated = location.key !== landing;
  // The admin area is offered only to an admin; the profile is in the avatar's menu.
  const session = useSession();
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

  const headerRef = useRef<HTMLElement>(null);
  const menuButton = useRef<HTMLButtonElement>(null);
  const searchButton = useRef<HTMLButtonElement>(null);
  const menuId = useId();
  const [scrolled, setScrolled] = useState(false);
  const [menuTop, setMenuTop] = useState(0);
  // Each is open for the page it was opened on, so a page change ends it.
  const [menuAt, setMenuAt] = useState<string | null>(null);
  const [searchAt, setSearchAt] = useState<string | null>(null);
  const menuOpen = menuAt === location.pathname;
  const searchOpen = searchAt === location.pathname;

  // Starts unset, so the server's markup and the first client render agree.
  useEffect(() => {
    let frame = 0;
    const read = () => {
      frame = 0;
      setScrolled(window.scrollY > 4);
    };
    const onScroll = () => {
      if (frame === 0) {
        frame = window.requestAnimationFrame(read);
      }
    };
    read();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => {
      window.removeEventListener('scroll', onScroll);
      if (frame !== 0) {
        window.cancelAnimationFrame(frame);
      }
    };
  }, []);

  const closeMenu = useCallback(() => {
    setMenuAt(null);
    menuButton.current?.focus();
  }, []);
  const closeSearch = useCallback((restoreFocus: boolean) => {
    setSearchAt(null);
    if (restoreFocus) {
      searchButton.current?.focus();
    }
  }, []);

  // A wider window ends them too: neither the sheet nor the overlay exists there.
  useEffect(() => {
    if (typeof window.matchMedia !== 'function') {
      return undefined;
    }
    const wide = window.matchMedia('(min-width: 48.0625rem)');
    const widest = window.matchMedia('(min-width: 64.0625rem)');
    const onWide = () => {
      if (wide.matches) {
        setMenuAt(null);
      }
    };
    const onWidest = () => {
      if (widest.matches) {
        setSearchAt(null);
      }
    };
    wide.addEventListener('change', onWide);
    widest.addEventListener('change', onWidest);
    return () => {
      wide.removeEventListener('change', onWide);
      widest.removeEventListener('change', onWidest);
    };
  }, []);

  return (
    <div className={['app', workspace && 'app--workspace', navigated && 'app--navigated'].filter(Boolean).join(' ')}>
      <a className="skip-link" href="#main">
        {t('app.skipToContent')}
      </a>

      <header
        className="app__header"
        ref={headerRef}
        {...(scrolled ? { 'data-scrolled': '' } : {})}
        {...(searchOpen ? { 'data-search': 'open' } : {})}
      >
        <div className="app__header-start">
          <Link className="app__brand" to="/">
            <span className="app__mark" aria-hidden="true" />
            {t('app.name')}
          </Link>
          <Navigation admin={admin} />
        </div>
        <div className="app__header-center" ref={setSlot} />
        <div className="app__header-end">
          {!workspace && <HeaderSearch overlayOpen={searchOpen} onClose={closeSearch} />}
          {!workspace && (
            <button
              ref={searchButton}
              type="button"
              className="icon-button app__search-button"
              aria-label={t('header.search.open')}
              onClick={() => {
                setMenuAt(null);
                setSearchAt(location.pathname);
              }}
            >
              <svg viewBox="0 0 24 24" aria-hidden="true" className="icon">
                <circle cx="11" cy="11" r="7" />
                <path d="m20 20-3.5-3.5" />
              </svg>
            </button>
          )}
          <ThemeToggle className="app__theme" />
          <SessionStatus returnPath={location.pathname + withoutSignInReturn(location.search) + location.hash} />
          <button
            ref={menuButton}
            type="button"
            className="icon-button app__menu-button"
            aria-label={t('header.menu')}
            aria-expanded={menuOpen}
            aria-controls={menuId}
            onClick={() => {
              if (menuOpen) {
                closeMenu();
              } else {
                setSearchAt(null);
                setMenuTop(headerRef.current?.getBoundingClientRect().bottom ?? 0);
                setMenuAt(location.pathname);
              }
            }}
          >
            <svg viewBox="0 0 24 24" aria-hidden="true" className="icon">
              <path d="M4 7h16M4 12h16M4 17h16" />
            </svg>
          </button>
        </div>
      </header>

      {menuOpen && <MobileMenu id={menuId} top={menuTop} admin={admin} onClose={closeMenu} />}

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

      <ApiUnavailableBanner />

      <main className="app__main" id="main">
        <SignInReturnContext value={signInReturn}>
          <HeaderSlotContext value={slot}>
            <Outlet />
          </HeaderSlotContext>
        </SignInReturnContext>
      </main>

      <AppFooter />

      <FeedbackWidget />
    </div>
  );
}

/** The sections in the header's row, the current one underlined (hidden below 48 rem: the menu has them). */
function Navigation({ admin }: { admin: boolean }) {
  const t = useT();
  const sections = useSections(admin);

  return (
    <nav className="app__nav" aria-label={t('nav.label')}>
      {sections.map((section) => (
        <NavLink key={section.to} to={section.to} className={navClass}>
          {section.label}
        </NavLink>
      ))}
    </nav>
  );
}

/**
 * The header's right end (docs/DESIGN.md › The arena). Signed in: the streak, leading to the profile,
 * and the avatar — the username's first letter — opening a menu with the username, *Профиль* and a
 * way out. A guest: *Войти*,
 * opening the providers with a way back to this page, and *Начать*, the demo task. Says nothing
 * while it does not know yet. Each menu is a native disclosure, which opens from the keyboard and
 * closes again with no script of its own; keyed by the page, so following a link in it closes it.
 */
function SessionStatus({ returnPath }: { returnPath: string }) {
  const session = useSession();
  const t = useT();
  const { demoTask } = useSiteConfig();

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
              <Link className="app__menu-link" to="/profile">
                {t('header.toProfile')}
              </Link>
              <SignOutButton className="button button--small" signOut={session.signOut} />
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
          <Link className="button button--small button--primary app__start" to={`/tasks/${demoTask}`}>
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
