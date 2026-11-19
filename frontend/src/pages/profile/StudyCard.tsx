import type { CSSProperties } from 'react';
import { Link } from 'react-router';
import type { ProblemCatalogue, StudyMark } from '../../api';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import type { ApiResource } from '../../hooks/useApiResource';
import { useCountUp } from '../../hooks/useCountUp';
import { useLocale, useT } from '../../i18n';
import { moscowDate, moscowDay } from '../home/arena';
import { CardLoading, ProfileCard } from './parts';
import { CHART_HEIGHT, CHART_WIDTH, chartPath, percent, studyProgress } from './profile';
import type { Study } from './profile';

/** What the study card reads: the published catalogue and the learner's marks on it. */
export interface StudyData {
  readonly catalogue: ProblemCatalogue;
  readonly marks: readonly StudyMark[];
}

/**
 * «Изучение» (docs/SPEC.md §4.7, ADR 0018): the published cards marked studied, in all and per class
 * in the class colours, and a chart of the count over time, cumulative from the first mark. Study is
 * kept apart from task mastery: a card found in a task is not counted here. With no mark the same
 * frame shows zeros and the way to `/problems`.
 */
export function StudyCard({ study, now }: { study: ApiResource<StudyData>; now: Date }) {
  const t = useT();
  const { state, reload } = study;

  return (
    <ProfileCard id="profile-study" title={t('profile.study.title')} index={4} className="rc-profile__study">
      {state.status === 'loading' && (
        <CardLoading label={t('profile.study.loading')}>
          <Skeleton height={28} width="30%" />
          <Skeleton className="rc-profile__chart-skeleton" />
        </CardLoading>
      )}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <StudyBody study={studyProgress(state.data.catalogue, state.data.marks)} now={now} />}
    </ProfileCard>
  );
}

function StudyBody({ study, now }: { study: Study; now: Date }) {
  const t = useT();
  const shown = useCountUp(study.studied);

  return (
    <>
      <div className="rc-profile__study-head">
        <p className="rc-profile__study-total" aria-label={t('profile.study.count', { studied: study.studied, total: study.total })}>
          <span className="rc-profile__study-value" aria-hidden="true">
            {shown}
          </span>
          <span className="rc-profile__study-of" aria-hidden="true">
            {t('profile.study.of', { total: study.total })} {t('profile.study.caption')}
          </span>
        </p>
        <span
          className="rc-bar rc-profile__bar rc-profile__study-bar"
          aria-hidden="true"
          style={{ '--p': percent(study.studied, study.total) } as CSSProperties}
        />
      </div>
      <ul className="rc-profile__study-classes">
        {study.byClass.map((item) => (
          <li key={item.class} className={`rc-profile__class rc-profile__study-class k-${item.class}`}>
            <span className="rc-profile__class-name">{item.name}</span>
            <span className="rc-profile__mono rc-profile__study-count">{t('profile.study.classCount', { studied: item.studied, total: item.total })}</span>
            <span
              className="rc-bar rc-profile__bar rc-profile__study-class-bar"
              role="img"
              aria-label={t('profile.study.bar', { studied: item.studied, total: item.total })}
              style={{ '--p': percent(item.studied, item.total) } as CSSProperties}
            />
          </li>
        ))}
      </ul>
      {study.timeline.length > 0 ? (
        <StudyChart timeline={study.timeline} now={now} />
      ) : (
        <div className="rc-profile__study-empty">
          <p className="rc-profile__note">{t('profile.study.empty')}</p>
          <p className="rc-profile__start-action">
            <Link className="button button--primary" to="/problems">
              {t('profile.study.action')}
            </Link>
          </p>
        </div>
      )}
      <p className="rc-profile__note">{t('profile.study.note')}</p>
    </>
  );
}

function StudyChart({ timeline, now }: { timeline: Study['timeline']; now: Date }) {
  const t = useT();
  const locale = useLocale();
  const dayFormat = new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short', timeZone: 'UTC' });
  const first = timeline[0];
  const last = timeline.at(-1);

  if (first === undefined || last === undefined) {
    return null;
  }

  const line = chartPath(timeline, moscowDay(now));
  const firstDate = dayFormat.format(moscowDate(first.day));

  return (
    <figure className="rc-profile__chart">
      <div className="rc-profile__chart-plot">
        <span className="rc-profile__chart-max" aria-hidden="true">
          {last.count}
        </span>
        <svg
          className="rc-profile__chart-svg"
          viewBox={`0 0 ${String(CHART_WIDTH)} ${String(CHART_HEIGHT)}`}
          preserveAspectRatio="none"
          role="img"
          aria-label={t('profile.study.chart', { first: firstDate, start: first.count, count: last.count })}
        >
          <path className="rc-profile__chart-area" d={`${line} V${String(CHART_HEIGHT)} H0 Z`} />
          <path className="rc-profile__chart-line" d={line} vectorEffect="non-scaling-stroke" />
        </svg>
      </div>
      <figcaption className="rc-profile__chart-axis" aria-hidden="true">
        <span>{firstDate}</span>
        <span>{t('profile.study.today')}</span>
      </figcaption>
    </figure>
  );
}
