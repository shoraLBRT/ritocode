import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import type { TaskStatus, TaskSummary } from '../api';
import { useT } from '../i18n';
import { Icon } from '../pages/task/parts';

/**
 * The arena's task table (DESIGN.md › The arena), shared by `/` and `/tasks`: status · title · time ·
 * difficulty, in the order given. No classes and no number of findings: either would give the answer
 * away (SPEC §4.3). The status column is there only when the API said a `status`, which it says only
 * to a signed-in caller: new (an empty ring, as before), tried, solved, or the answer revealed — each
 * a mark named in words. The whole row is the target: the title's link stretches over it (CSS), so
 * there is still one link per row, named by the task.
 */
export function TaskTable({ tasks }: { tasks: readonly TaskSummary[] }) {
  const t = useT();
  const signedIn = tasks.some((task) => task.status !== null);

  return (
    <div className="table-scroll">
      <table className="rc-task-table">
        <thead>
          <tr>
            {signedIn && <th scope="col">{t('taskTable.status')}</th>}
            <th scope="col">{t('taskTable.task')}</th>
            <th scope="col" className="rc-task-table__time">
              {t('taskTable.time')}
            </th>
            <th scope="col">{t('taskTable.difficulty')}</th>
          </tr>
        </thead>
        <tbody>
          {tasks.map((task) => (
            <tr key={task.slug}>
              {signedIn && (
                <td>
                  <StatusMark status={task.status ?? 'new'} />
                </td>
              )}
              <td>
                <Link className="rc-task-table__title" to={`/tasks/${task.slug}`}>
                  {task.title}
                </Link>
              </td>
              <td className="rc-task-table__time">{t(`tasks.time.${task.difficulty}`)}</td>
              <td>
                <span className={`rc-difficulty rc-difficulty--${task.difficulty}`}>{t(`tasks.difficulty.${task.difficulty}`)}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const statusClass: Record<TaskStatus, string> = {
  new: 'rc-status--todo',
  tried: 'rc-status--tried',
  solved: 'rc-status--done',
  gaveUp: 'rc-status--revealed',
};

/** A task's status as a small round mark (`rc-status`), named in words for a screen reader. */
export function StatusMark({ status }: { status: TaskStatus }) {
  const t = useT();

  return (
    <span className={`rc-status ${statusClass[status]}`} role="img" aria-label={t(`taskTable.statuses.${status}`)} title={t(`taskTable.statuses.${status}`)}>
      {status === 'solved' && <Icon name="check" />}
      {status === 'gaveUp' && <Icon name="eye" />}
    </span>
  );
}

export interface TabOption<T extends string> {
  readonly value: T;
  readonly label: string;
  /** Left out, the tab shows no number: a filter whose counts are not known without more requests. */
  readonly count?: number;
}

interface Thumb {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

/**
 * Segmented tabs (`rc-tabs`): a filter of two to five exclusive options, each with its count where there is one.
 * The pressed mark is one "thumb" that slides and resizes to the pressed tab (CSS transition on `transform` and
 * `width`); it is measured from the tab's offsets, also on a second line when the tabs wrap. Where it cannot be
 * measured (no layout, as in tests) the pressed tab keeps its own background.
 */
export function Tabs<T extends string>({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: readonly TabOption<T>[];
  value: T;
  onChange: (value: T) => void;
}) {
  const group = useRef<HTMLDivElement>(null);
  const [thumb, setThumb] = useState<Thumb | null>(null);
  const [ready, setReady] = useState(false);

  const measure = useCallback(() => {
    const pressed = group.current?.querySelector<HTMLElement>('[aria-pressed="true"]');
    if (pressed === null || pressed === undefined || pressed.offsetWidth === 0) {
      setThumb(null);
      return;
    }
    const next = { x: pressed.offsetLeft, y: pressed.offsetTop, width: pressed.offsetWidth, height: pressed.offsetHeight };
    setThumb((previous) =>
      previous !== null && previous.x === next.x && previous.y === next.y && previous.width === next.width && previous.height === next.height ? previous : next,
    );
  }, []);

  useLayoutEffect(() => {
    measure();
  }, [measure, value, options]);

  // The first measurement is painted without a transition; the thumb moves only after that.
  useEffect(() => {
    if (thumb === null || ready) return;
    const frame = requestAnimationFrame(() => {
      setReady(true);
    });
    return () => {
      cancelAnimationFrame(frame);
    };
  }, [thumb, ready]);

  useEffect(() => {
    const element = group.current;
    if (element === null || typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    for (const child of element.children) observer.observe(child);
    return () => {
      observer.disconnect();
    };
  }, [measure, options]);

  return (
    <div ref={group} className="rc-tabs" role="group" aria-label={label} data-thumb={thumb !== null ? (ready ? 'ready' : 'set') : undefined}>
      {thumb !== null && (
        <span
          className="rc-tabs__thumb"
          aria-hidden="true"
          style={{ width: thumb.width, height: thumb.height, transform: `translate(${String(thumb.x)}px, ${String(thumb.y)}px)` }}
        />
      )}
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          className="rc-tab"
          aria-pressed={value === option.value}
          onClick={() => {
            onChange(option.value);
          }}
        >
          {option.label}
          {option.count !== undefined && <span className="rc-tab__count">{option.count}</span>}
        </button>
      ))}
    </div>
  );
}
