import { Link, useSearchParams } from 'react-router';
import { listAdminAttempts, useApiClient } from '../../api';
import type { AdminAttempt, AdminAttemptStatus } from '../../api';
import { Tabs } from '../../components/TaskTable';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { Icon } from '../task/parts';
import { ADMIN_PAGE_SIZE, pageParam } from './paging';
import { AdminDate, AdminPanel, AdminTable, AdminTotal, LearnerName, ListBody, Pager } from './shared';

const STATUSES: readonly AdminAttemptStatus[] = ['all', 'open', 'submitted'];

/**
 * `/admin/attempts` — every attempt, newest first (docs/SPEC.md §6.2): learner, task, first or
 * practice, started, submitted, time taken, score, and for one never submitted the step reached —
 * "where they gave up". `?user=` narrows it to one learner, as the list of users links it.
 */
export function AdminAttemptsPage() {
  const client = useApiClient();
  const t = useT();
  const [params, setParams] = useSearchParams();
  const requested = params.get('status');
  const status: AdminAttemptStatus = STATUSES.find((value) => value === requested) ?? 'all';
  const user = params.get('user') ?? undefined;
  const page = pageParam(params.get('page'));

  const { state, reload } = useApiResource(
    (signal) => listAdminAttempts(client, { status, ...(user ? { user } : {}), page, pageSize: ADMIN_PAGE_SIZE }, signal),
    [client, status, user, page],
  );

  const address = (changes: { status?: AdminAttemptStatus; user?: string | undefined; page?: number }) => {
    const next = { status, user, page: 1, ...changes };
    return {
      ...(next.status !== 'all' ? { status: next.status } : {}),
      ...(next.user ? { user: next.user } : {}),
      ...(next.page > 1 ? { page: String(next.page) } : {}),
    };
  };

  const learner = state.status === 'success' && user ? state.data.items[0]?.learner : undefined;

  return (
    <AdminPanel
      title={t('admin.attempts')}
      lead={t('admin.attemptsPage.lead')}
      filters={
        <>
          <Tabs
            label={t('admin.attemptsPage.status')}
            options={STATUSES.map((value) => ({ value, label: t(`admin.attemptsPage.${value}`) }))}
            value={status}
            onChange={(value) => {
              setParams(address({ status: value }));
            }}
          />
          {user && (
            <span className="rc-admin__chip rc-admin__chip--filter">
              <span>{t('admin.attemptsPage.forLearner', { learner: learner ? (learner.email ?? learner.id) : user })}</span>
              <Link
                to={{ search: new URLSearchParams(address({ user: undefined })).toString() }}
                aria-label={t('admin.attemptsPage.allLearners')}
                title={t('admin.attemptsPage.allLearners')}
              >
                <Icon name="close" />
              </Link>
            </span>
          )}
        </>
      }
      total={state.status === 'success' ? <AdminTotal count={state.data.totalItems} /> : null}
    >
      <ListBody state={state} reload={reload} empty={t('admin.attemptsPage.empty')}>
        {(list) => (
          <>
            <AdminTable label={t('admin.attempts')}>
              <thead>
                <tr>
                  <th scope="col">{t('admin.attemptsPage.started')}</th>
                  <th scope="col">{t('admin.attemptsPage.learner')}</th>
                  <th scope="col">{t('admin.attemptsPage.task')}</th>
                  <th scope="col">{t('admin.attemptsPage.kind')}</th>
                  <th scope="col">{t('admin.attemptsPage.step')}</th>
                  <th scope="col">{t('admin.attemptsPage.submittedColumn')}</th>
                  <th scope="col" className="rc-admin__num">
                    {t('admin.attemptsPage.timeTaken')}
                  </th>
                  <th scope="col" className="rc-admin__num">
                    {t('admin.attemptsPage.score')}
                  </th>
                </tr>
              </thead>
              <tbody>
                {list.items.map((attempt) => (
                  <AttemptRow key={attempt.id} attempt={attempt} />
                ))}
              </tbody>
            </AdminTable>
            <Pager
              page={list}
              onPage={(next) => {
                setParams(address({ page: next }));
              }}
            />
          </>
        )}
      </ListBody>
    </AdminPanel>
  );
}

/** Time taken: minutes and seconds, and from an hour hours and minutes, so a forgotten tab stays short. */
function takenText(t: ReturnType<typeof useT>, seconds: number): string {
  return seconds >= 3600
    ? t('admin.attemptsPage.hours', { hours: Math.floor(seconds / 3600), minutes: Math.floor((seconds % 3600) / 60) })
    : t('admin.attemptsPage.minutes', { minutes: Math.floor(seconds / 60), seconds: seconds % 60 });
}

function None() {
  const t = useT();

  return <span className="rc-admin__muted">{t('admin.none')}</span>;
}

function AttemptRow({ attempt }: { attempt: AdminAttempt }) {
  const t = useT();
  const submitted = attempt.submittedAt !== null;

  return (
    <tr className="rc-admin__row">
      <td className="rc-admin__mono">
        <AdminDate value={attempt.startedAt} />
      </td>
      <td className="rc-admin__text">
        <LearnerName learner={attempt.learner} />
      </td>
      <td>
        <Link className="rc-admin__slug" to={`/tasks/${attempt.task}`}>
          {attempt.task}
        </Link>
      </td>
      <td>
        {submitted ? (
          <span className={attempt.practice ? 'rc-admin__chip' : 'rc-admin__chip rc-admin__chip--first'}>
            {t(attempt.practice ? 'admin.attemptsPage.practice' : 'admin.attemptsPage.first')}
          </span>
        ) : (
          <span className="rc-admin__chip rc-admin__chip--warn">{t('admin.attemptsPage.notSubmitted')}</span>
        )}
      </td>
      {/* A submitted attempt has been through both steps; the step says something only for one that was not. */}
      <td>{submitted ? <None /> : <span className="rc-admin__chip">{t(`admin.attemptsPage.stepName.${attempt.step}`)}</span>}</td>
      <td className="rc-admin__mono">{attempt.submittedAt !== null ? <AdminDate value={attempt.submittedAt} /> : <None />}</td>
      <td className="rc-admin__num">
        {attempt.timeTakenSeconds !== null ? (
          takenText(t, attempt.timeTakenSeconds)
        ) : (
          <None />
        )}
      </td>
      <td className="rc-admin__num">
        {attempt.score !== null && attempt.maxScore !== null ? t('admin.attemptsPage.scoreOf', { score: attempt.score, max: attempt.maxScore }) : <None />}
      </td>
    </tr>
  );
}
