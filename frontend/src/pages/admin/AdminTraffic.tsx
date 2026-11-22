import { useState } from 'react';
import { readAdminTraffic, useApiClient } from '../../api';
import type { TrafficCount, TrafficReport } from '../../api';
import { Skeleton } from '../../components/Skeleton';
import { Tabs } from '../../components/TaskTable';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { RANGES, utcDate } from './range';
import type { Range } from './range';
import { AdminPanel } from './shared';

/** The funnel's steps, in order, as the API sends them: each a key of the strings under `admin.traffic.steps`. */
const STEPS = ['task-opened', 'step-2-reached', 'check-signed-out', 'sign-in-completed', 'attempt-submitted'] as const;

/**
 * The overview's traffic panel (#306): visitors, visits and page views, the top referrers and pages, and the
 * funnel from an opened task to a submitted attempt, as Umami counted them over the last 7, 30 or 90 UTC days,
 * with a link to Umami's own dashboard. Umami not configured, down or refusing the token is said in the panel;
 * the rest of the overview does not depend on it.
 */
export function AdminTraffic() {
  const client = useApiClient();
  const t = useT();
  const [range, setRange] = useState<Range>('30');

  const traffic = useApiResource(
    (signal) => readAdminTraffic(client, { from: utcDate(Number(range) - 1), to: utcDate(0) }, signal),
    [client, range],
  );
  const state = traffic.state;
  const dashboard = state.status === 'success' ? state.data.dashboardUrl : null;

  return (
    <AdminPanel
      title={t('admin.traffic.title')}
      lead={t('admin.traffic.lead')}
      filters={
        <Tabs
          label={t('admin.traffic.range')}
          options={RANGES.map((value) => ({ value, label: t('admin.traffic.days', { count: value }) }))}
          value={range}
          onChange={setRange}
        />
      }
      total={
        dashboard ? (
          <a className="rc-admin__more" href={dashboard} target="_blank" rel="noopener noreferrer">
            {t('admin.traffic.dashboard')}
          </a>
        ) : null
      }
    >
      <div className="rc-admin__activity">
        {state.status === 'loading' ? <Skeleton className="rc-admin__activity-skeleton" height={180} /> : null}
        {state.status === 'error' ? (
          <p className="rc-admin__activity-failed">
            {t('admin.traffic.failed')}{' '}
            <button type="button" className="rc-admin__more" onClick={traffic.reload}>
              {t('admin.traffic.retry')}
            </button>
          </p>
        ) : null}
        {state.status === 'success' ? <Report report={state.data} /> : null}
      </div>
    </AdminPanel>
  );
}

function Report({ report }: { report: TrafficReport }) {
  const t = useT();

  if (report.status !== 'ok' || !report.summary) {
    const message = report.status === 'refused' ? 'refused' : report.status === 'notConfigured' ? 'notConfigured' : 'failing';
    return (
      <p className="rc-admin__traffic-state" role="status" data-status={report.status}>
        {t(`admin.traffic.${message}`)}
      </p>
    );
  }

  const { summary } = report;

  return (
    <>
      <dl className="rc-admin__activity-summary rc-admin__traffic-summary">
        <Figure label={t('admin.traffic.visitors')} value={summary.visitors} />
        <Figure label={t('admin.traffic.visits')} value={summary.visits} />
        <Figure label={t('admin.traffic.pageViews')} value={summary.pageViews} />
      </dl>
      <div className="rc-admin__traffic-lists">
        <TopList title={t('admin.traffic.referrers')} rows={report.referrers} empty={t('admin.traffic.noReferrers')} />
        <TopList title={t('admin.traffic.pages')} rows={report.pages} empty={t('admin.traffic.noPages')} mono />
      </div>
      <Funnel funnel={report.funnel} />
    </>
  );
}

function Figure({ label, value }: { label: string; value: number }) {
  return (
    <div className="rc-admin__activity-figure">
      <dt>{label}</dt>
      <dd>
        <span className="rc-admin__activity-value">{value}</span>
      </dd>
    </div>
  );
}

function TopList({ title, rows, empty, mono }: { title: string; rows: readonly TrafficCount[]; empty: string; mono?: boolean }) {
  return (
    <section className="rc-admin__traffic-list">
      <h3>{title}</h3>
      {rows.length === 0 ? (
        <p className="rc-admin__activity-note">{empty}</p>
      ) : (
        <ol>
          {rows.map((row) => (
            <li key={row.name}>
              <span className={mono ? 'rc-admin__traffic-name rc-admin__mono' : 'rc-admin__traffic-name'} title={row.name}>
                {row.name}
              </span>
              <span className="rc-admin__chart-total">{row.count}</span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

/** The five steps, one bar a step scaled to the first, with each step's share of the first. */
function Funnel({ funnel }: { funnel: readonly TrafficCount[] }) {
  const t = useT();
  const counts = new Map(funnel.map((step) => [step.name, step.count]));
  const first = counts.get(STEPS[0]) ?? 0;

  return (
    <section className="rc-admin__traffic-list">
      <h3>{t('admin.traffic.funnel')}</h3>
      <ol className="rc-admin__funnel" aria-label={t('admin.traffic.funnelLabel')}>
        {STEPS.map((step) => {
          const count = counts.get(step) ?? 0;
          const share = first > 0 ? Math.round((count / first) * 100) : null;
          return (
            <li key={step} className="rc-admin__funnel-step">
              <span className="rc-admin__traffic-name">{t(`admin.traffic.steps.${step}`)}</span>
              <span className="rc-admin__funnel-bar" aria-hidden="true">
                <span style={{ width: `${String(share === null ? 0 : Math.min(share, 100))}%` }} />
              </span>
              <span className="rc-admin__chart-total">
                {count}
                {share === null ? null : <span className="rc-admin__funnel-share">{' '}{t('admin.traffic.ofFirst', { share })}</span>}
              </span>
            </li>
          );
        })}
      </ol>
      <p className="rc-admin__activity-note">{t('admin.traffic.funnelNote')}</p>
    </section>
  );
}
