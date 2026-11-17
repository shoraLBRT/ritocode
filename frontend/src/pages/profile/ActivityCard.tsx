import type { AttemptSummary } from '../../api';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import type { ApiResource } from '../../hooks/useApiResource';
import { useCountUp } from '../../hooks/useCountUp';
import { useLocale, useT } from '../../i18n';
import { activityLevel, moscowDate, YEAR_DAYS, yearActivity } from '../home/arena';
import { CardLoading, ProfileCard } from './parts';

/**
 * A year of activity (docs/SPEC.md §4.7), by the home page's rules: the attempts submitted in it, the
 * active days and the longest streak, then the map — 53 weeks of seven days, today last and outlined,
 * the months labelled under it. The map is one image named in words; each day says its date and count
 * on hover. On a phone it scrolls sideways in its own region.
 */
export function ActivityCard({ year, now }: { year: ApiResource<AttemptSummary[]>; now: Date }) {
  const t = useT();
  const { state, reload } = year;

  return (
    <ProfileCard id="profile-activity" title={t('profile.activity.title')} index={4} className="rc-profile__activity">
      {state.status === 'loading' && (
        <CardLoading label={t('profile.activity.loading')}>
          <Skeleton height={18} width="40%" />
          <Skeleton className="rc-profile__heat-skeleton" />
        </CardLoading>
      )}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <ActivityBody attempts={state.data} now={now} />}
    </ProfileCard>
  );
}

function showToday(region: HTMLDivElement | null) {
  if (region !== null) {
    region.scrollLeft = region.scrollWidth;
  }
}

function ActivityBody({ attempts, now }: { attempts: readonly AttemptSummary[]; now: Date }) {
  const t = useT();
  const locale = useLocale();
  const year = yearActivity(attempts, now);
  const total = useCountUp(year.total);
  const dayFormat = new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });
  const monthFormat = new Intl.DateTimeFormat(locale, { month: 'short', timeZone: 'UTC' });

  return (
    <>
      <div className="rc-profile__activity-summary">
        <p className="rc-profile__activity-total">{t('profile.activity.attempts', { count: total })}</p>
        <p className="rc-profile__activity-stats">
          <span>{t('profile.activity.activeDays', { count: year.activeDays })}</span>
          <span>{t('profile.activity.longestStreak', { count: year.longestStreak })}</span>
        </p>
      </div>
      {/* A region of its own that scrolls sideways on a phone, so the page itself never does; it opens on today. */}
      <div className="rc-profile__heat-scroll" ref={showToday} tabIndex={0} role="region" aria-label={t('profile.activity.region')}>
        <div
          className="rc-profile__heat"
          role="img"
          aria-label={t('profile.activity.map', { count: year.total, days: year.activeDays, longest: year.longestStreak })}
        >
          {year.days.map((count, index) => (
            // The days are positional: the same index is the same day for this render.
            <span
              key={index}
              className="rc-heat__cell rc-profile__heat-cell"
              data-level={activityLevel(count)}
              data-today={index === YEAR_DAYS - 1 ? '' : undefined}
              title={t('profile.activity.cell', { date: dayFormat.format(moscowDate(year.firstDay + index)), count })}
            />
          ))}
        </div>
        <div className="rc-profile__months" aria-hidden="true">
          {year.months.map(({ column, day }) => (
            <span key={day} style={{ gridColumnStart: column + 1 }}>
              {monthFormat.format(moscowDate(day))}
            </span>
          ))}
        </div>
      </div>
    </>
  );
}
