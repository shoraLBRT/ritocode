import { useState } from 'react';
import type { CSSProperties } from 'react';
import { Link } from 'react-router';
import { getProgress, listAttempts, listAttemptsSince, listTasks, useApiClient } from '../../api';
import type { AttemptSummary, Page, Progress, TaskSummary } from '../../api';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { useApiResource } from '../../hooks/useApiResource';
import type { ApiResource } from '../../hooks/useApiResource';
import { useCountUp } from '../../hooks/useCountUp';
import { useT } from '../../i18n';
import { useSession } from '../../session';
import { activity, yearSince } from '../home/arena';
import { ActivityCard } from './ActivityCard';
import { HistoryPanel } from './HistoryPanel';
import { CardLoading, ProfileCard } from './parts';
import { diagnosis, percent, solvedTasks } from './profile';
import './profile.css';

/** How many attempts the recent list shows. */
export const RECENT_ATTEMPTS = 10;

/**
 * `/profile` — the learner's own page (docs/SPEC.md §4.7, DESIGN.md › The profile): who they are,
 * the classes, the tasks solved by difficulty, the diagnosis, a year of activity and the recent
 * attempts and cards. Everything is read from what the API already serves — the task list, the
 * progress and the attempts — each card on its own, so one failed read does not blank the page.
 * No XP, no levels, no leaderboard.
 *
 * Signed in only: it sits under `RequireSignIn`. Two columns from 64rem; on a phone one, in the order
 * person, solved, diagnosis, classes, activity, the tabs (profile.css).
 */
export function ProfilePage() {
  const client = useApiClient();
  const [now] = useState(() => new Date());
  const tasks = useApiResource((signal) => listTasks(client, { pageSize: 100 }, signal), [client]);
  const progress = useApiResource((signal) => getProgress(client, signal), [client]);
  const year = useApiResource((signal) => listAttemptsSince(client, yearSince(now), signal), [client, now]);
  // Opening a task starts an attempt, so most of a learner's attempts may never be submitted; the
  // list is of answers given, read from a page large enough to find ten of them.
  const recent = useApiResource(
    (signal) =>
      listAttempts(client, { pageSize: 50 }, signal).then((page) => page.items.filter((attempt) => attempt.submittedAt !== null).slice(0, RECENT_ATTEMPTS)),
    [client],
  );

  // A learner who has not begun: the same frame, and an invitation where the history would be.
  const fresh =
    progress.state.status === 'success' &&
    progress.state.data.tasks === 0 &&
    recent.state.status === 'success' &&
    recent.state.data.length === 0;

  return (
    <div className="rc-profile">
      <div className="rc-profile__side">
        <PersonCard year={year} now={now} />
        <ClassesCard progress={progress} />
      </div>
      <div className="rc-profile__main">
        <div className="rc-profile__row">
          <SolvedCard tasks={tasks} />
          <DiagnosisCard progress={progress} />
        </div>
        <ActivityCard year={year} now={now} />
        {fresh ? (
          <StartCard />
        ) : (
          <HistoryPanel recent={recent} tasks={tasks.state.status === 'success' ? tasks.state.data.items : []} progress={progress} />
        )}
      </div>
    </div>
  );
}

function PersonCard({ year, now }: { year: ApiResource<AttemptSummary[]>; now: Date }) {
  const t = useT();
  const session = useSession();
  const [signingOut, setSigningOut] = useState(false);
  const [failed, setFailed] = useState(false);

  if (session.status !== 'signedIn') {
    return null;
  }

  const { username } = session.user;
  const streak = year.state.status === 'success' ? activity(year.state.data, now).streak : null;

  return (
    <section className="rc-profile__card rc-profile__person" aria-labelledby="profile-name" style={{ '--i': 0 } as CSSProperties}>
      <span className="rc-profile__avatar" aria-hidden="true">
        {(Array.from(username)[0] ?? '?').toLocaleUpperCase('ru')}
      </span>
      <h1 id="profile-name" className="rc-profile__name">
        {username}
      </h1>
      <div className="rc-profile__person-meta">
        {streak === null ? (
          year.state.status === 'loading' && <Skeleton className="rc-profile__streak-skeleton" />
        ) : (
          <span className="rc-streak" role="img" aria-label={t('header.streakLabel', { count: streak })}>
            <svg viewBox="0 0 24 24" aria-hidden="true" className="icon">
              <path d="M12 2c1 4 5 6 5 11a5 5 0 0 1-10 0c0-3 2-4 2-7 1.5 1 3 2.5 3-4z" />
            </svg>
            {t('header.streak', { count: streak })}
          </span>
        )}
      </div>
      <button
        type="button"
        className="button rc-profile__sign-out"
        disabled={signingOut}
        onClick={() => {
          setSigningOut(true);
          setFailed(false);
          session.signOut().then(
            () => {
              setSigningOut(false);
            },
            () => {
              setSigningOut(false);
              setFailed(true);
            },
          );
        }}
      >
        {signingOut ? t('session.signingOut') : t('session.signOut')}
      </button>
      {failed && (
        <p className="app__session-error" role="alert">
          {t('session.signOutFailed')}
        </p>
      )}
    </section>
  );
}

/** The six classes from first attempts: found of met as a bar in the class's colour, and treated right. */
function ClassesCard({ progress }: { progress: ApiResource<Progress> }) {
  const t = useT();
  const { state, reload } = progress;

  return (
    <ProfileCard id="profile-classes" title={t('profile.classes.title')} index={3} className="rc-profile__classes">
      {state.status === 'loading' && (
        <CardLoading label={t('profile.classes.loading')}>
          {Array.from({ length: 6 }, (_, index) => (
            <div key={index} className="rc-profile__class-skeleton">
              <Skeleton height={14} width="60%" />
              <Skeleton height={6} />
            </div>
          ))}
        </CardLoading>
      )}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <ClassList progress={state.data} />}
    </ProfileCard>
  );
}

function ClassList({ progress }: { progress: Progress }) {
  const t = useT();
  const nothingMet = progress.classes.every((item) => item.met === 0);

  return (
    <>
      {nothingMet && <p className="rc-profile__note">{t('profile.classes.none')}</p>}
      <ul className="rc-profile__classes-list">
        {progress.classes.map((item) => (
          <li key={item.class} className={item.met === 0 ? `rc-profile__class rc-profile__class--none k-${item.class}` : `rc-profile__class k-${item.class}`}>
            <span className="rc-profile__class-name">{item.name}</span>
            <span
              className="rc-bar rc-profile__bar"
              role="img"
              aria-label={t('profile.classes.bar', { found: item.found, met: item.met })}
              style={{ '--p': percent(item.found, item.met) } as CSSProperties}
            />
            {item.met === 0 ? (
              !nothingMet && <span className="rc-profile__small">{t('profile.classes.notMet')}</span>
            ) : (
              <span className="rc-profile__class-foot">
                <span className="rc-profile__mono">{t('profile.classes.counts', { found: item.found, met: item.met })}</span>
                <span className="rc-profile__small">{t('profile.classes.treatedRight', { count: item.treatedRight })}</span>
              </span>
            )}
          </li>
        ))}
      </ul>
    </>
  );
}

/** Tasks solved out of all, as a ring, with a row per difficulty beside it (from `GET /tasks`). */
function SolvedCard({ tasks }: { tasks: ApiResource<Page<TaskSummary>> }) {
  const t = useT();
  const { state, reload } = tasks;

  return (
    <ProfileCard id="profile-solved" title={t('profile.solved.title')} index={1} className="rc-profile__solved">
      {state.status === 'loading' && (
        <CardLoading label={t('profile.solved.loading')}>
          <div className="rc-profile__solved-body">
            <Skeleton className="rc-profile__ring-skeleton" />
            <div className="rc-profile__levels">
              <Skeleton height={18} />
              <Skeleton height={18} />
              <Skeleton height={18} />
            </div>
          </div>
        </CardLoading>
      )}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <SolvedBody tasks={state.data.items} />}
    </ProfileCard>
  );
}

function SolvedBody({ tasks }: { tasks: readonly TaskSummary[] }) {
  const t = useT();
  const solved = solvedTasks(tasks);
  const shown = useCountUp(solved.solved);

  return (
    <div className="rc-profile__solved-body">
      <div
        className="rc-ring rc-profile__ring"
        role="img"
        aria-label={t('profile.solved.ring', { solved: solved.solved, total: solved.total })}
        style={{ '--p': percent(solved.solved, solved.total) } as CSSProperties}
      >
        <div className="rc-ring__hole rc-profile__ring-hole" aria-hidden="true">
          <span className="rc-ring__value rc-profile__ring-value">{shown}</span>
          <span className="rc-ring__of">{t('profile.solved.of', { total: solved.total })}</span>
        </div>
      </div>
      <div className="rc-profile__levels">
        <ul className="rc-profile__level-list">
          {solved.byDifficulty.map((level) => (
            <li
              key={level.difficulty}
              className={`rc-profile__level rc-profile__level--${level.difficulty}`}
              aria-label={t('profile.solved.row', { level: t(`profile.solved.${level.difficulty}`), solved: level.solved, total: level.total })}
            >
              <span className="rc-profile__level-name" aria-hidden="true">
                {t(`profile.solved.${level.difficulty}`)}
              </span>
              <span className="rc-profile__level-count" aria-hidden="true">
                {level.solved}
                <span className="rc-profile__level-total">{t('profile.solved.outOf', { total: level.total })}</span>
              </span>
              <span
                className="rc-bar rc-profile__bar rc-profile__level-bar"
                aria-hidden="true"
                style={{ '--p': percent(level.solved, level.total) } as CSSProperties}
              />
            </li>
          ))}
        </ul>
        <p className="rc-profile__small rc-profile__aside-counts">
          <span>{t('profile.solved.tried', { count: solved.tried })}</span>
          <span>{t('profile.solved.gaveUp', { count: solved.gaveUp })}</span>
        </p>
      </div>
    </div>
  );
}

/** Three tiles from the progress: found of met, treated right of found, picked where it was not. */
function DiagnosisCard({ progress }: { progress: ApiResource<Progress> }) {
  const t = useT();
  const { state, reload } = progress;

  return (
    <ProfileCard id="profile-diagnosis" title={t('profile.diagnosis.title')} index={2} className="rc-profile__diagnosis">
      {state.status === 'loading' && (
        <CardLoading label={t('profile.diagnosis.loading')}>
          <div className="rc-profile__tiles">
            <Skeleton className="rc-profile__tile-skeleton" />
            <Skeleton className="rc-profile__tile-skeleton" />
            <Skeleton className="rc-profile__tile-skeleton" />
          </div>
        </CardLoading>
      )}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <DiagnosisBody progress={state.data} />}
    </ProfileCard>
  );
}

function DiagnosisBody({ progress }: { progress: Progress }) {
  const t = useT();
  const counts = diagnosis(progress);

  return (
    <>
      <div className="rc-profile__tiles">
        <Tile value={counts.found} of={counts.met} label={t('profile.diagnosis.found')} />
        <Tile value={counts.treatedRight} of={counts.found} label={t('profile.diagnosis.treatedRight')} />
        <Tile value={counts.pickedWhenAbsent} label={t('profile.diagnosis.pickedWhenAbsent')} tone={counts.pickedWhenAbsent > 0 ? 'danger' : undefined} />
      </div>
      <p className="rc-profile__note">{t('profile.diagnosis.note')}</p>
    </>
  );
}

function Tile({ value, of, label, tone }: { value: number; of?: number; label: string; tone?: 'danger' | undefined }) {
  const t = useT();
  const shown = useCountUp(value);

  return (
    <div className={tone === 'danger' ? 'rc-stat rc-profile__tile rc-profile__tile--danger' : 'rc-stat rc-profile__tile'}>
      <div className="rc-stat__value">
        {shown}
        {of !== undefined && <span className="rc-profile__tile-of"> {t('profile.diagnosis.of', { total: of })}</span>}
      </div>
      <div className="rc-stat__label">{label}</div>
    </div>
  );
}

/** In place of the history for a learner who has not begun: the way to the tasks. */
function StartCard() {
  const t = useT();

  return (
    <ProfileCard id="profile-start" title={t('profile.start.title')} index={5} className="rc-profile__start">
      <p className="rc-profile__note">{t('profile.start.text')}</p>
      <p className="rc-profile__start-action">
        <Link className="button button--primary" to="/tasks">
          {t('profile.start.action')}
        </Link>
      </p>
    </ProfileCard>
  );
}
