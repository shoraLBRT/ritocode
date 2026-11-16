import { Link, useSearchParams } from 'react-router';
import { listAdminUsers, useApiClient } from '../../api';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { ADMIN_PAGE_SIZE, pageParam } from './paging';
import { AdminDate, AdminPanel, AdminTable, AdminTotal, ListBody, Pager } from './shared';

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
    <AdminPanel
      title={t('admin.users')}
      lead={t('admin.usersPage.lead')}
      total={state.status === 'success' ? <AdminTotal count={state.data.totalItems} /> : null}
    >
      <ListBody state={state} reload={reload} empty={t('admin.usersPage.empty')}>
        {(list) => (
          <>
            <AdminTable label={t('admin.users')}>
              <thead>
                <tr>
                  <th scope="col">{t('admin.usersPage.email')}</th>
                  <th scope="col">{t('admin.usersPage.username')}</th>
                  <th scope="col">{t('admin.usersPage.providers')}</th>
                  <th scope="col">{t('admin.usersPage.registered')}</th>
                  <th scope="col" className="rc-admin__num">
                    {t('admin.usersPage.attempts')}
                  </th>
                  <th scope="col" className="rc-admin__num">
                    {t('admin.usersPage.tasksSolved')}
                  </th>
                </tr>
              </thead>
              <tbody>
                {list.items.map((user) => (
                  <tr key={user.id} className="rc-admin__row">
                    <td className="rc-admin__text">{user.email}</td>
                    <td className="rc-admin__text">{user.username}</td>
                    <td>
                      {user.providers.length > 0 ? (
                        <span className="rc-admin__chips">
                          {user.providers.map((provider) => (
                            <span key={provider} className="rc-admin__chip">
                              {provider}
                            </span>
                          ))}
                        </span>
                      ) : (
                        <span className="rc-admin__muted">{t('admin.usersPage.noProvider')}</span>
                      )}
                    </td>
                    <td className="rc-admin__mono">
                      <AdminDate value={user.registeredAt} />
                    </td>
                    <td className={user.attempts === 0 ? 'rc-admin__num rc-admin__num--zero' : 'rc-admin__num'}>
                      {user.attempts > 0 ? (
                        <Link className="rc-admin__link" to={`/admin/attempts?user=${user.id}`} aria-label={`${t('admin.usersPage.showAttempts')}: ${user.email}`}>
                          {user.attempts}
                        </Link>
                      ) : (
                        user.attempts
                      )}
                    </td>
                    <td className={user.tasksSolved === 0 ? 'rc-admin__num rc-admin__num--zero' : 'rc-admin__num'}>{user.tasksSolved}</td>
                  </tr>
                ))}
              </tbody>
            </AdminTable>
            <Pager
              page={list}
              onPage={(next) => {
                setParams({ page: String(next) });
              }}
            />
          </>
        )}
      </ListBody>
    </AdminPanel>
  );
}
