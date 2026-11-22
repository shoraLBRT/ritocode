import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router';
import { useApiClient } from '../../api';
import { Tabs } from '../../components/TaskTable';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { getAdminError, listAdminErrors, resolveAdminError } from './adminErrors';
import type { AdminErrorGroup, AdminErrorOccurrence, ErrorGroupStatus } from './adminErrors';
import { ADMIN_PAGE_SIZE, pageParam } from './paging';
import { AdminDate, AdminPanel, AdminTable, AdminTotal, ListBody, Pager } from './shared';

/** How long a resolved row takes to leave: the `--dur` token of the fade in admin.css. */
const FADE_MS = 200;

/**
 * `/admin/errors` — the error groups (ADR 0020, #294): server exceptions and browser errors, the most
 * recently seen first, with their count, first and last time, and the latest request ids, which find their
 * lines in the logs (RUNBOOK › An error was reported). One action, marking a group resolved; a resolved group
 * that happens again comes back here. The filter and the page live in the address.
 */
export function AdminErrorsPage() {
  const client = useApiClient();
  const t = useT();
  const [params, setParams] = useSearchParams();
  const status: ErrorGroupStatus = params.get('status') === 'resolved' ? 'resolved' : 'open';
  const page = pageParam(params.get('page'));

  const { state, reload } = useApiResource(
    (signal) => listAdminErrors(client, { status, page, pageSize: ADMIN_PAGE_SIZE }, signal),
    [client, status, page],
  );

  return (
    <AdminPanel
      title={t('admin.errors')}
      lead={t('admin.errorsPage.lead')}
      filters={
        <Tabs
          label={t('admin.errorsPage.status')}
          options={[
            { value: 'open', label: t('admin.errorsPage.open') },
            { value: 'resolved', label: t('admin.errorsPage.resolved') },
          ]}
          value={status}
          onChange={(value) => {
            setParams(value === 'resolved' ? { status: 'resolved' } : {});
          }}
        />
      }
      total={state.status === 'success' ? <AdminTotal count={state.data.totalItems} /> : null}
    >
      <ListBody state={state} reload={reload} empty={t('admin.errorsPage.empty')} columns={6} icon="flag">
        {(list) => (
          <>
            <AdminTable label={t('admin.errors')}>
              <thead>
                <tr>
                  <th scope="col">{t('admin.errorsPage.error')}</th>
                  <th scope="col">{t('admin.errorsPage.where')}</th>
                  <th scope="col">{t('admin.errorsPage.count')}</th>
                  <th scope="col">{t('admin.errorsPage.seen')}</th>
                  <th scope="col">{t('admin.errorsPage.requestIds')}</th>
                  <th scope="col">{status === 'open' ? t('admin.errorsPage.state') : t('admin.errorsPage.resolvedAt')}</th>
                </tr>
              </thead>
              <tbody>
                {list.items.map((item) => (
                  <ErrorRow key={item.id} item={item} onResolved={reload} />
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

function ErrorRow({ item, onResolved }: { item: AdminErrorGroup; onResolved: () => void }) {
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
    resolveAdminError(client, item.id).then(
      () => {
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
      <td className="rc-admin__comment rc-admin-errors__error">
        <span className={item.source === 'server' ? 'rc-admin__chip rc-admin__chip--warn' : 'rc-admin__chip'}>
          {item.source === 'server' ? t('admin.errorsPage.server') : t('admin.errorsPage.browser')}
        </span>{' '}
        <strong className="rc-admin__mono">{item.type}</strong>
        <span className="rc-admin-errors__message">{item.message}</span>
        <LatestStack id={item.id} />
      </td>
      <td className="rc-admin__mono rc-admin-errors__where">{item.location}</td>
      <td className="rc-admin__mono">{item.count}</td>
      <td className="rc-admin__mono">
        <AdminDate value={item.firstSeenAt} />
        <br />
        <AdminDate value={item.lastSeenAt} />
      </td>
      <td className="rc-admin__mono rc-admin-errors__ids">
        {item.latestRequestIds.map((id) => (
          <code key={id}>{id}</code>
        ))}
      </td>
      <td>
        {item.resolvedAt !== null ? (
          <span className="rc-admin__chip rc-admin__chip--good rc-admin__mono">
            <AdminDate value={item.resolvedAt} />
          </span>
        ) : (
          <span className="rc-admin__action">
            <button type="button" className="button button--small" disabled={resolving} aria-busy={resolving} onClick={resolve}>
              {resolving ? t('admin.errorsPage.resolving') : t('admin.errorsPage.resolve')}
            </button>
            {failed && (
              <span className="rc-admin__error" role="alert">
                {t('admin.errorsPage.resolveFailed')}
              </span>
            )}
          </span>
        )}
      </td>
    </tr>
  );
}

/** The latest occurrence's stack, read only when it is opened. */
function LatestStack({ id }: { id: string }) {
  const client = useApiClient();
  const t = useT();
  const [state, setState] = useState<'closed' | 'loading' | 'failed' | AdminErrorOccurrence | null>('closed');

  const open = () => {
    // Read once; a read that failed is tried again the next time the stack is opened.
    if (state !== 'closed' && state !== 'failed') {
      return;
    }
    setState('loading');
    getAdminError(client, id).then(
      (detail) => {
        setState(detail.occurrences[0] ?? null);
      },
      () => {
        setState('failed');
      },
    );
  };

  return (
    <details
      className="rc-admin-errors__stack"
      onToggle={(event) => {
        if (event.currentTarget.open) {
          open();
        }
      }}
    >
      <summary>{t('admin.errorsPage.details')}</summary>
      {state === 'loading' && <p className="rc-admin__muted">{t('admin.errorsPage.loadingStack')}</p>}
      {state === 'failed' && <p className="rc-admin__error">{t('admin.errorsPage.stackFailed')}</p>}
      {state === null && <p className="rc-admin__muted">{t('admin.errorsPage.noStack')}</p>}
      {typeof state === 'object' && state !== null && (
        <>
          {state.commit !== null && <p className="rc-admin__muted">{t('admin.errorsPage.commit', { commit: state.commit })}</p>}
          {state.stack !== null ? <pre>{state.stack}</pre> : <p className="rc-admin__muted">{t('admin.errorsPage.noStack')}</p>}
        </>
      )}
    </details>
  );
}
