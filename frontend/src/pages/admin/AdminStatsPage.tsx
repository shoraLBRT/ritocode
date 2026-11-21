import { useState } from 'react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { readAdminCardStats, readAdminTaskStats, useApiClient } from '../../api';
import type { CardStats, TaskStats } from '../../api';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { AdminRowsSkeleton } from '../../components/skeletons';
import { useApiResource } from '../../hooks/useApiResource';
import type { ApiResourceState } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import type { MessageKey } from '../../i18n';
import { AdminPanel, AdminTable, AdminTotal } from './shared';

type Direction = 'asc' | 'desc';

/** A column a table sorts by: its heading, how to read a row's value, and whether it is a figure. */
interface Column<T> {
  readonly key: string;
  readonly label: MessageKey;
  readonly value: (row: T) => number | string | null;
  readonly numeric?: boolean;
}

const TASK_COLUMNS: readonly Column<TaskStats>[] = [
  { key: 'task', label: 'admin.statsPage.task', value: (task) => task.title.toLocaleLowerCase('ru') },
  { key: 'difficulty', label: 'admin.statsPage.difficulty', value: (task) => (task.difficulty ? ['easy', 'medium', 'hard'].indexOf(task.difficulty) : null), numeric: true },
  { key: 'first', label: 'admin.statsPage.first', value: (task) => task.firstAttempts, numeric: true },
  { key: 'practice', label: 'admin.statsPage.practice', value: (task) => task.practiceAttempts, numeric: true },
  { key: 'solved', label: 'admin.statsPage.solved', value: (task) => task.solvedShare, numeric: true },
  { key: 'revealed', label: 'admin.statsPage.revealed', value: (task) => task.revealedShare, numeric: true },
  { key: 'time', label: 'admin.statsPage.time', value: (task) => task.time?.medianSeconds ?? null, numeric: true },
  { key: 'open', label: 'admin.statsPage.open', value: (task) => task.open.diagnosis + task.open.treatment, numeric: true },
  { key: 'score', label: 'admin.statsPage.score', value: (task) => task.meanScoreShare, numeric: true },
];

const CARD_COLUMNS: readonly Column<CardStats>[] = [
  { key: 'card', label: 'admin.statsPage.card', value: (card) => card.name.toLocaleLowerCase('ru') },
  { key: 'class', label: 'admin.statsPage.class', value: (card) => card.className },
  { key: 'met', label: 'admin.statsPage.met', value: (card) => card.met, numeric: true },
  { key: 'found', label: 'admin.statsPage.found', value: (card) => card.found, numeric: true },
  { key: 'missed', label: 'admin.statsPage.missed', value: (card) => card.missed, numeric: true },
  { key: 'pickedWhenAbsent', label: 'admin.statsPage.pickedWhenAbsent', value: (card) => card.pickedWhenAbsent, numeric: true },
  { key: 'treatedRight', label: 'admin.statsPage.treatedRight', value: (card) => card.treatedRight, numeric: true },
];

/**
 * `/admin/stats` — how learners fare, per task and per card (#305), to calibrate the size bands and the
 * scoring (SPEC §3.4, §5, §8). Two tables, each sorted by any column; a task or a card leads to its
 * attempts, and an outlier carries a chip.
 */
export function AdminStatsPage() {
  const client = useApiClient();
  const t = useT();

  const tasks = useApiResource((signal) => readAdminTaskStats(client, signal), [client]);
  const cards = useApiResource((signal) => readAdminCardStats(client, signal), [client]);

  return (
    <>
      <AdminPanel
        title={t('admin.statsPage.tasks')}
        lead={t('admin.statsPage.tasksLead')}
        total={tasks.state.status === 'success' ? <AdminTotal count={tasks.state.data.tasks.length} /> : null}
      >
        <Body state={tasks.state} reload={tasks.reload} rows={(report) => report.tasks} columns={TASK_COLUMNS.length}>
          {(rows) => <SortedTable label={t('admin.statsPage.tasks')} columns={TASK_COLUMNS} rows={rows} initial="first" row={TaskRow} rowKey={(task) => task.task} />}
        </Body>
      </AdminPanel>

      <AdminPanel
        title={t('admin.statsPage.cards')}
        lead={t('admin.statsPage.cardsLead', { count: cards.state.status === 'success' ? cards.state.data.firstAttempts : 0 })}
        total={cards.state.status === 'success' ? <AdminTotal count={cards.state.data.cards.length} /> : null}
      >
        <Body state={cards.state} reload={cards.reload} rows={(report) => report.cards} columns={CARD_COLUMNS.length}>
          {(rows) => <SortedTable label={t('admin.statsPage.cards')} columns={CARD_COLUMNS} rows={rows} initial="met" row={CardRow} rowKey={(card) => card.card} />}
        </Body>
      </AdminPanel>
    </>
  );
}

/** A report's body in each of its states: loading, failed, empty, or the table. */
function Body<R, T>({
  state,
  reload,
  rows,
  columns,
  children,
}: {
  state: ApiResourceState<R>;
  reload: () => void;
  rows: (report: R) => readonly T[];
  columns: number;
  children: (rows: readonly T[]) => ReactNode;
}) {
  const t = useT();

  switch (state.status) {
    case 'loading':
      return <LoadingState label={t('admin.loading')} skeleton={<AdminRowsSkeleton columns={columns} />} />;
    case 'error':
      return <ErrorState error={state.error} onRetry={reload} />;
    case 'success': {
      const list = rows(state.data);
      return list.length === 0 ? <EmptyState title={t('admin.statsPage.empty')} icon="tasks" /> : children(list);
    }
  }
}

/**
 * A table sorted by one column at a time: a heading is a button, pressed again it turns the order. Figures
 * sort largest first, text from A; a row without the value goes last either way.
 */
function SortedTable<T>({
  label,
  columns,
  rows,
  initial,
  row: Row,
  rowKey,
}: {
  label: string;
  columns: readonly Column<T>[];
  rows: readonly T[];
  initial: string;
  row: (props: { item: T }) => ReactNode;
  rowKey: (row: T) => string;
}) {
  const t = useT();
  const [sort, setSort] = useState<{ key: string; direction: Direction }>({ key: initial, direction: 'desc' });
  const column = columns.find((candidate) => candidate.key === sort.key) ?? columns[0];

  const sorted = column ? [...rows].sort((a, b) => compare(column.value(a), column.value(b), sort.direction)) : [...rows];

  return (
    <AdminTable label={label}>
      <thead>
        <tr>
          {columns.map((candidate) => {
            const active = candidate.key === sort.key;
            return (
              <th
                key={candidate.key}
                scope="col"
                className={candidate.numeric ? 'rc-admin__num' : undefined}
                aria-sort={active ? (sort.direction === 'asc' ? 'ascending' : 'descending') : undefined}
              >
                <button
                  type="button"
                  className="rc-admin__sort"
                  onClick={() => {
                    setSort(
                      active
                        ? { key: candidate.key, direction: sort.direction === 'asc' ? 'desc' : 'asc' }
                        : { key: candidate.key, direction: candidate.numeric ? 'desc' : 'asc' },
                    );
                  }}
                >
                  {t(candidate.label)}
                  <span className="rc-admin__sort-mark" aria-hidden="true">
                    {active ? (sort.direction === 'asc' ? '↑' : '↓') : ''}
                  </span>
                </button>
              </th>
            );
          })}
        </tr>
      </thead>
      <tbody>
        {sorted.map((item) => (
          <Row key={rowKey(item)} item={item} />
        ))}
      </tbody>
    </AdminTable>
  );
}

function compare(a: number | string | null, b: number | string | null, direction: Direction): number {
  if (a === null || b === null) {
    return a === b ? 0 : a === null ? 1 : -1;
  }

  const order = typeof a === 'number' && typeof b === 'number' ? a - b : String(a).localeCompare(String(b), 'ru');
  return direction === 'asc' ? order : -order;
}

function Share({ value }: { value: number | null }) {
  const t = useT();

  return value === null ? <span className="rc-admin__muted">{t('admin.none')}</span> : <>{t('admin.statsPage.percent', { value: Math.round(value * 100) })}</>;
}

/** Minutes and seconds, as `m:ss`. */
function clock(seconds: number): string {
  return `${String(Math.floor(seconds / 60))}:${String(seconds % 60).padStart(2, '0')}`;
}

function TaskRow({ item: task }: { item: TaskStats }) {
  const t = useT();
  const band = task.band;
  const slower = band !== null && task.time !== null && task.time.medianSeconds > band.minutesTo * 60;

  return (
    <tr className="rc-admin__row">
      <td className="rc-admin__text">
        <Link className="rc-admin__link" to={`/admin/attempts?task=${encodeURIComponent(task.task)}`}>
          {task.title}
        </Link>
        <span className="rc-admin__sub rc-admin__mono">{task.task}</span>
        {(task.rarelySolved || !task.published) && (
          <span className="rc-admin__chips">
            {task.rarelySolved && <span className="rc-admin__chip rc-admin__chip--warn">{t('admin.statsPage.rarelySolved')}</span>}
            {!task.published && <span className="rc-admin__chip">{t('admin.statsPage.unpublished')}</span>}
          </span>
        )}
      </td>
      <td>
        {task.difficulty ? (
          <span className="rc-admin__chip">{t(`tasks.difficulty.${task.difficulty}`)}</span>
        ) : (
          <span className="rc-admin__muted">{t('admin.none')}</span>
        )}
      </td>
      <td className="rc-admin__num">{task.firstAttempts}</td>
      <td className="rc-admin__num">{task.practiceAttempts}</td>
      <td className="rc-admin__num">
        <Share value={task.solvedShare} />
      </td>
      <td className="rc-admin__num">
        <Share value={task.revealedShare} />
      </td>
      <td className="rc-admin__num">
        {task.time ? (
          <span title={t('admin.statsPage.timeTitle', { count: task.time.count })}>
            <span className={slower ? 'rc-admin__over' : undefined}>{clock(task.time.medianSeconds)}</span>
            <span className="rc-admin__sub">
              {t('admin.statsPage.spread', { from: clock(task.time.lowerQuartileSeconds), to: clock(task.time.upperQuartileSeconds) })}
            </span>
          </span>
        ) : (
          <span className="rc-admin__muted">{t('admin.none')}</span>
        )}
        {band && (
          <span className="rc-admin__sub">
            {band.minutesFrom === band.minutesTo
              ? t('admin.statsPage.bandAbout', { minutes: band.minutesTo, lines: task.lines ?? 0, maxLines: band.maxLines })
              : t('admin.statsPage.band', { from: band.minutesFrom, to: band.minutesTo, lines: task.lines ?? 0, maxLines: band.maxLines })}
          </span>
        )}
      </td>
      <td className="rc-admin__num">
        <span title={t('admin.statsPage.openTitle')}>
          {t('admin.statsPage.openSteps', { diagnosis: task.open.diagnosis, treatment: task.open.treatment })}
        </span>
      </td>
      <td className="rc-admin__num">
        <Share value={task.meanScoreShare} />
      </td>
    </tr>
  );
}

function CardRow({ item: card }: { item: CardStats }) {
  const t = useT();

  return (
    <tr className="rc-admin__row">
      <td className="rc-admin__text">
        <Link className="rc-admin__link" to={`/admin/attempts?card=${encodeURIComponent(card.card)}`}>
          {card.name}
        </Link>
        <span className="rc-admin__sub rc-admin__mono">{card.card}</span>
        {card.pickedMoreThanFound && (
          <span className="rc-admin__chips">
            <span className="rc-admin__chip rc-admin__chip--warn">{t('admin.statsPage.pickedMoreThanFound')}</span>
          </span>
        )}
      </td>
      <td className="rc-admin__text">{card.className ?? <span className="rc-admin__muted">{t('admin.none')}</span>}</td>
      <td className="rc-admin__num">{card.met}</td>
      <td className="rc-admin__num">{card.found}</td>
      <td className="rc-admin__num">{card.missed}</td>
      <td className="rc-admin__num">{card.pickedWhenAbsent}</td>
      <td className="rc-admin__num">{card.treatedRight}</td>
    </tr>
  );
}
