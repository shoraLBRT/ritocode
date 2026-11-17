import type { CSSProperties } from 'react';
import type { Difficulty, TaskStatus, TaskSummary } from '../api';
import { useCountUp } from '../hooks/useCountUp';
import { useT } from '../i18n';
import { percent, solvedTasks } from '../pages/profile/profile';
import { Skeleton } from './Skeleton';

/**
 * The strip above the task list: where the learner stands before the list says what is left. Signed
 * in, the solved count, a bar of three parts (one per difficulty, as wide as that difficulty's share
 * of the tasks, filled by its solved share) and the started and revealed counts; a guest sees how
 * many tasks there are, by difficulty, and an invitation to sign in. Every label and count is a button
 * that sets the matching filter in the address. The counting is the profile's (`solvedTasks`).
 */
export function SolvedSummary({
  tasks,
  signedIn,
  difficulty,
  status,
  onDifficulty,
  onStatus,
}: {
  tasks: readonly TaskSummary[];
  signedIn: boolean;
  difficulty: Difficulty | 'all';
  status: 'all' | TaskStatus;
  onDifficulty: (value: Difficulty | 'all') => void;
  onStatus: (value: 'all' | TaskStatus) => void;
}) {
  const t = useT();
  const solved = solvedTasks(tasks);
  const shown = useCountUp(solved.solved);

  return (
    <section className="rc-summary" aria-label={t('tasks.summary.title')}>
      <p className="rc-summary__lead">
        {signedIn ? (
          <>
            <span className="rc-summary__word">{t('tasks.summary.solved')}</span>
            <span className="rc-summary__figure">{shown}</span>
            <span className="rc-summary__word">{t('tasks.summary.of', { total: solved.total })}</span>
          </>
        ) : (
          <span className="rc-summary__figure rc-summary__figure--text">{t('tasks.count', { count: solved.total })}</span>
        )}
      </p>

      <div className="rc-summary__levels">
        <div className="rc-summary__bar" role="img" aria-label={signedIn ? t('tasks.summary.bar', { solved: solved.solved, total: solved.total }) : undefined} aria-hidden={signedIn ? undefined : true}>
          {solved.byDifficulty.map((level) => (
            <span
              key={level.difficulty}
              className={`rc-summary__segment rc-summary__segment--${level.difficulty}`}
              style={{ flexGrow: level.total, '--p': signedIn ? percent(level.solved, level.total) : 0 } as CSSProperties}
            />
          ))}
        </div>
        <ul className="rc-summary__labels">
          {solved.byDifficulty.map((level) => (
            <li key={level.difficulty}>
              <button
                type="button"
                className={`rc-summary__link rc-summary__link--${level.difficulty}`}
                aria-pressed={difficulty === level.difficulty}
                onClick={() => {
                  onDifficulty(difficulty === level.difficulty ? 'all' : level.difficulty);
                }}
              >
                {signedIn
                  ? t('tasks.summary.level', { level: t(`tasks.summary.levels.${level.difficulty}`), solved: level.solved, total: level.total })
                  : t('tasks.summary.levelGuest', { level: t(`tasks.summary.levels.${level.difficulty}`), total: level.total })}
              </button>
            </li>
          ))}
        </ul>
      </div>

      {signedIn ? (
        <div className="rc-summary__counts">
          <button
            type="button"
            className="rc-summary__link"
            aria-pressed={status === 'tried'}
            onClick={() => {
              onStatus(status === 'tried' ? 'all' : 'tried');
            }}
          >
            {t('tasks.summary.tried', { count: solved.tried })}
          </button>
          <button
            type="button"
            className="rc-summary__link"
            aria-pressed={status === 'gaveUp'}
            onClick={() => {
              onStatus(status === 'gaveUp' ? 'all' : 'gaveUp');
            }}
          >
            {t('tasks.summary.gaveUp', { count: solved.gaveUp })}
          </button>
        </div>
      ) : (
        <p className="rc-summary__guest">{t('tasks.summary.guest')}</p>
      )}
    </section>
  );
}

/** The strip's shape while the list loads. */
export function SolvedSummarySkeleton() {
  return (
    <div className="rc-summary rc-summary--loading" aria-hidden="true">
      <Skeleton width={150} height={30} />
      <div className="rc-summary__levels">
        <Skeleton width="100%" height={8} />
        <Skeleton width="55%" height={14} />
      </div>
      <Skeleton width={150} height={14} />
    </div>
  );
}
