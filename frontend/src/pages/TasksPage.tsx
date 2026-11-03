import { useSearchParams } from 'react-router';
import { difficulties, listTasks, useApiClient } from '../api';
import type { Difficulty, TaskSummary } from '../api';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { Tabs, TaskTable } from '../components/TaskTable';
import { useApiResource } from '../hooks/useApiResource';
import { useT } from '../i18n';

type StatusFilter = 'all' | 'unsolved' | 'solved';

/**
 * `/tasks` — the task catalogue (docs/SPEC.md §4.3) in the arena's style (DESIGN.md › The arena):
 * every task in the task table, in the order the API serves, easy first, with difficulty tabs and —
 * for a signed-in learner — status tabs. **No class tags**: they would give the answer away.
 *
 * The catalogue is twenty tasks, so it is read in one request and filtered here; the API's largest
 * page is a hundred. The filters live in the address, so coming back from a task keeps them.
 */
export function TasksPage() {
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource((signal) => listTasks(client, { pageSize: 100 }, signal), [client]);

  return (
    <section className="page rc-arena">
      <header className="rc-page-head">
        <h1>{t('tasks.title')}</h1>
        <p>{t('tasks.lead')}</p>
      </header>

      <div className="rc-panel">
        {state.status === 'loading' && <LoadingState label={t('tasks.loading')} />}
        {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
        {state.status === 'success' && state.data.items.length === 0 && <p className="rc-arena__empty">{t('tasks.empty')}</p>}
        {state.status === 'success' && state.data.items.length > 0 && <TaskList tasks={state.data.items} />}
      </div>
    </section>
  );
}

function TaskList({ tasks }: { tasks: readonly TaskSummary[] }) {
  const t = useT();
  const [params, setParams] = useSearchParams();
  const difficulty = difficultyParam(params.get('difficulty'));
  // The API says `solved` only to a signed-in caller; without it there is nothing to filter by.
  const signedIn = tasks.some((task) => task.solved !== null);
  const status = signedIn ? statusParam(params.get('status')) : 'all';

  const ofDifficulty = (value: Difficulty | 'all') => tasks.filter((task) => value === 'all' || task.difficulty === value);
  const ofStatus = (list: readonly TaskSummary[], value: StatusFilter) =>
    list.filter((task) => value === 'all' || (value === 'solved') === task.solved);
  const shown = ofStatus(ofDifficulty(difficulty), status);

  // A filter at its default leaves the address; replacing the entry keeps the back button for pages.
  const choose = (name: 'difficulty' | 'status', value: string) => {
    const next = new URLSearchParams(params);
    if (value === 'all') {
      next.delete(name);
    } else {
      next.set(name, value);
    }
    setParams(next, { replace: true });
  };

  return (
    <>
      <div className="rc-panel__bar">
        <div className="rc-list-filters">
          <Tabs
            label={t('tasks.difficultyFilter')}
            options={(['all', ...difficulties] as const).map((value) => ({
              value,
              label: value === 'all' ? t('tasks.all') : t(`tasks.difficulty.${value}`),
              count: ofStatus(ofDifficulty(value), status).length,
            }))}
            value={difficulty}
            onChange={(value) => {
              choose('difficulty', value);
            }}
          />
          {signedIn && (
            <Tabs
              label={t('tasks.statusFilter')}
              options={(['all', 'unsolved', 'solved'] as const).map((value) => ({
                value,
                label: t(value === 'all' ? 'tasks.all' : value === 'solved' ? 'tasks.solvedOnly' : 'tasks.unsolvedOnly'),
                count: ofStatus(ofDifficulty(difficulty), value).length,
              }))}
              value={status}
              onChange={(value) => {
                choose('status', value);
              }}
            />
          )}
        </div>
        <span className="rc-arena__note" role="status">
          {t('tasks.count', { count: shown.length })}
        </span>
      </div>

      {shown.length === 0 ? <p className="rc-arena__empty">{t('tasks.noMatch')}</p> : <TaskTable tasks={shown} />}
    </>
  );
}

function difficultyParam(value: string | null): Difficulty | 'all' {
  return difficulties.find((item) => item === value) ?? 'all';
}

function statusParam(value: string | null): StatusFilter {
  return value === 'solved' || value === 'unsolved' ? value : 'all';
}
