import { useState } from 'react';
import type { CSSProperties } from 'react';
import { Link, useSearchParams } from 'react-router';
import type { AttemptSummary, CardProgress, Progress, TaskSummary } from '../../api';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import type { ApiResource } from '../../hooks/useApiResource';
import { useLocale, useT } from '../../i18n';
import { CardLoading } from './parts';

type Tab = 'attempts' | 'cards';

/**
 * The profile's last panel: two tabs, the choice kept in the address (`?tab=cards`; the attempts by
 * default). *Недавние попытки* — the newest attempts, each opening its review, an open one the task.
 * *По карточкам* — the cards met or picked in first attempts, filtered by class (docs/SPEC.md §4.7);
 * the tasks whose answer is not revealed name no card here, and a note counts them (ADR 0017).
 */
export function HistoryPanel({
  recent,
  tasks,
  progress,
}: {
  recent: ApiResource<AttemptSummary[]>;
  tasks: readonly TaskSummary[];
  progress: ApiResource<Progress>;
}) {
  const t = useT();
  const [params, setParams] = useSearchParams();
  const tab: Tab = params.get('tab') === 'cards' ? 'cards' : 'attempts';
  const tabs: readonly Tab[] = ['attempts', 'cards'];

  return (
    <section className="rc-panel rc-profile__history" aria-label={t('profile.tabs.label')} style={{ '--i': 5 } as CSSProperties}>
      <div className="rc-panel__bar">
        <div className="rc-tabs" role="group" aria-label={t('profile.tabs.label')}>
          {tabs.map((option) => (
            <button
              key={option}
              type="button"
              className="rc-tab"
              aria-pressed={tab === option}
              onClick={() => {
                setParams(option === 'cards' ? { tab: 'cards' } : {}, { replace: true });
              }}
            >
              {t(`profile.tabs.${option}`)}
            </button>
          ))}
        </div>
      </div>
      {tab === 'attempts' ? <RecentAttempts recent={recent} tasks={tasks} /> : <CardsTab progress={progress} />}
    </section>
  );
}

function RecentAttempts({ recent, tasks }: { recent: ApiResource<AttemptSummary[]>; tasks: readonly TaskSummary[] }) {
  const t = useT();
  const { state, reload } = recent;

  if (state.status === 'loading') {
    return (
      <div className="rc-profile__panel-body">
        <CardLoading label={t('profile.attempts.loading')}>
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} height={36} />
          ))}
        </CardLoading>
      </div>
    );
  }

  if (state.status === 'error') {
    return (
      <div className="rc-profile__panel-body">
        <ErrorState error={state.error} onRetry={reload} />
      </div>
    );
  }

  if (state.data.length === 0) {
    return <EmptyState title={t('profile.attempts.none')} icon="flag" />;
  }

  const titles = new Map(tasks.map((task) => [task.slug, task.title]));

  return (
    <ul className="rc-profile__attempts">
      {state.data.map((attempt) => (
        <li key={attempt.id}>
          <AttemptRow attempt={attempt} title={titles.get(attempt.task) ?? attempt.task} />
        </li>
      ))}
    </ul>
  );
}

function AttemptRow({ attempt, title }: { attempt: AttemptSummary; title: string }) {
  const t = useT();
  const locale = useLocale();
  const submittedAt = attempt.submittedAt ?? attempt.startedAt;
  const date = new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short' }).format(new Date(submittedAt));
  const chip = attempt.practice ? 'practice' : 'first';

  return (
    <Link className="rc-profile__attempt" to={`/tasks/${attempt.task}/attempts/${attempt.id}`}>
      <time className="rc-profile__attempt-date" dateTime={submittedAt}>
        {date}
      </time>
      <span className="rc-profile__attempt-title">{title}</span>
      <span className={`rc-profile__chip rc-profile__chip--${chip}`}>{t(`profile.attempts.${chip}`)}</span>
      <span
        className="rc-profile__attempt-score"
        aria-label={attempt.score !== null && attempt.maxScore !== null ? t('profile.attempts.score', { score: attempt.score, max: attempt.maxScore }) : undefined}
      >
        {attempt.score !== null && attempt.maxScore !== null && t('profile.attempts.scoreShort', { score: attempt.score, max: attempt.maxScore })}
      </span>
    </Link>
  );
}

function CardsTab({ progress }: { progress: ApiResource<Progress> }) {
  const t = useT();
  const { state, reload } = progress;

  if (state.status === 'loading') {
    return (
      <div className="rc-profile__panel-body">
        <CardLoading label={t('profile.cards.loading')}>
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} height={28} />
          ))}
        </CardLoading>
      </div>
    );
  }

  if (state.status === 'error') {
    return (
      <div className="rc-profile__panel-body">
        <ErrorState error={state.error} onRetry={reload} />
      </div>
    );
  }

  return <CardsBody progress={state.data} />;
}

/**
 * The cards, one `tbody` per class in the taxonomy's order, the class named in a header row; a card
 * Content no longer knows comes last, under a group of its own, and links nowhere. The filter offers
 * only the classes that have cards; pressed again, a class gives way to all.
 */
function CardsBody({ progress }: { progress: Progress }) {
  const t = useT();
  const [only, setOnly] = useState<string | null>(null);

  const groups = [
    ...progress.classes.map((item) => ({ key: item.class, name: item.name, cards: progress.cards.filter((card) => card.class === item.class) })),
    { key: '', name: t('profile.cards.noClass'), cards: progress.cards.filter((card) => card.class === null) },
  ].filter((group) => group.cards.length > 0);
  const classes = groups.filter((group) => group.key !== '');
  const shown = only === null ? groups : groups.filter((group) => group.key === only);

  return (
    <div className="rc-profile__panel-body">
      {progress.notRevealed > 0 && <p className="rc-profile__note">{t('profile.cards.notRevealed', { count: progress.notRevealed })}</p>}
      {progress.cards.length === 0 ? (
        progress.notRevealed === 0 && <p className="rc-profile__note">{t('profile.cards.none')}</p>
      ) : (
        <>
          {classes.length > 1 && (
            <div className="rc-catalogue__filter" role="group" aria-label={t('profile.cards.filter')}>
              <button
                type="button"
                className="rc-catalogue__chip"
                aria-pressed={only === null}
                onClick={() => {
                  setOnly(null);
                }}
              >
                {t('profile.cards.all')}
              </button>
              {classes.map((group) => (
                <button
                  key={group.key}
                  type="button"
                  className={`rc-catalogue__chip k-${group.key}`}
                  aria-pressed={only === group.key}
                  onClick={() => {
                    setOnly(only === group.key ? null : group.key);
                  }}
                >
                  <span className="rc-catalogue__dot" aria-hidden="true" />
                  {group.name}
                  <span className="rc-catalogue__chip-count">{group.cards.length}</span>
                </button>
              ))}
            </div>
          )}
          <p className="rc-profile__note">{t('profile.cards.hint')}</p>
          <div className="table-scroll" role="region" aria-label={t('profile.tabs.cards')} tabIndex={0}>
            <table className="rc-profile__table">
              <thead>
                <tr>
                  <th scope="col">{t('profile.cards.card')}</th>
                  <th scope="col">{t('profile.cards.met')}</th>
                  <th scope="col">{t('profile.cards.found')}</th>
                  <th scope="col">{t('profile.cards.missed')}</th>
                  <th scope="col">{t('profile.cards.pickedWhenAbsent')}</th>
                  <th scope="col">{t('profile.cards.treatedRight')}</th>
                </tr>
              </thead>
              {shown.map((group) => (
                <tbody key={group.key}>
                  <tr className="rc-profile__table-group">
                    <th scope="colgroup" colSpan={6}>
                      {group.name}
                    </th>
                  </tr>
                  {group.cards.map((card) => (
                    <CardRow key={card.card} card={card} />
                  ))}
                </tbody>
              ))}
            </table>
          </div>
        </>
      )}
    </div>
  );
}

function CardRow({ card }: { card: CardProgress }) {
  return (
    <tr>
      <th scope="row">{card.class === null ? card.name : <Link to={`/problems#${card.card}`}>{card.name}</Link>}</th>
      <Count value={card.met} />
      <Count value={card.found} />
      <Count value={card.missed} tone="warn" />
      <Count value={card.pickedWhenAbsent} tone="danger" />
      <Count value={card.treatedRight} tone="good" />
    </tr>
  );
}

/** A count in the table: a zero is quiet; a non-zero takes its column's tone, if it has one. */
function Count({ value, tone }: { value: number; tone?: 'warn' | 'danger' | 'good' }) {
  const className = value === 0 ? 'rc-profile__count rc-profile__count--zero' : tone ? `rc-profile__count rc-profile__count--${tone}` : 'rc-profile__count';
  return <td className={className}>{value}</td>;
}
