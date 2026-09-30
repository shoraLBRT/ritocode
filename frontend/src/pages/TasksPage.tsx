import { useState } from 'react';
import { Link } from 'react-router';
import { listTasks, useApiClient } from '../api';
import type { Difficulty, TaskSummary } from '../api';
import { EmptyState } from '../components/EmptyState';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { useApiResource } from '../hooks/useApiResource';
import { useT } from '../i18n';

const difficulties: readonly Difficulty[] = ['easy', 'medium', 'hard'];

type SolvedFilter = 'all' | 'solved' | 'unsolved';

/**
 * `/tasks` — the task catalogue (docs/SPEC.md §4.3): title, difficulty, the time a task takes, and
 * for a signed-in learner whether they solved it. **No class tags**: they would give the answer
 * away. In the order the API serves, easy first.
 *
 * The catalogue is twenty tasks, so it is read in one request and filtered here; the API's largest
 * page is a hundred.
 */
export function TasksPage() {
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource((signal) => listTasks(client, { pageSize: 100 }, signal), [client]);
  const [difficulty, setDifficulty] = useState<Difficulty | 'all'>('all');
  const [solved, setSolved] = useState<SolvedFilter>('all');

  return (
    <section className="page">
      <h1>{t('tasks.title')}</h1>
      <p className="page__lead">{t('tasks.lead')}</p>

      {state.status === 'loading' && <LoadingState label={t('tasks.loading')} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && state.data.items.length === 0 && <EmptyState>{t('tasks.empty')}</EmptyState>}
      {state.status === 'success' && state.data.items.length > 0 && (
        <TaskList
          tasks={state.data.items}
          difficulty={difficulty}
          solved={solved}
          onDifficulty={setDifficulty}
          onSolved={setSolved}
        />
      )}
    </section>
  );
}

function TaskList({
  tasks,
  difficulty,
  solved,
  onDifficulty,
  onSolved,
}: {
  tasks: readonly TaskSummary[];
  difficulty: Difficulty | 'all';
  solved: SolvedFilter;
  onDifficulty: (value: Difficulty | 'all') => void;
  onSolved: (value: SolvedFilter) => void;
}) {
  const t = useT();

  // The API says `solved` only to a signed-in caller; without it there is nothing to filter by.
  const signedIn = tasks.some((task) => task.solved !== null);

  const shown = tasks.filter(
    (task) =>
      (difficulty === 'all' || task.difficulty === difficulty)
      && (solved === 'all' || !signedIn || (solved === 'solved') === task.solved),
  );

  return (
    <>
      <div className="filters" role="group" aria-label={t('tasks.filters')}>
        <label className="filters__field">
          <span>{t('tasks.difficultyFilter')}</span>
          <select
            value={difficulty}
            onChange={(event) => {
              onDifficulty(event.target.value as Difficulty | 'all');
            }}
          >
            <option value="all">{t('tasks.all')}</option>
            {difficulties.map((value) => (
              <option key={value} value={value}>
                {t(`tasks.difficulty.${value}`)}
              </option>
            ))}
          </select>
        </label>

        {signedIn && (
          <label className="filters__field">
            <span>{t('tasks.solvedFilter')}</span>
            <select
              value={solved}
              onChange={(event) => {
                onSolved(event.target.value as SolvedFilter);
              }}
            >
              <option value="all">{t('tasks.all')}</option>
              <option value="solved">{t('tasks.solvedOnly')}</option>
              <option value="unsolved">{t('tasks.unsolvedOnly')}</option>
            </select>
          </label>
        )}

        <span className="filters__count">{t('tasks.count', { count: shown.length })}</span>
      </div>

      {shown.length === 0 ? (
        <EmptyState>{t('tasks.noMatch')}</EmptyState>
      ) : (
        <ul className="task-list">
          {shown.map((task) => (
            <li key={task.slug} className="task-list__item">
              <Link className="task-list__title" to={`/tasks/${task.slug}`}>
                {task.title}
              </Link>
              <span className="task-list__meta">
                <span className="badge">{t(`tasks.difficulty.${task.difficulty}`)}</span>
                <span>{t(`tasks.time.${task.difficulty}`)}</span>
                {task.solved === true && <span className="badge badge--done">{t('tasks.solved')}</span>}
              </span>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
