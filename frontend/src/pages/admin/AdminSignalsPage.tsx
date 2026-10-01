import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { listAdminSignals, resolveSignal, useApiClient } from '../../api';
import type { AdminSignal, AdminSignalStatus } from '../../api';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { ADMIN_PAGE_SIZE, pageParam } from './paging';
import { AdminDate, LearnerName, Pager } from './shared';

/**
 * `/admin/signals` — every signal, newest first (docs/SPEC.md §6.2): task, card, learner, comment,
 * date; open or resolved; one action, marking it resolved. The author changes the task in the content
 * repository, not here. The filter and the page live in the address, so a reload keeps them.
 */
export function AdminSignalsPage() {
  const client = useApiClient();
  const t = useT();
  const [params, setParams] = useSearchParams();
  const status: AdminSignalStatus = params.get('status') === 'resolved' ? 'resolved' : 'open';
  const page = pageParam(params.get('page'));

  const { state, reload } = useApiResource(
    (signal) => listAdminSignals(client, { status, page, pageSize: ADMIN_PAGE_SIZE }, signal),
    [client, status, page],
  );

  return (
    <>
      <h1>{t('admin.signals')}</h1>
      <p className="page__lead">{t('admin.signalsPage.lead')}</p>

      <div className="filters">
        <label className="filters__field">
          <span>{t('admin.signalsPage.status')}</span>
          <select
            value={status}
            onChange={(event) => {
              setParams(event.target.value === 'resolved' ? { status: 'resolved' } : {});
            }}
          >
            <option value="open">{t('admin.signalsPage.open')}</option>
            <option value="resolved">{t('admin.signalsPage.resolved')}</option>
          </select>
        </label>
      </div>

      {state.status === 'loading' && <LoadingState label={t('admin.loading')} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && state.data.items.length === 0 && <EmptyState>{t('admin.signalsPage.empty')}</EmptyState>}
      {state.status === 'success' && state.data.items.length > 0 && (
        <>
          <div className="table-scroll" role="region" aria-label={t('admin.signals')} tabIndex={0}>
            <table className="admin-table">
              <thead>
                <tr>
                  <th scope="col">{t('admin.signalsPage.date')}</th>
                  <th scope="col">{t('admin.signalsPage.task')}</th>
                  <th scope="col">{t('admin.signalsPage.card')}</th>
                  <th scope="col">{t('admin.signalsPage.learner')}</th>
                  <th scope="col">{t('admin.signalsPage.comment')}</th>
                  <th scope="col">{status === 'open' ? t('admin.signalsPage.state') : t('admin.signalsPage.resolvedAt')}</th>
                </tr>
              </thead>
              <tbody>
                {state.data.items.map((item) => (
                  <SignalRow key={item.id} item={item} onResolved={reload} />
                ))}
              </tbody>
            </table>
          </div>
          <Pager
            page={state.data}
            onPage={(next) => {
              setParams({ ...(status === 'resolved' ? { status } : {}), page: String(next) });
            }}
          />
        </>
      )}
    </>
  );
}

function SignalRow({ item, onResolved }: { item: AdminSignal; onResolved: () => void }) {
  const client = useApiClient();
  const t = useT();
  const [resolving, setResolving] = useState(false);
  const [failed, setFailed] = useState(false);

  return (
    <tr>
      <td>
        <AdminDate value={item.createdAt} />
      </td>
      <td>
        <Link to={`/tasks/${item.task}`}>{item.task}</Link>
      </td>
      <td>
        <Link to={`/problems#${item.card}`}>{item.cardName}</Link>
      </td>
      <td>
        <LearnerName learner={item.learner} />
      </td>
      <td className="admin-table__text">{item.comment ?? <span className="admin-muted">{t('admin.signalsPage.noComment')}</span>}</td>
      <td>
        {item.resolvedAt !== null ? (
          <AdminDate value={item.resolvedAt} />
        ) : (
          <>
            <button
              type="button"
              className="button button--small"
              disabled={resolving}
              onClick={() => {
                setResolving(true);
                setFailed(false);
                resolveSignal(client, item.id).then(onResolved, () => {
                  setResolving(false);
                  setFailed(true);
                });
              }}
            >
              {resolving ? t('admin.signalsPage.resolving') : t('admin.signalsPage.resolve')}
            </button>
            {failed && (
              <span className="admin-table__error" role="alert">
                {t('admin.signalsPage.resolveFailed')}
              </span>
            )}
          </>
        )}
      </td>
    </tr>
  );
}
