import { NavLink, Outlet } from 'react-router';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { useT } from '../../i18n';
import { useSession } from '../../session';
import { NotFoundPage } from '../NotFoundPage';

/**
 * The layout route of the admin area (docs/SPEC.md §6.2): its pages, with a navigation between them,
 * for an admin only. Anyone else — signed out included — sees the page an unknown address shows, as
 * the API answers them, so the area does not confirm it exists.
 */
export function RequireAdmin() {
  const session = useSession();
  const t = useT();

  switch (session.status) {
    case 'loading':
      return <LoadingState />;
    case 'error':
      return <ErrorState error={session.error} onRetry={session.retry} />;
    case 'signedOut':
      return <NotFoundPage />;
    case 'signedIn':
      return session.user.admin ? (
        <section className="page">
          <nav className="admin-nav" aria-label={t('admin.nav')}>
            <NavLink to="/admin/signals" className={navClass}>
              {t('admin.signals')}
            </NavLink>
            <NavLink to="/admin/users" className={navClass}>
              {t('admin.users')}
            </NavLink>
            <NavLink to="/admin/attempts" className={navClass}>
              {t('admin.attempts')}
            </NavLink>
          </nav>
          <Outlet />
        </section>
      ) : (
        <NotFoundPage />
      );
  }
}

function navClass({ isActive }: { isActive: boolean }): string {
  return isActive ? 'admin-nav__link admin-nav__link--active' : 'admin-nav__link';
}
