import { useCallback, useEffect, useState } from 'react';
import { NavLink, Outlet } from 'react-router';
import { listAdminSignals, useApiClient } from '../../api';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { useT } from '../../i18n';
import { useSession } from '../../session';
import { NotFoundPage } from '../NotFoundPage';
import './admin.css';
import { AdminOutletContext } from './adminContext';

/**
 * The layout route of the admin area (docs/SPEC.md §6.2): its pages, with a navigation between them,
 * for an admin only. Anyone else — signed out included — sees the page an unknown address shows, as
 * the API answers them, so the area does not confirm it exists.
 */
export function RequireAdmin() {
  const session = useSession();

  switch (session.status) {
    case 'loading':
      return <LoadingState />;
    case 'error':
      return <ErrorState error={session.error} onRetry={session.retry} />;
    case 'signedOut':
      return <NotFoundPage />;
    case 'signedIn':
      return session.user.admin ? <AdminShell /> : <NotFoundPage />;
  }
}

/** The page head and the section tabs, with the number of open signals on *Сигналы*; read for admins only. */
function AdminShell() {
  const client = useApiClient();
  const t = useT();
  const [openSignals, setOpenSignals] = useState<number | null>(null);
  const [attempt, setAttempt] = useState(0);

  // Read again after a signal is resolved; meanwhile the last number stays, and a failure hides it.
  useEffect(() => {
    const controller = new AbortController();
    listAdminSignals(client, { status: 'open', page: 1, pageSize: 1 }, controller.signal).then(
      (page) => {
        if (!controller.signal.aborted) setOpenSignals(page.totalItems);
      },
      () => {
        if (!controller.signal.aborted) setOpenSignals(null);
      },
    );
    return () => {
      controller.abort();
    };
  }, [client, attempt]);
  const reloadOpenSignals = useCallback(() => {
    setAttempt((previous) => previous + 1);
  }, []);

  return (
    <section className="page rc-arena rc-admin">
      <header className="rc-page-head">
        <h1>{t('admin.title')}</h1>
        <p>{t('admin.lead')}</p>
      </header>

      <nav className="rc-admin__nav" aria-label={t('admin.nav')}>
        <NavLink to="/admin" end className="rc-admin__tab">
          {t('admin.overview')}
        </NavLink>
        <NavLink to="/admin/signals" className="rc-admin__tab">
          {t('admin.signals')}
          {openSignals !== null && <span className="rc-tab__count">{openSignals}</span>}
        </NavLink>
        <NavLink to="/admin/users" className="rc-admin__tab">
          {t('admin.users')}
        </NavLink>
        <NavLink to="/admin/attempts" className="rc-admin__tab">
          {t('admin.attempts')}
        </NavLink>
        <NavLink to="/admin/stats" className="rc-admin__tab">
          {t('admin.stats')}
        </NavLink>
        <NavLink to="/admin/system" className="rc-admin__tab">
          {t('admin.system')}
        </NavLink>
        <NavLink to="/admin/errors" className="rc-admin__tab">
          {t('admin.errors')}
        </NavLink>
      </nav>

      <AdminOutletContext value={{ openSignals, reloadOpenSignals }}>
        <Outlet />
      </AdminOutletContext>
    </section>
  );
}
