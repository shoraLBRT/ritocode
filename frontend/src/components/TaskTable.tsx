import { Link } from 'react-router';
import type { TaskSummary } from '../api';
import { useT } from '../i18n';
import { Icon } from '../pages/task/parts';

/**
 * The arena's task table (DESIGN.md › The arena), shared by `/` and `/tasks`: status · title · time ·
 * difficulty, in the order given. No classes and no number of findings: either would give the answer
 * away (SPEC §4.3). The status column is there only when the API said `solved`, which it says only to
 * a signed-in caller.
 */
export function TaskTable({ tasks }: { tasks: readonly TaskSummary[] }) {
  const t = useT();
  const signedIn = tasks.some((task) => task.solved !== null);

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
                  {task.solved === true ? (
                    <span className="rc-status rc-status--done" role="img" aria-label={t('taskTable.solved')}>
                      <Icon name="check" />
                    </span>
                  ) : (
                    <span className="rc-status rc-status--todo" role="img" aria-label={t('taskTable.unsolved')} />
                  )}
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

export interface TabOption<T extends string> {
  readonly value: T;
  readonly label: string;
  readonly count: number;
}

/** Segmented tabs (`rc-tabs`): a filter of two to five exclusive options, each with its count. */
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
  return (
    <div className="rc-tabs" role="group" aria-label={label}>
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
          <span className="rc-tab__count">{option.count}</span>
        </button>
      ))}
    </div>
  );
}
