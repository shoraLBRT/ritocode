import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { listAdminSignals, resolveSignal, useApiClient } from '../../api';
import type { AdminSignal, AdminSignalStatus } from '../../api';
import { Tabs } from '../../components/TaskTable';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { useAdmin } from './adminContext';
import { ADMIN_PAGE_SIZE, pageParam } from './paging';
import { AdminDate, AdminPanel, AdminTable, AdminTotal, LearnerName, ListBody, Pager } from './shared';

/** How long a resolved row takes to leave: the `--dur` token of the fade in admin.css. */
const FADE_MS = 200;

/**
 * `/admin/signals` — every signal, newest first (docs/SPEC.md §6.2): task, card, learner, comment,
 * date; open or resolved; one action, marking it resolved. The author changes the task in the content
 * repository, not here. The filter and the page live in the address, so a reload keeps them.
 */
export function AdminSignalsPage() {
  const client = useApiClient();
  const t = useT();
  const { openSignals, reloadOpenSignals } = useAdmin();
  const [params, setParams] = useSearchParams();
  const status: AdminSignalStatus = params.get('status') === 'resolved' ? 'resolved' : 'open';
  const page = pageParam(params.get('page'));

  const { state, reload } = useApiResource(
    (signal) => listAdminSignals(client, { status, page, pageSize: ADMIN_PAGE_SIZE }, signal),
    [client, status, page],
  );

  const resolved = () => {
    reload();
    reloadOpenSignals();
  };

  return (
    <AdminPanel
      title={t('admin.signals')}
      lead={t('admin.signalsPage.lead')}
      filters={
        <Tabs
          label={t('admin.signalsPage.status')}
          options={[
            { value: 'open', label: t('admin.signalsPage.open'), ...(openSignals !== null ? { count: openSignals } : {}) },
            { value: 'resolved', label: t('admin.signalsPage.resolved') },
          ]}
          value={status}
          onChange={(value) => {
            setParams(value === 'resolved' ? { status: 'resolved' } : {});
          }}
        />
      }
      total={state.status === 'success' ? <AdminTotal count={state.data.totalItems} /> : null}
    >
      <ListBody state={state} reload={reload} empty={t('admin.signalsPage.empty')} columns={7} icon="flag">
        {(list) => (
          <>
            <AdminTable label={t('admin.signals')}>
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
                {list.items.map((item) => (
                  <SignalRow key={item.id} item={item} onResolved={resolved} />
                ))}
              </tbody>
            </AdminTable>
            <Pager
              page={list}
              onPage={(next) => {
                setParams({ ...(status === 'resolved' ? { status } : {}), page: String(next) });
              }}
            />
          </>
        )}
      </ListBody>
    </AdminPanel>
  );
}

function SignalRow({ item, onResolved }: { item: AdminSignal; onResolved: () => void }) {
  const client = useApiClient();
  const t = useT();
  const [resolving, setResolving] = useState(false);
  const [leaving, setLeaving] = useState(false);
  const [failed, setFailed] = useState(false);
  const timer = useRef<number | undefined>(undefined);

  useEffect(
    () => () => {
      window.clearTimeout(timer.current);
    },
    [],
  );

  const resolve = () => {
    setResolving(true);
    setFailed(false);
    resolveSignal(client, item.id).then(
      () => {
        // The row fades, and only then is the list read again, so it does not jump while it goes.
        setLeaving(true);
        timer.current = window.setTimeout(onResolved, FADE_MS);
      },
      () => {
        setResolving(false);
        setFailed(true);
      },
    );
  };

  return (
    <tr className={leaving ? 'rc-admin__row rc-admin__row--leaving' : 'rc-admin__row'}>
      <td className="rc-admin__mono">
        <AdminDate value={item.createdAt} />
      </td>
      <td>
        <Link className="rc-admin__slug" to={`/tasks/${item.task}`}>
          {item.task}
        </Link>
      </td>
      <td>
        <Link className="rc-admin__link" to={`/problems#${item.card}`}>
          {item.cardName}
        </Link>
      </td>
      <td className="rc-admin__text">
        <LearnerName learner={item.learner} />
      </td>
      <td className="rc-admin__comment">{item.comment ?? <span className="rc-admin__muted">{t('admin.signalsPage.noComment')}</span>}</td>
      <td>
        {item.resolvedAt !== null ? (
          <span className="rc-admin__chip rc-admin__chip--good rc-admin__mono">
            <AdminDate value={item.resolvedAt} />
          </span>
        ) : (
          <span className="rc-admin__action">
            <button type="button" className="button button--small" disabled={resolving} aria-busy={resolving} onClick={resolve}>
              {resolving ? t('admin.signalsPage.resolving') : t('admin.signalsPage.resolve')}
            </button>
            {failed && (
              <span className="rc-admin__error" role="alert">
                {t('admin.signalsPage.resolveFailed')}
              </span>
            )}
          </span>
        )}
      </td>
    </tr>
  );
}
