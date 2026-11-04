import type { AdminLearner, Page } from '../../api';
import { useLocale, useT } from '../../i18n';

/** What the admin tables share: a date, a learner, a pager. */

/** A timestamp as a date and a time, in the reader's own zone. */
export function AdminDate({ value }: { value: string }) {
  const locale = useLocale();
  const text = new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));

  return <time dateTime={value}>{text}</time>;
}

/** The learner by e-mail, or a word saying the user is gone. */
export function LearnerName({ learner }: { learner: AdminLearner }) {
  const t = useT();

  return learner.email ?? <span className="admin-muted">{t('admin.unknownUser')}</span>;
}

/** Previous and next, between pages of an admin list, with the total. Nothing to page, no buttons. */
export function Pager({ page, onPage }: { page: Page<unknown>; onPage: (page: number) => void }) {
  const t = useT();

  return (
    <nav className="admin-pager" aria-label={t('admin.pager')}>
      <span>{t('admin.total', { count: page.totalItems })}</span>
      {page.totalPages > 1 && (
        <>
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
        </>
      )}
    </nav>
  );
}
