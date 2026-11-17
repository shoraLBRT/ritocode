import { Link } from 'react-router';
import { listAdminAttempts, listAdminSignals, listAdminUsers, useApiClient } from '../../api';
import type { Page } from '../../api';
import { Skeleton } from '../../components/Skeleton';
import { useApiResource } from '../../hooks/useApiResource';
import type { ApiResourceState } from '../../hooks/useApiResource';
import { useCountUp } from '../../hooks/useCountUp';
import { useT } from '../../i18n';
import { AdminDate, AdminPanel, AdminTable, LearnerName, ListBody } from './shared';

/** How many of the newest open signals the overview lists. */
const LATEST = 5;

/**
 * `/admin` — the overview: four tiles, each a count read as the `totalItems` of a one-row page of its
 * own list and leading to that list, and the newest open signals under them. No data of its own
 * (docs/SPEC.md §6.2). A tile that fails says so and does not blank the page.
 */
export function AdminOverviewPage() {
  const client = useApiClient();
  const t = useT();

  const signals = useApiResource((signal) => listAdminSignals(client, { status: 'open', page: 1, pageSize: LATEST }, signal), [client]);
  const users = useApiResource((signal) => listAdminUsers(client, { page: 1, pageSize: 1 }, signal), [client]);
  const submitted = useApiResource((signal) => listAdminAttempts(client, { status: 'submitted', page: 1, pageSize: 1 }, signal), [client]);
  const notSubmitted = useApiResource((signal) => listAdminAttempts(client, { status: 'open', page: 1, pageSize: 1 }, signal), [client]);

  return (
    <>
      <ul className="rc-admin__tiles" aria-label={t('admin.overviewPage.tiles')}>
        <Tile to="/admin/signals" label={t('admin.overviewPage.signals')} hint={t('admin.overviewPage.signalsHint')} state={signals.state} />
        <Tile to="/admin/users" label={t('admin.overviewPage.users')} hint={t('admin.overviewPage.usersHint')} state={users.state} />
        <Tile
          to="/admin/attempts?status=submitted"
          label={t('admin.overviewPage.submitted')}
          hint={t('admin.overviewPage.submittedHint')}
          state={submitted.state}
        />
        <Tile
          to="/admin/attempts?status=open"
          label={t('admin.overviewPage.notSubmitted')}
          hint={t('admin.overviewPage.notSubmittedHint')}
          state={notSubmitted.state}
        />
      </ul>

      <AdminPanel
        title={t('admin.overviewPage.latest')}
        total={
          <Link className="rc-admin__more" to="/admin/signals">
            {t('admin.overviewPage.allSignals')}
          </Link>
        }
      >
        <ListBody state={signals.state} reload={signals.reload} empty={t('admin.overviewPage.empty')}>
          {(list) => (
            <AdminTable label={t('admin.overviewPage.latest')}>
              <thead>
                <tr>
                  <th scope="col">{t('admin.signalsPage.date')}</th>
                  <th scope="col">{t('admin.signalsPage.task')}</th>
                  <th scope="col">{t('admin.signalsPage.card')}</th>
                  <th scope="col">{t('admin.signalsPage.learner')}</th>
                </tr>
              </thead>
              <tbody>
                {list.items.map((item) => (
                  <tr key={item.id} className="rc-admin__row">
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
                  </tr>
                ))}
              </tbody>
            </AdminTable>
          )}
        </ListBody>
      </AdminPanel>
    </>
  );
}

function Tile({ to, label, hint, state }: { to: string; label: string; hint: string; state: ApiResourceState<Page<unknown>> }) {
  const t = useT();

  return (
    <li>
      <Link className="rc-admin__tile" to={to}>
        <span className="rc-admin__tile-label">{label}</span>
        {state.status === 'success' ? <TileValue value={state.data.totalItems} /> : null}
        {state.status === 'loading' ? <Skeleton className="rc-admin__tile-skeleton" height={32} width={72} /> : null}
        {state.status === 'error' ? <span className="rc-admin__tile-value rc-admin__tile-value--none">{t('admin.none')}</span> : null}
        <span className="rc-admin__tile-hint">{state.status === 'error' ? t('admin.overviewPage.tileFailed') : hint}</span>
      </Link>
    </li>
  );
}

function TileValue({ value }: { value: number }) {
  const shown = useCountUp(value);

  return <span className="rc-admin__tile-value">{shown}</span>;
}
