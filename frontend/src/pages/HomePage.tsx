import { useCallback, useEffect, useRef, useState } from 'react';
import type { CSSProperties } from 'react';
import { Link } from 'react-router';
import { getProblemCatalogue, getProgress, listPracticeSince, listTasks, useApiClient } from '../api';
import type { Practice, Progress, TaskSummary } from '../api';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { ActivitySkeleton, DailyTaskSkeleton, StatSkeleton, ProblemsToReadSkeleton, ProgressSkeleton, TaskTableSkeleton } from '../components/skeletons';
import { TaskTable } from '../components/TaskTable';
import { useApiResource } from '../hooks/useApiResource';
import { useCountUp } from '../hooks/useCountUp';
import { useHydrated } from '../hooks/useHydrated';
import { useLocale, useT } from '../i18n';
import { useSession } from '../session';
import { SignInLinks } from '../session/SignInLinks';
import { usePrerendered, useSiteConfig } from '../site';
import type { ArenaCard, ArenaCatalogue } from '../site';
import {
  activity,
  activityLevel,
  activitySince,
  dailyTask,
  ACTIVITY_DAYS,
  formatCountdown,
  moscowDate,
  moscowDay,
  nextMoscowMidnight,
  suggestedProblems,
  suggestedTasks,
} from './home/arena';
import './home/home.css';

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
  // Prerendered, the page hydrates with the classes and the cards it was built with and loads them
  // no more (#310). The problems to read change with the day, so its first render is for the day it
  // was built, and the arena moves on to today once it is mounted.
  const prerendered = usePrerendered('/')?.arena;
  const { state } = useApiResource<ArenaCatalogue>((signal) => getProblemCatalogue(client, signal), [client], prerendered && { data: prerendered.catalogue });

  return <Arena catalogue={state.status === 'success' ? state.data : null} builtAt={prerendered?.builtAt} />;
}

function Arena({ catalogue, builtAt }: { catalogue: ArenaCatalogue | null; builtAt: string | undefined }) {
  const client = useApiClient();
  const t = useT();
  const session = useSession();
  const { demoTask } = useSiteConfig();
  const tasks = useApiResource((signal) => listTasks(client, { pageSize: 100 }, signal), [client]);
  // One instant for the whole page, so the daily task and the activity agree on what today is. It
  // moves on when the daily task's countdown reaches midnight, and the next day's task takes over.
  // A hydrated page renders first for the instant it was built for, as its markup is (#310).
  const [clock, setNow] = useState(() => new Date());
  const hydrated = useHydrated();
  const now = hydrated || builtAt === undefined ? clock : new Date(builtAt);
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
            {taskList !== null ? (
              <Stat value={taskList.length} label={t('home.statTasks', { count: taskList.length })} />
            ) : (
              catalogue === null && <StatSkeleton />
            )}
            {catalogue !== null ? (
              <>
                <Stat value={catalogue.cards.length} label={t('home.statProblems', { count: catalogue.cards.length })} />
                <Stat value={catalogue.classes.length} label={t('home.statClasses', { count: catalogue.classes.length })} />
              </>
            ) : (
              <>
                <StatSkeleton />
                <StatSkeleton />
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
            {tasks.state.status === 'loading' && <LoadingState label={t('home.table.loading')} skeleton={<TaskTableSkeleton more rows={6} status={session.status !== 'signedOut'} />} />}
            {tasks.state.status === 'error' && <ErrorState error={tasks.state.error} onRetry={tasks.reload} />}
            {taskList !== null && <SuggestedTasks tasks={suggestedTasks(taskList, now, today?.slug ?? null)} />}
          </section>

          <section className="rc-panel" aria-labelledby="home-reading">
            <div className="rc-panel__bar">
              <h2 id="home-reading" className="rc-arena__panel-title">
                {t('home.reading.title')}
              </h2>
            </div>
            {catalogue === null ? <LoadingState label={t('home.reading.loading')} skeleton={<ProblemsToReadSkeleton />} /> : <ProblemsToRead catalogue={catalogue} now={now} />}
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
  // A prerendered count is shown as it is; one that arrives with the data counts up (useCountUp).
  const shown = useCountUp(value);

  return (
    <div className="rc-stat">
      <div className="rc-stat__value">{shown}</div>
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
      <div className="rc-daily">
        <div className="rc-daily__top">
          <span>{t('home.daily.label')}</span>
        </div>
        <LoadingState className="rc-daily__loading" label={t('home.daily.loading')} skeleton={<DailyTaskSkeleton />} />
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
      <p className="rc-daily__about">{t('home.daily.about')}</p>
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
function ProblemsToRead({ catalogue, now }: { catalogue: ArenaCatalogue; now: Date }) {
  const t = useT();
  const className = (card: ArenaCard) => catalogue.classes.find((each) => each.id === card.class)?.name ?? card.class;

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
      {state.status === 'loading' && <LoadingState label={t('home.progress.loading')} skeleton={<ProgressSkeleton />} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <ProgressBody progress={state.data} />}
    </section>
  );
}

function ProgressBody({ progress }: { progress: Progress }) {
  const t = useT();
  const met = progress.classes.reduce((sum, item) => sum + item.met, 0);
  const found = progress.classes.reduce((sum, item) => sum + item.found, 0);
  const shown = useCountUp(found);

  if (met === 0) {
    return <p className="rc-arena__note">{t('home.progress.none')}</p>;
  }

  return (
    <>
      <div className="rc-arena__ring-row">
        <div className="rc-ring rc-arena__ring" style={{ '--p': percent(found, met) } as CSSProperties}>
          <div className="rc-ring__hole">
            <span className="rc-ring__value">{shown}</span>
            <span className="rc-ring__of">{t('home.progress.ring', { met })}</span>
          </div>
        </div>
        <p className="rc-arena__note">{t('home.progress.note')}</p>
      </div>
      <ul className="rc-arena__bars">
        {progress.classes.map((item, index) => (
          <li key={item.class} className={`k-${item.class}`} style={{ '--i': index } as CSSProperties}>
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
      <div className="rc-arena__card-foot">
        <Link to="/profile">{t('home.progress.profile')}</Link>
      </div>
    </>
  );
}

/** The streak and twelve weeks of activity, from every submitted attempt and study mark (SPEC §4.7, ADR 0018). */
function ActivityCard({ now }: { now: Date }) {
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource((signal) => listPracticeSince(client, activitySince(now), signal), [client, now]);

  return (
    <section className="rc-arena__card" aria-labelledby="home-activity">
      <div className="rc-arena__card-head">
        <h2 id="home-activity" className="rc-arena__panel-title">
          <Link className="rc-arena__title-link" to="/profile">
            {t('home.activity.title')}
          </Link>
        </h2>
        <span className="rc-arena__note">{t('home.activity.period')}</span>
      </div>
      {state.status === 'loading' && <LoadingState label={t('home.activity.loading')} skeleton={<ActivitySkeleton />} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <ActivityBody practice={state.data} now={now} />}
    </section>
  );
}

function ActivityBody({ practice, now }: { practice: Practice; now: Date }) {
  const t = useT();
  const locale = useLocale();
  const { days, marks, streak } = activity(practice.attempts, now, practice.marks);
  const firstDay = moscowDay(now) - ACTIVITY_DAYS + 1;
  const dayFormat = new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', timeZone: 'UTC' });
  const studied = marks.reduce((sum, count) => sum + count, 0);
  const total = days.reduce((sum, count) => sum + count, 0) - studied;

  return (
    <>
      <p className="rc-arena__streak">
        <span className="rc-streak">{streak > 0 ? t('home.activity.streak', { count: streak }) : t('home.activity.noStreak')}</span>
      </p>
      {/* One image named in words (the streak sentence does not say the days); each day says its date and count on hover. */}
      <div
        className="rc-heat"
        role="img"
        aria-label={studied > 0 ? t('home.activity.mapStudied', { count: total, studied }) : t('home.activity.map', { count: total })}
      >
        {days.map((count, index) => {
          const date = dayFormat.format(moscowDate(firstDay + index));
          const marked = marks[index] ?? 0;
          const tried = count - marked;

          return (
            // The days are positional: the same index is the same day for this render.
            <span
              key={index}
              className="rc-heat__cell"
              data-level={activityLevel(count)}
              data-today={index === ACTIVITY_DAYS - 1 ? '' : undefined}
              title={
                count === 0
                  ? t('home.activity.cellNone', { date })
                  : marked === 0
                    ? t('home.activity.cell', { date, count })
                    : tried === 0
                      ? t('home.activity.cellStudied', { date, count: marked })
                      : t('home.activity.cellBoth', { date, count: tried, studied: marked })
              }
            />
          );
        })}
      </div>
      <HeatLegend />
    </>
  );
}

/** «меньше ▢▢▢▢▢ больше»: the five shades of the map, so a cell's colour can be read. */
function HeatLegend() {
  const t = useT();

  return (
    <div className="rc-arena__legend" aria-hidden="true">
      <span>{t('home.activity.less')}</span>
      {[0, 1, 2, 3, 4].map((level) => (
        <span key={level} className="rc-heat__cell" data-level={level} />
      ))}
      <span>{t('home.activity.more')}</span>
    </div>
  );
}

/** A fixed pattern for the guest's preview: twelve weeks of seven days, the same on every render. */
const PREVIEW_LEVELS = Array.from({ length: 84 }, (_, index) => {
  const week = Math.floor(index / 7);
  const day = index % 7;
  return [0, 1, 0, 2, 3, 1, 4, 0, 2, 1][(week * 3 + day * 4 + week * day) % 10] as 0 | 1 | 2 | 3 | 4;
});

/**
 * What signing in gives a guest: a drawn, muted preview of the ring and the map — decorative, no
 * numbers — then the sign-in links.
 */
function GuestCard() {
  const t = useT();

  return (
    <section className="rc-arena__card" aria-labelledby="home-guest">
      <h2 id="home-guest" className="rc-arena__panel-title">
        {t('home.guest.title')}
      </h2>
      <p className="rc-arena__note">{t('home.guest.text')}</p>
      <figure className="rc-arena__preview" aria-hidden="true">
        <div className="rc-arena__preview-art">
          <div className="rc-ring" style={{ '--p': 60 } as CSSProperties}>
            <div className="rc-ring__hole" />
          </div>
          <div className="rc-heat">
            {PREVIEW_LEVELS.map((level, index) => (
              <span key={index} className="rc-heat__cell" data-level={level} />
            ))}
          </div>
        </div>
        <figcaption className="rc-arena__note">{t('home.guest.preview')}</figcaption>
      </figure>
      <SignInLinks returnPath="/" />
    </section>
  );
}

function percent(part: number, whole: number): number {
  return whole === 0 ? 0 : Math.round((part / whole) * 100);
}
