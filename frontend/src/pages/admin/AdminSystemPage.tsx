import type { ReactNode } from 'react';
import { readAdminSystem, useApiClient } from '../../api';
import type { ModuleMigrations, SystemReport, SystemStatus } from '../../api';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { useApiResource } from '../../hooks/useApiResource';
import { useLocale, useT } from '../../i18n';
import type { MessageKey } from '../../i18n';
import { AdminDate, AdminPanel, AdminTable } from './shared';
import { readWebCommit } from './webVersion';

/** How much of a commit is shown; the whole one is in its title. */
const SHORT_COMMIT = 12;

const STATUS: Record<SystemStatus, MessageKey> = {
  ok: 'admin.systemPage.status.ok',
  failing: 'admin.systemPage.status.failing',
  notConfigured: 'admin.systemPage.status.notConfigured',
};

const DEPENDENCY: Record<string, MessageKey> = {
  postgresql: 'admin.systemPage.dependency.postgresql',
  umami: 'admin.systemPage.dependency.umami',
  feedbackGitHubApp: 'admin.systemPage.dependency.feedbackGitHubApp',
};

/**
 * `/admin/system` — what is deployed and how it is (#297): the builds and the content, the database and the
 * services the API depends on, then each module's migrations. One read of `GET /admin/system`, and the web
 * image's commit from its `version.json`.
 */
export function AdminSystemPage() {
  const client = useApiClient();
  const t = useT();

  const report = useApiResource((signal) => readAdminSystem(client, signal), [client]);
  const web = useApiResource((signal) => readWebCommit(signal), []);
  const webCommit = web.state.status === 'success' ? web.state.data : null;

  switch (report.state.status) {
    case 'loading':
      return <LoadingState label={t('admin.loading')} />;
    case 'error':
      return <ErrorState error={report.state.error} onRetry={report.reload} />;
    case 'success':
      return <Report report={report.state.data} webCommit={webCommit} reload={report.reload} />;
  }
}

function Report({ report, webCommit, reload }: { report: SystemReport; webCommit: string | null; reload: () => void }) {
  const t = useT();

  return (
    <>
      <AdminPanel
        title={t('admin.systemPage.deployed')}
        lead={t('admin.systemPage.deployedLead')}
        total={
          <button type="button" className="button button--small" onClick={reload}>
            {t('admin.systemPage.refresh')}
          </button>
        }
      >
        <dl className="rc-admin__facts">
          <Fact label={t('admin.systemPage.apiVersion')}>
            <span className="rc-admin__code">{report.api.version}</span>
          </Fact>
          <Fact label={t('admin.systemPage.apiCommit')}>
            <Commit value={report.api.commit} />
          </Fact>
          <Fact label={t('admin.systemPage.webCommit')}>
            <Commit value={webCommit} />
          </Fact>
          <Fact label={t('admin.systemPage.content')}>
            {report.content ? (
              <>
                <Commit value={report.content.revision} />
                <span className="rc-admin__muted">
                  {' · '}
                  <AdminDate value={report.content.ingestedAt} />
                </span>
              </>
            ) : (
              <span className="rc-admin__muted">{t('admin.systemPage.noContent')}</span>
            )}
          </Fact>
          <Fact label={t('admin.systemPage.checkedAt')}>
            <AdminDate value={report.checkedAt} />
          </Fact>
        </dl>
      </AdminPanel>

      <AdminPanel title={t('admin.systemPage.health')} lead={t('admin.systemPage.healthLead')}>
        <dl className="rc-admin__facts">
          {report.dependencies.map((dependency) => {
            const key = DEPENDENCY[dependency.name];
            return (
              <Fact key={dependency.name} label={key ? t(key) : dependency.name}>
                <State status={dependency.status} />
              </Fact>
            );
          })}
          <Fact label={t('admin.systemPage.databaseSize')}>
            {report.database.sizeBytes === null ? <span className="rc-admin__muted">{t('admin.none')}</span> : <Size bytes={report.database.sizeBytes} />}
          </Fact>
          <Fact label={t('admin.systemPage.backup')}>
            {report.backup ? <AdminDate value={report.backup.at} /> : <span className="rc-admin__muted">{t('admin.systemPage.noBackup')}</span>}
          </Fact>
        </dl>
      </AdminPanel>

      <AdminPanel title={t('admin.systemPage.migrations')} lead={t('admin.systemPage.migrationsLead')}>
        <AdminTable label={t('admin.systemPage.migrations')}>
          <thead>
            <tr>
              <th scope="col">{t('admin.systemPage.module')}</th>
              <th scope="col">{t('admin.systemPage.state')}</th>
              <th scope="col">{t('admin.systemPage.applied')}</th>
              <th scope="col">{t('admin.systemPage.last')}</th>
            </tr>
          </thead>
          <tbody>
            {report.migrations.map((module) => (
              <tr key={module.module} className="rc-admin__row">
                <td className="rc-admin__mono">{module.module}</td>
                <td>
                  <MigrationState module={module} />
                </td>
                <td className="rc-admin__num">{module.applied ?? t('admin.none')}</td>
                <td className="rc-admin__mono">{module.last ?? t('admin.none')}</td>
              </tr>
            ))}
          </tbody>
        </AdminTable>
      </AdminPanel>
    </>
  );
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="rc-admin__fact">
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/** A commit, shortened, the whole one in its title; «неизвестен» when the build was not told it. */
function Commit({ value }: { value: string | null }) {
  const t = useT();

  if (value === null) {
    return <span className="rc-admin__muted">{t('admin.systemPage.unknown')}</span>;
  }

  return (
    <span className="rc-admin__code" title={value}>
      {value.length > SHORT_COMMIT ? value.slice(0, SHORT_COMMIT) : value}
    </span>
  );
}

function State({ status }: { status: SystemStatus }) {
  const t = useT();
  const tone = status === 'notConfigured' ? 'none' : status;

  return <span className={`rc-admin__state rc-admin__state--${tone}`}>{t(STATUS[status])}</span>;
}

function MigrationState({ module }: { module: ModuleMigrations }) {
  const t = useT();

  switch (module.status) {
    case 'upToDate':
      return <span className="rc-admin__state rc-admin__state--ok">{t('admin.systemPage.upToDate')}</span>;
    case 'failing':
      return <span className="rc-admin__state rc-admin__state--failing">{t('admin.systemPage.status.failing')}</span>;
    case 'pending':
      return (
        <span className="rc-admin__state rc-admin__state--failing" title={module.pending.join('\n')}>
          {t('admin.systemPage.pending', { count: module.pending.length })}
        </span>
      );
  }
}

/** Bytes in the largest unit that keeps the number at least one. */
function Size({ bytes }: { bytes: number }) {
  const locale = useLocale();
  const units = ['byte', 'kilobyte', 'megabyte', 'gigabyte'] as const;
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  const text = new Intl.NumberFormat(locale, { style: 'unit', unit: units[unit], unitDisplay: 'short', maximumFractionDigits: 1 }).format(value);

  return <span className="rc-admin__code">{text}</span>;
}
