import { NavLink, Outlet } from 'react-router';

/**
 * The frame every route renders inside: a header with the primary navigation, a main region,
 * and a footer. Routes below it render into the {@link Outlet}, so navigation state and the
 * chrome around it survive a route change instead of being remounted.
 */
export function AppLayout() {
  return (
    <div className="app">
      <header className="app__header">
        <a className="app__brand" href="/">
          Ritocode
        </a>
        <nav className="app__nav" aria-label="Primary">
          <NavLink to="/" end className={navClass}>
            Home
          </NavLink>
          <NavLink to="/problems" className={navClass}>
            Problems
          </NavLink>
        </nav>
      </header>

      <main className="app__main" id="main">
        <Outlet />
      </main>

      <footer className="app__footer">
        <span>Practise code review, refactoring and test quality on real code.</span>
      </footer>
    </div>
  );
}

function navClass({ isActive }: { isActive: boolean }): string {
  return isActive ? 'app__nav-link app__nav-link--active' : 'app__nav-link';
}
