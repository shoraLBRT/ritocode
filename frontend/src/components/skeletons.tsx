import { ACTIVITY_DAYS } from '../pages/home/arena';
import { useT } from '../i18n';
import { Skeleton } from './Skeleton';

/*
 * Blocks in the shape of what is loading, each laid out with the final markup's classes so the
 * content that replaces them lands at the same height. They are decoration: hand them to
 * `LoadingState` as its `skeleton`, which carries the words and `aria-busy`.
 */

/** Title-bar widths that vary row to row, so a table reads as a list rather than a barcode. */
const TITLE_WIDTHS: readonly string[] = ['62%', '48%', '71%', '55%', '66%', '43%', '58%', '74%', '51%', '68%'];

/** The task table: its header row and `rows` rows (status dot, title bar, time, difficulty pill). */
export function TaskTableSkeleton({
  rows,
  status = true,
  filters = false,
  more = false,
}: {
  rows: number;
  status?: boolean;
  filters?: boolean;
  /** The home panel's "all tasks" line under the table. */
  more?: boolean;
}) {
  const t = useT();

  const table = (
    <div className="table-scroll" aria-hidden="true">
      <table className="rc-task-table">
        <thead>
          <tr>
            {status && <th scope="col">{t('taskTable.status')}</th>}
            <th scope="col">{t('taskTable.task')}</th>
            <th scope="col" className="rc-task-table__time">
              {t('taskTable.time')}
            </th>
            <th scope="col">{t('taskTable.difficulty')}</th>
          </tr>
        </thead>
        <tbody>
          {Array.from({ length: rows }, (_, row) => (
            <tr key={row}>
              {status && (
                <td>
                  <Skeleton className="rc-skeleton--dot" width={20} height={20} />
                </td>
              )}
              <td>
                <Skeleton height={16} width={TITLE_WIDTHS[row % TITLE_WIDTHS.length] ?? '60%'} />
              </td>
              <td className="rc-task-table__time">
                <Skeleton height={16} width={56} />
              </td>
              <td>
                <Skeleton height={24} width={72} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );

  if (!filters) {
    return more ? (
      <>
        {table}
        <div className="rc-arena__more" aria-hidden="true">
          <Skeleton height={21} width={120} />
        </div>
      </>
    ) : (
      table
    );
  }

  // `/tasks` has its filter tabs above the table: a bar as tall as they are keeps the rows where they land.
  return (
    <>
      <div className="rc-panel__bar" aria-hidden="true">
        <Skeleton height={40} width={260} />
        <Skeleton height={40} width={340} />
      </div>
      {table}
    </>
  );
}

/** One of the hero's three figures, before its number is known. */
export function StatSkeleton() {
  return <Skeleton className="rc-skeleton--stat" height={77} />;
}

/** The home page's problems-to-read: four cards of `.rc-arena__problem`, and its "more" line. */
export function ProblemsToReadSkeleton() {
  return (
    <div aria-hidden="true">
      <ul className="rc-arena__problems">
        {[0, 1, 2, 3].map((card) => (
          <li key={card}>
            <div className="rc-arena__problem rc-arena__problem--skeleton">
              <Skeleton height={22} width={88} />
              <Skeleton height={20} width="70%" />
              <Skeleton height={14} />
              <Skeleton height={14} width="85%" />
            </div>
          </li>
        ))}
      </ul>
      <div className="rc-arena__more">
        <Skeleton height={21} width={160} />
      </div>
    </div>
  );
}

/** `/problems` fetched client-side: two class headings, each over a grid of card-shaped blocks. */
export function CatalogueSkeleton() {
  return (
    <div aria-hidden="true">
      <Skeleton className="rc-skeleton--panel" height={166} />
      <div className="rc-catalogue__classes">
        {[0, 1].map((group) => (
          <div key={group} className="rc-catalogue__class">
            <div className="rc-catalogue__class-head">
              <Skeleton height={30} width={176} />
            </div>
            <div className="rc-catalogue__grid">
              {[0, 1, 2, 3].map((card) => (
                <Skeleton key={card} className="rc-skeleton--pcard" />
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

/** The home progress card's body: the ring beside its note, and the six class bars. */
export function ProgressSkeleton() {
  return (
    <div className="rc-arena__progress-skeleton" aria-hidden="true">
      <div className="rc-arena__ring-row">
        <Skeleton className="rc-skeleton--ring" width={104} height={104} />
        <div className="rc-arena__ring-note">
          <Skeleton height={14} />
          <Skeleton height={14} width="70%" />
        </div>
      </div>
      <ul className="rc-arena__bars">
        {Array.from({ length: 6 }, (_, bar) => (
          <li key={bar}>
            <span className="rc-arena__bar-label">
              <Skeleton height={19} width="38%" />
              <Skeleton height={19} width={36} />
            </span>
            <Skeleton className="rc-skeleton--bar" height={6} />
          </li>
        ))}
      </ul>
    </div>
  );
}

/** The home activity card's body: the streak line and the heat map's empty grid. */
export function ActivitySkeleton() {
  return (
    <div className="rc-arena__activity-skeleton" aria-hidden="true">
      <Skeleton height={30} width="45%" />
      <div className="rc-heat">
        {Array.from({ length: ACTIVITY_DAYS }, (_, day) => (
          <span key={day} className="rc-heat__cell" />
        ))}
      </div>
    </div>
  );
}

/** The daily task on the spotlight: a title, the difficulty and time chips, the button. */
export function DailyTaskSkeleton() {
  return (
    <div className="rc-daily__skeleton" aria-hidden="true">
      <Skeleton className="rc-skeleton--on-spotlight" height={32} width="72%" />
      <div className="rc-daily__meta">
        <Skeleton className="rc-skeleton--on-spotlight" height={24} width={72} />
        <Skeleton className="rc-skeleton--on-spotlight" height={24} width={64} />
      </div>
      <Skeleton className="rc-skeleton--on-spotlight" height={36} width={176} />
    </div>
  );
}

/** Rows of an admin table, `columns` cells wide, in the table's own markup. */
export function AdminRowsSkeleton({ columns = 4, rows = 6 }: { columns?: number; rows?: number }) {
  return (
    <div className="table-scroll" aria-hidden="true">
      <table className="rc-admin__table">
        <thead>
          <tr>
            {Array.from({ length: columns }, (_, column) => (
              <th key={column} scope="col">
                <Skeleton height={19} width={column === 0 ? 56 : 72} />
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {Array.from({ length: rows }, (_, row) => (
            <tr key={row}>
              {Array.from({ length: columns }, (_, column) => (
                <td key={column}>
                  <Skeleton height={24} width={column === 1 ? `${String(55 + ((row * 13) % 35))}%` : '60%'} />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
