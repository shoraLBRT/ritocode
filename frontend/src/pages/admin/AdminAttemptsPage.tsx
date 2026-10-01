import { Link, useSearchParams } from 'react-router';
import { listAdminAttempts, useApiClient } from '../../api';
import type { AdminAttempt, AdminAttemptStatus } from '../../api';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { ADMIN_PAGE_SIZE, pageParam } from './paging';
import { AdminDate, LearnerName, Pager } from './shared';

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
    <>
      <h1>{t('admin.attempts')}</h1>
      <p className="page__lead">{t('admin.attemptsPage.lead')}</p>

      <div className="filters">
        <label className="filters__field">
          <span>{t('admin.attemptsPage.status')}</span>
          <select
            value={status}
            onChange={(event) => {
              setParams(address({ status: event.target.value as AdminAttemptStatus }));
            }}
          >
            {STATUSES.map((value) => (
              <option key={value} value={value}>
                {t(`admin.attemptsPage.${value}`)}
              </option>
            ))}
          </select>
        </label>
        {user && (
          <p className="filters__count">
            {learner ? t('admin.attemptsPage.forLearner', { learner: learner.email ?? learner.id }) : null}{' '}
            <Link to={{ search: new URLSearchParams(address({ user: undefined })).toString() }}>{t('admin.attemptsPage.allLearners')}</Link>
          </p>
        )}
      </div>

      {state.status === 'loading' && <LoadingState label={t('admin.loading')} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && state.data.items.length === 0 && <EmptyState>{t('admin.attemptsPage.empty')}</EmptyState>}
      {state.status === 'success' && state.data.items.length > 0 && (
        <>
          <div className="table-scroll" role="region" aria-label={t('admin.attempts')} tabIndex={0}>
            <table className="admin-table">
              <thead>
                <tr>
                  <th scope="col">{t('admin.attemptsPage.started')}</th>
                  <th scope="col">{t('admin.attemptsPage.learner')}</th>
                  <th scope="col">{t('admin.attemptsPage.task')}</th>
                  <th scope="col">{t('admin.attemptsPage.kind')}</th>
                  <th scope="col">{t('admin.attemptsPage.step')}</th>
                  <th scope="col">{t('admin.attemptsPage.submittedColumn')}</th>
                  <th scope="col">{t('admin.attemptsPage.timeTaken')}</th>
                  <th scope="col">{t('admin.attemptsPage.score')}</th>
                </tr>
              </thead>
              <tbody>
                {state.data.items.map((attempt) => (
                  <AttemptRow key={attempt.id} attempt={attempt} />
                ))}
              </tbody>
            </table>
          </div>
          <Pager
            page={state.data}
            onPage={(next) => {
              setParams(address({ page: next }));
            }}
          />
        </>
      )}
    </>
  );
}

function AttemptRow({ attempt }: { attempt: AdminAttempt }) {
  const t = useT();
  const submitted = attempt.submittedAt !== null;

  return (
    <tr className={submitted ? undefined : 'admin-table__open'}>
      <td>
        <AdminDate value={attempt.startedAt} />
      </td>
      <td>
        <LearnerName learner={attempt.learner} />
      </td>
      <td>
        <Link to={`/tasks/${attempt.task}`}>{attempt.task}</Link>
      </td>
      <td>{submitted ? t(attempt.practice ? 'admin.attemptsPage.practice' : 'admin.attemptsPage.first') : t('admin.attemptsPage.notSubmitted')}</td>
      {/* A submitted attempt has been through both steps; the step says something only for one that was not. */}
      <td>{submitted ? t('admin.none') : t(`admin.attemptsPage.stepName.${attempt.step}`)}</td>
      <td>{attempt.submittedAt !== null ? <AdminDate value={attempt.submittedAt} /> : t('admin.none')}</td>
      <td className="admin-table__number">
        {attempt.timeTakenSeconds !== null
          ? t('admin.attemptsPage.minutes', { minutes: Math.floor(attempt.timeTakenSeconds / 60), seconds: attempt.timeTakenSeconds % 60 })
          : t('admin.none')}
      </td>
      <td className="admin-table__number">
        {attempt.score !== null && attempt.maxScore !== null ? t('admin.attemptsPage.scoreOf', { score: attempt.score, max: attempt.maxScore }) : t('admin.none')}
      </td>
    </tr>
  );
}
