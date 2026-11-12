import { useCallback, useEffect, useRef, useState } from 'react';
import type { CSSProperties } from 'react';
import { Link } from 'react-router';
import { getProblemCatalogue, getProgress, listAttemptsSince, listTasks, useApiClient } from '../api';
import type { AttemptSummary, ProblemCard, ProblemCatalogue, Progress, TaskSummary } from '../api';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { TaskTable } from '../components/TaskTable';
import { useApiResource } from '../hooks/useApiResource';
import { useT } from '../i18n';
import { useSession } from '../session';
import { SignInLinks } from '../session/SignInLinks';
import { useSiteConfig } from '../site';
import {
  activity,
  activityLevel,
  activitySince,
  dailyTask,
  formatCountdown,
  nextMoscowMidnight,
  suggestedProblems,
  suggestedTasks,
} from './home/arena';

/**
 * `/` — the arena (docs/SPEC.md §4.1, ADR 0015, DESIGN.md › The arena): what Ritocode teaches with
 * the real counts, the daily task, a few tasks and problems to start from — the ways into the two
 * catalogues — and the learner's progress, streak and activity, or, for a guest, a way to sign in.
 *
 * The prerender renders {@link StaticHomePage} with the catalogue from the content export: effects do
 * not run there, so the lead, the counts and the problems to read are in the static page, and the
 * tasks and everything personal are filled in by the application.
 */
export function HomePage() {
  const client = useApiClient();
  const { state } = useApiResource((signal) => getProblemCatalogue(client, signal), [client]);

  return <Arena catalogue={state.status === 'success' ? state.data : null} />;
}

/** The arena for the prerender: the catalogue comes from the content export, not the API. */
export function StaticHomePage({ catalogue }: { catalogue: ProblemCatalogue }) {
  return <Arena catalogue={catalogue} />;
}

function Arena({ catalogue }: { catalogue: ProblemCatalogue | null }) {
  const client = useApiClient();
  const t = useT();
  const session = useSession();
  const { demoTask } = useSiteConfig();
  const tasks = useApiResource((signal) => listTasks(client, { pageSize: 100 }, signal), [client]);
  // One instant for the whole page, so the daily task and the activity agree on what today is. It
  // moves on when the daily task's countdown reaches midnight, and the next day's task takes over.
  const [now, setNow] = useState(() => new Date());
  const nextDay = useCallback(() => {
    setNow(new Date());
  }, []);
  const taskList = tasks.state.status === 'success' ? tasks.state.data.items : null;
  const today = taskList === null ? null : dailyTask(taskList, now);

  return (
    <section className="page rc-arena">
      <div className="rc-arena__top">
        <header className="rc-arena__hero">
          <h1>{t('home.title')}</h1>
          <p className="rc-arena__lead">{t('home.lead')}</p>

          <div className="rc-arena__stats" role="group" aria-label={t('home.stats')}>
            {taskList !== null && <Stat value={taskList.length} label={t('home.statTasks', { count: taskList.length })} />}
            {catalogue !== null && (
              <>
                <Stat value={catalogue.cards.length} label={t('home.statProblems', { count: catalogue.cards.length })} />
                <Stat value={catalogue.classes.length} label={t('home.statClasses', { count: catalogue.classes.length })} />
              </>
            )}
          </div>

          <nav className="rc-arena__actions" aria-label={t('home.actions')}>
            <Link className="button button--primary" to={`/tasks/${demoTask}`}>
              {t('home.demo')}
            </Link>
            <Link className="button" to="/tasks">
              {t('home.tasks')}
            </Link>
            <Link className="button" to="/problems">
              {t('home.problems')}
            </Link>
          </nav>
        </header>

        <DailyTask
          task={today}
          loading={taskList === null}
          now={now}
          onNextDay={nextDay}
        />
      </div>

      <div className="rc-arena__main">
        <div className="rc-arena__lists">
          <section className="rc-panel" aria-labelledby="home-tasks">
            <div className="rc-panel__bar">
              <h2 id="home-tasks" className="rc-arena__panel-title">
                {t('home.table.title')}
              </h2>
            </div>
            {tasks.state.status === 'loading' && <LoadingState label={t('home.table.loading')} />}
            {tasks.state.status === 'error' && <ErrorState error={tasks.state.error} onRetry={tasks.reload} />}
            {taskList !== null && <SuggestedTasks tasks={suggestedTasks(taskList, now, today?.slug ?? null)} />}
          </section>

          <section className="rc-panel" aria-labelledby="home-reading">
            <div className="rc-panel__bar">
              <h2 id="home-reading" className="rc-arena__panel-title">
                {t('home.reading.title')}
              </h2>
            </div>
            {catalogue === null ? <LoadingState label={t('home.reading.loading')} /> : <ProblemsToRead catalogue={catalogue} now={now} />}
          </section>
        </div>

        <aside className="rc-arena__aside">
          {session.status === 'signedIn' && (
            <>
              <ProgressCard />
              <ActivityCard now={now} />
            </>
          )}
          {session.status === 'signedOut' && <GuestCard />}
        </aside>
      </div>
    </section>
  );
}

function Stat({ value, label }: { value: number; label: string }) {
  return (
    <div className="rc-stat">
      <div className="rc-stat__value">{value}</div>
      <div className="rc-stat__label">{label}</div>
    </div>
  );
}

/**
 * The page's one spotlight: today's task, or a placeholder while the list loads. The countdown comes
 * with the task, so the prerendered page — which has no tasks — never shows a time baked at build.
 */
function DailyTask({
  task,
  loading,
  now,
  onNextDay,
}: {
  task: TaskSummary | null;
  loading: boolean;
  now: Date;
  onNextDay: () => void;
}) {
  const t = useT();

  if (task === null) {
    return loading ? (
      <div className="rc-daily" aria-busy="true">
        <div className="rc-daily__top">
          <span>{t('home.daily.label')}</span>
        </div>
        <p className="rc-daily__title">{t('home.daily.loading')}</p>
      </div>
    ) : null;
  }

  return (
    <article className="rc-daily" aria-labelledby="home-daily">
      <div className="rc-daily__top">
        <span>{t('home.daily.label')}</span>
        <Countdown key={nextMoscowMidnight(now).getTime()} until={nextMoscowMidnight(now)} onZero={onNextDay} />
      </div>
      <h2 id="home-daily" className="rc-daily__title">
        {task.title}
      </h2>
      <div className="rc-daily__meta">
        <span className={`rc-difficulty rc-difficulty--${task.difficulty}`}>{t(`tasks.difficulty.${task.difficulty}`)}</span>
        <span className="rc-daily__chip">{t(`tasks.time.${task.difficulty}`)}</span>
        {task.status !== null && task.status !== 'new' && <span className="rc-daily__chip">{t(`home.daily.statuses.${task.status}`)}</span>}
      </div>
      <Link className="button rc-daily__action" to={`/tasks/${task.slug}`}>
        {t('home.daily.start')}
      </Link>
    </article>
  );
}

/**
 * The time left until `until`, ticking every second; at zero it calls `onZero` once. Not a live
 * region: a screen reader reads the time when it reaches it, not on every tick.
 */
function Countdown({ until, onZero }: { until: Date; onZero: () => void }) {
  const t = useT();
  const [left, setLeft] = useState(() => until.getTime() - Date.now());
  const zero = useRef(onZero);

  useEffect(() => {
    zero.current = onZero;
  }, [onZero]);

  useEffect(() => {
    const timer = window.setInterval(() => {
      const ms = until.getTime() - Date.now();
      setLeft(ms);
      if (ms <= 0) {
        window.clearInterval(timer);
        zero.current();
      }
    }, 1000);

    return () => {
      window.clearInterval(timer);
    };
  }, [until]);

  const time = formatCountdown(left);

  return (
    <span className="rc-daily__countdown">
      {t('home.daily.countdown')} <time dateTime={`PT${time.replace(/^(\d+):(\d+):(\d+)$/, '$1H$2M$3S')}`}>{time}</time>
    </span>
  );
}

/** The suggested tasks (`suggestedTasks`) in the arena's table; every task is on `/tasks`. */
function SuggestedTasks({ tasks }: { tasks: readonly TaskSummary[] }) {
  const t = useT();

  if (tasks.length === 0) {
    return <p className="rc-arena__empty">{t('home.table.empty')}</p>;
  }

  return (
    <>
      <TaskTable tasks={tasks} />
      <div className="rc-arena__more">
        <Link to="/tasks">{t('home.table.more')}</Link>
      </div>
    </>
  );
}

/** A few cards of the catalogue — name, class and summary — each opening at its anchor in `/problems`. */
function ProblemsToRead({ catalogue, now }: { catalogue: ProblemCatalogue; now: Date }) {
  const t = useT();
  const className = (card: ProblemCard) => catalogue.classes.find((each) => each.id === card.class)?.name ?? card.class;

  return (
    <>
      <ul className="rc-arena__problems">
        {suggestedProblems(catalogue.cards, now).map((card) => (
          <li key={card.slug}>
            <Link to={`/problems#${card.slug}`} className={`rc-arena__problem k-${card.class}`}>
              <span className="rc-class">{className(card)}</span>
              <span className="rc-arena__problem-name">{card.name}</span>
              <span className="rc-arena__problem-summary">{card.summary}</span>
            </Link>
          </li>
        ))}
      </ul>
      <div className="rc-arena__more">
        <Link to="/problems">{t('home.reading.more')}</Link>
      </div>
    </>
  );
}

/** Progress by class from first attempts (SPEC §4.7): found out of met, as a ring and six bars. */
function ProgressCard() {
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource((signal) => getProgress(client, signal), [client]);

  return (
    <section className="rc-arena__card" aria-labelledby="home-progress">
      <h2 id="home-progress" className="rc-arena__panel-title">
        {t('home.progress.title')}
      </h2>
      {state.status === 'loading' && <LoadingState label={t('home.progress.loading')} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <ProgressBody progress={state.data} />}
    </section>
  );
}

function ProgressBody({ progress }: { progress: Progress }) {
  const t = useT();
  const met = progress.classes.reduce((sum, item) => sum + item.met, 0);
  const found = progress.classes.reduce((sum, item) => sum + item.found, 0);

  if (met === 0) {
    return <p className="rc-arena__note">{t('home.progress.none')}</p>;
  }

  return (
    <>
      <div className="rc-arena__ring-row">
        <div className="rc-ring" style={{ '--p': percent(found, met) } as CSSProperties}>
          <div className="rc-ring__hole">
            <span className="rc-ring__value">{found}</span>
            <span className="rc-ring__of">{t('home.progress.ring', { met })}</span>
          </div>
        </div>
        <p className="rc-arena__note">{t('home.progress.note')}</p>
      </div>
      <ul className="rc-arena__bars">
        {progress.classes.map((item) => (
          <li key={item.class} className={`k-${item.class}`}>
            <span className="rc-arena__bar-label">
              <span>{item.name}</span>
              <span className="rc-arena__bar-count">{t('home.progress.count', { found: item.found, met: item.met })}</span>
            </span>
            <span
              className="rc-bar"
              role="img"
              aria-label={t('home.progress.found', { found: item.found, met: item.met })}
              style={{ '--p': percent(item.found, item.met) } as CSSProperties}
            />
          </li>
        ))}
      </ul>
    </>
  );
}

/** The streak and twelve weeks of activity, from every submitted attempt (SPEC §4.7). */
function ActivityCard({ now }: { now: Date }) {
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource((signal) => listAttemptsSince(client, activitySince(now), signal), [client, now]);

  return (
    <section className="rc-arena__card" aria-labelledby="home-activity">
      <div className="rc-arena__card-head">
        <h2 id="home-activity" className="rc-arena__panel-title">
          {t('home.activity.title')}
        </h2>
        <span className="rc-arena__note">{t('home.activity.period')}</span>
      </div>
      {state.status === 'loading' && <LoadingState label={t('home.activity.loading')} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <ActivityBody attempts={state.data} now={now} />}
    </section>
  );
}

function ActivityBody({ attempts, now }: { attempts: readonly AttemptSummary[]; now: Date }) {
  const t = useT();
  const { days, streak } = activity(attempts, now);

  return (
    <>
      <p className="rc-arena__streak">
        <span className="rc-streak">{streak > 0 ? t('home.activity.streak', { count: streak }) : t('home.activity.noStreak')}</span>
      </p>
      <div className="rc-heat" aria-hidden="true">
        {days.map((count, index) => (
          // The days are positional: the same index is the same day for this render.
          <span key={index} className="rc-heat__cell" data-level={activityLevel(count)} />
        ))}
      </div>
    </>
  );
}

function GuestCard() {
  const t = useT();

  return (
    <section className="rc-arena__card" aria-labelledby="home-guest">
      <h2 id="home-guest" className="rc-arena__panel-title">
        {t('home.guest.title')}
      </h2>
      <p className="rc-arena__note">{t('home.guest.text')}</p>
      <SignInLinks returnPath="/" />
    </section>
  );
}

function percent(part: number, whole: number): number {
  return whole === 0 ? 0 : Math.round((part / whole) * 100);
}
