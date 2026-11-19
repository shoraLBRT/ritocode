import { useState } from 'react';
import { readAdminActivity, useApiClient } from '../../api';
import type { ActivityBucket, ActivityReport } from '../../api';
import { Skeleton } from '../../components/Skeleton';
import { Tabs } from '../../components/TaskTable';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { AdminPanel } from './shared';

/** The ranges the picker offers, in days; 90 is read by week so the bars stay readable. */
const RANGES = ['7', '30', '90'] as const;
type Range = (typeof RANGES)[number];

/** The figures charted, in order: each is a key of a bucket and of the strings under `admin.activity`. */
const FIGURES = [
  'activeLearners',
  'newUsers',
  'attemptsStarted',
  'attemptsSubmitted',
  'answersRevealed',
  'signalsSent',
  'cardsStudied',
] as const satisfies readonly (keyof ActivityBucket)[];

/** A UTC date as `yyyy-MM-dd`, `days` before today. */
function utcDate(daysAgo: number, now = new Date()): string {
  const date = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - daysAgo));
  return date.toISOString().slice(0, 10);
}

/**
 * The overview's usage over time (#304): learners active, sign-ups and what learners did, one small bar
 * chart a figure, over the last 7, 30 or 90 UTC days, with active learners over the day, week and month
 * that end today and the share who came back a week later.
 */
export function AdminActivity() {
  const client = useApiClient();
  const t = useT();
  const [range, setRange] = useState<Range>('30');

  const activity = useApiResource(
    (signal) =>
      readAdminActivity(client, { from: utcDate(Number(range) - 1), to: utcDate(0), bucket: range === '90' ? 'week' : 'day' }, signal),
    [client, range],
  );
  const state = activity.state;

  return (
    <AdminPanel
      title={t('admin.activity.title')}
      lead={t('admin.activity.lead')}
      filters={
        <Tabs
          label={t('admin.activity.range')}
          options={RANGES.map((value) => ({ value, label: t('admin.activity.days', { count: value }) }))}
          value={range}
          onChange={setRange}
        />
      }
    >
      <div className="rc-admin__activity">
        {state.status === 'loading' ? <Skeleton className="rc-admin__activity-skeleton" height={180} /> : null}
        {state.status === 'error' ? (
          <p className="rc-admin__activity-failed">
            {t('admin.activity.failed')}{' '}
            <button type="button" className="rc-admin__more" onClick={activity.reload}>
              {t('admin.activity.retry')}
            </button>
          </p>
        ) : null}
        {state.status === 'success' ? <Report report={state.data} /> : null}
      </div>
    </AdminPanel>
  );
}

function Report({ report }: { report: ActivityReport }) {
  const t = useT();
  const { summary } = report;
  const share = summary.learners > 0 ? Math.round((summary.returnedAfterWeek / summary.learners) * 100) : null;

  return (
    <>
      <dl className="rc-admin__activity-summary">
        <Figure label={t('admin.activity.dau')} value={String(summary.dailyActive)} />
        <Figure label={t('admin.activity.wau')} value={String(summary.weeklyActive)} />
        <Figure label={t('admin.activity.mau')} value={String(summary.monthlyActive)} />
        <Figure
          label={t('admin.activity.returned')}
          value={share === null ? t('admin.none') : `${String(share)}%`}
          hint={t('admin.activity.returnedOf', { returned: summary.returnedAfterWeek, learners: summary.learners })}
        />
      </dl>
      <ul className="rc-admin__charts">
        {FIGURES.map((figure) => (
          <li key={figure}>
            <BarChart label={t(`admin.activity.${figure}`)} buckets={report.buckets} figure={figure} />
          </li>
        ))}
      </ul>
      <p className="rc-admin__activity-note">{t(report.bucket === 'week' ? 'admin.activity.noteWeek' : 'admin.activity.noteDay')}</p>
    </>
  );
}

function Figure({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rc-admin__activity-figure">
      <dt>{label}</dt>
      <dd>
        <span className="rc-admin__activity-value">{value}</span>
        {hint ? <span className="rc-admin__activity-hint">{hint}</span> : null}
      </dd>
    </div>
  );
}

/** One figure's bars, hand-drawn as the profile's chart is (#281): one bar a bucket, scaled to the largest. */
function BarChart({ label, buckets, figure }: { label: string; buckets: readonly ActivityBucket[]; figure: (typeof FIGURES)[number] }) {
  const t = useT();
  const values = buckets.map((bucket) => bucket[figure]);
  const total = values.reduce((sum, value) => sum + value, 0);
  const max = Math.max(...values, 0);
  const width = Math.max(buckets.length, 1);
  const first = buckets[0];
  const last = buckets[buckets.length - 1];

  return (
    <figure className="rc-admin__chart">
      <figcaption>
        <span>{label}</span>
        <span className="rc-admin__chart-total">{figure === 'activeLearners' ? t('admin.activity.peak', { count: max }) : total}</span>
      </figcaption>
      <svg
        className="rc-admin__chart-svg"
        viewBox={`0 0 ${String(width)} 100`}
        preserveAspectRatio="none"
        role="img"
        aria-label={t('admin.activity.chartLabel', { figure: label, values: values.join(', ') })}
      >
        {max > 0
          ? values.map((value, index) => {
              const height = (value / max) * 100;
              return <rect key={buckets[index]?.start ?? index} x={index + 0.15} y={100 - height} width={0.7} height={height} />;
            })
          : null}
      </svg>
      {first && last ? (
        <div className="rc-admin__chart-axis" aria-hidden="true">
          <span>{first.start.slice(5)}</span>
          <span>{last.end.slice(5)}</span>
        </div>
      ) : null}
    </figure>
  );
}
