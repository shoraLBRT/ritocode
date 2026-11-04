import { useCallback, useEffect, useRef, useState } from 'react';
import type { CSSProperties } from 'react';
import { Link } from 'react-router';
import { difficulties, getProblemCatalogue, getProgress, listAttemptsSince, listTasks, useApiClient } from '../api';
import type { AttemptSummary, Difficulty, ProblemCatalogue, Progress, TaskSummary } from '../api';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { Tabs, TaskTable } from '../components/TaskTable';
import { useApiResource } from '../hooks/useApiResource';
import { useT } from '../i18n';
import { useSession } from '../session';
import { SignInLinks } from '../session/SignInLinks';
import { useSiteConfig } from '../site';
import { activity, activityLevel, activitySince, dailyTask, formatCountdown, nextMoscowMidnight } from './home/arena';

/**
 * `/` — the arena (docs/SPEC.md §4.1, ADR 0015, DESIGN.md › The arena): what Ritocode teaches with
 * the real counts, the daily task, the task list, and the learner's progress, streak and activity —
 * or, for a guest, a way to sign in.
 *
 * The prerender renders {@link StaticHomePage} with the catalogue from the content export: effects do
 * not run there, so the lead, the counts and how a task goes are in the static page, and the tasks
 * and everything personal are filled in by the application.
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

/** How many tasks the home page lists; the rest are a link away. */
const LISTED_TASKS = 8;

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
          task={taskList === null ? null : dailyTask(taskList, now)}
          loading={taskList === null}
          now={now}
          onNextDay={nextDay}
        />
      </div>

      <div className="rc-arena__main">
        <div className="rc-panel rc-arena__tasks">
          {tasks.state.status === 'loading' && <LoadingState label={t('home.table.loading')} />}
          {tasks.state.status === 'error' && <ErrorState error={tasks.state.error} onRetry={tasks.reload} />}
          {taskList !== null && <HomeTasks tasks={taskList} />}
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

      <section className="rc-arena__how" aria-labelledby="home-how">
        <h2 id="home-how">{t('home.howTitle')}</h2>
        <ol>
          <li>{t('home.step1')}</li>
          <li>{t('home.step2')}</li>
          <li>{t('home.step3')}</li>
        </ol>
      </section>
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
        {task.solved === true && <span className="rc-daily__chip">{t('home.daily.solved')}</span>}
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

/**
 * The first tasks of the catalogue, in the API's order, with difficulty tabs; the rest are on
 * `/tasks`, which the link opens on the same tab.
 */
function HomeTasks({ tasks }: { tasks: readonly TaskSummary[] }) {
  const t = useT();
  const [difficulty, setDifficulty] = useState<Difficulty | 'all'>('all');

  if (tasks.length === 0) {
    return <p className="rc-arena__empty">{t('home.table.empty')}</p>;
  }

  const shown = tasks.filter((task) => difficulty === 'all' || task.difficulty === difficulty).slice(0, LISTED_TASKS);
  const count = (value: Difficulty | 'all') => tasks.filter((task) => value === 'all' || task.difficulty === value).length;

  return (
    <>
      <div className="rc-panel__bar">
        <h2 className="rc-arena__panel-title">{t('home.table.title')}</h2>
        <Tabs
          label={t('tasks.difficultyFilter')}
          options={(['all', ...difficulties] as const).map((value) => ({
            value,
            label: value === 'all' ? t('tasks.all') : t(`tasks.difficulty.${value}`),
            count: count(value),
          }))}
          value={difficulty}
          onChange={setDifficulty}
        />
      </div>

      <TaskTable tasks={shown} />

      <div className="rc-arena__more">
        <Link to={difficulty === 'all' ? '/tasks' : `/tasks?difficulty=${difficulty}`}>{t('home.table.more')}</Link>
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
