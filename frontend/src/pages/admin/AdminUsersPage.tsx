import { Link, useSearchParams } from 'react-router';
import { listAdminUsers, useApiClient } from '../../api';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { ADMIN_PAGE_SIZE, pageParam } from './paging';
import { AdminDate, Pager } from './shared';

/**
 * `/admin/users` — every user, newest first (docs/SPEC.md §6.2): e-mail, provider, date registered,
 * attempts made, tasks solved; each with a link to their attempts.
 */
export function AdminUsersPage() {
  const client = useApiClient();
  const t = useT();
  const [params, setParams] = useSearchParams();
  const page = pageParam(params.get('page'));

  const { state, reload } = useApiResource((signal) => listAdminUsers(client, { page, pageSize: ADMIN_PAGE_SIZE }, signal), [client, page]);

  return (
    <>
      <h1>{t('admin.users')}</h1>
      <p className="page__lead">{t('admin.usersPage.lead')}</p>

      {state.status === 'loading' && <LoadingState label={t('admin.loading')} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && state.data.items.length === 0 && <EmptyState>{t('admin.usersPage.empty')}</EmptyState>}
      {state.status === 'success' && state.data.items.length > 0 && (
        <>
          <div className="table-scroll" role="region" aria-label={t('admin.users')} tabIndex={0}>
            <table className="admin-table">
              <thead>
                <tr>
                  <th scope="col">{t('admin.usersPage.email')}</th>
                  <th scope="col">{t('admin.usersPage.username')}</th>
                  <th scope="col">{t('admin.usersPage.providers')}</th>
                  <th scope="col">{t('admin.usersPage.registered')}</th>
                  <th scope="col">{t('admin.usersPage.attempts')}</th>
                  <th scope="col">{t('admin.usersPage.tasksSolved')}</th>
                </tr>
              </thead>
              <tbody>
                {state.data.items.map((user) => (
                  <tr key={user.id}>
                    <td>{user.email}</td>
                    <td>{user.username}</td>
                    <td>{user.providers.length > 0 ? user.providers.join(', ') : <span className="admin-muted">{t('admin.usersPage.noProvider')}</span>}</td>
                    <td>
                      <AdminDate value={user.registeredAt} />
                    </td>
                    <td className="admin-table__number">
                      {user.attempts > 0 ? (
                        <Link to={`/admin/attempts?user=${user.id}`} aria-label={`${t('admin.usersPage.showAttempts')}: ${user.email}`}>
                          {user.attempts}
                        </Link>
                      ) : (
                        user.attempts
                      )}
                    </td>
                    <td className="admin-table__number">{user.tasksSolved}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager
            page={state.data}
            onPage={(next) => {
              setParams({ page: String(next) });
            }}
          />
        </>
      )}
    </>
  );
}
