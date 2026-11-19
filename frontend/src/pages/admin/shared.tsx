import { useId } from 'react';
import type { ReactNode } from 'react';
import type { AdminLearner, Page } from '../../api';
import { EmptyState } from '../../components/EmptyState';
import type { EmptyIcon } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { AdminRowsSkeleton } from '../../components/skeletons';
import type { ApiResourceState } from '../../hooks/useApiResource';
import { useLocale, useT } from '../../i18n';

/** What the admin pages share: the layout's context, a panel with its bar, a date, a learner, a pager. */

/** A timestamp as a date and a time, in the reader's own zone. */
export function AdminDate({ value }: { value: string }) {
  const locale = useLocale();
  const text = new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));

  return <time dateTime={value}>{text}</time>;
}

/** The learner by e-mail, or a word saying the user is gone. */
export function LearnerName({ learner }: { learner: AdminLearner }) {
  const t = useT();

  return learner.email ?? <span className="rc-admin__muted">{t('admin.unknownUser')}</span>;
}

/**
 * The panel of a list page: its bar holds the section's heading (an `h2` — the page's one `h1` is the
 * layout's) and lead, the filters, and the total at the right end; the body and the foot follow.
 */
export function AdminPanel({
  title,
  lead,
  filters,
  total,
  children,
}: {
  title: string;
  lead?: string;
  filters?: ReactNode;
  total?: ReactNode;
  children: ReactNode;
}) {
  const headingId = useId();

  return (
    <section className="rc-panel rc-admin__panel" aria-labelledby={headingId}>
      <div className="rc-panel__bar">
        <div className="rc-admin__title">
          <h2 id={headingId}>{title}</h2>
          {lead && <p>{lead}</p>}
        </div>
        {filters && <div className="rc-list-filters">{filters}</div>}
        {total}
      </div>
      {children}
    </section>
  );
}

/** The number of rows a list holds, said in the bar's right end. */
export function AdminTotal({ count }: { count: number }) {
  const t = useT();

  return (
    <span className="rc-arena__note rc-admin__total" role="status">
      {t('admin.total', { count })}
    </span>
  );
}

/** A list's body in each of its states: loading, failed, empty, or the table `children` draws. */
export function ListBody<T>({
  state,
  reload,
  empty,
  columns = 5,
  icon = 'inbox',
  children,
}: {
  state: ApiResourceState<Page<T>>;
  reload: () => void;
  empty: string;
  /** How many columns the table has, so its skeleton is as wide. */
  columns?: number;
  icon?: EmptyIcon;
  children: (page: Page<T>) => ReactNode;
}) {
  const t = useT();

  switch (state.status) {
    case 'loading':
      return <LoadingState label={t('admin.loading')} skeleton={<AdminRowsSkeleton columns={columns} />} />;
    case 'error':
      return <ErrorState error={state.error} onRetry={reload} />;
    case 'success':
      return state.data.items.length === 0 ? <EmptyState title={empty} icon={icon} /> : children(state.data);
  }
}

/** A table in its own scrolling region, so a phone scrolls the table and never the page. */
export function AdminTable({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="table-scroll" role="region" aria-label={label} tabIndex={0}>
      <table className="rc-admin__table">{children}</table>
    </div>
  );
}

/** Previous and next, between pages of an admin list. Nothing to page, nothing shown. */
export function Pager({ page, onPage }: { page: Page<unknown>; onPage: (page: number) => void }) {
  const t = useT();

  if (page.totalPages <= 1) {
    return null;
  }

  return (
    <nav className="rc-admin__foot" aria-label={t('admin.pager')}>
      <button
        type="button"
        className="button button--small"
        disabled={!page.hasPreviousPage}
        onClick={() => {
          onPage(page.pageNumber - 1);
        }}
      >
        {t('admin.previous')}
      </button>
      <span>{t('admin.pageOf', { page: page.pageNumber, pages: page.totalPages })}</span>
      <button
        type="button"
        className="button button--small"
        disabled={!page.hasNextPage}
        onClick={() => {
          onPage(page.pageNumber + 1);
        }}
      >
        {t('admin.next')}
      </button>
    </nav>
  );
}
