import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { TaskDetail } from '../../api';
import { Markdown } from '../../components/Markdown';
import { useT } from '../../i18n';
import { Icon } from './parts';

/**
 * The task's context in the left column (docs/SPEC.md §4.4, docs/DESIGN.md › The task workspace):
 * the title, the difficulty and the brief — what the agent was asked — always; *Весь контекст*
 * adds the context itself and the way back to the tasks. The page decides when it is open: open on
 * diagnosis, folded from treatment on. `children` is what the page adds under it (the review's
 * other tasks over the same code).
 */
export function ContextPanel({ task, open, onToggle, children }: { task: TaskDetail; open: boolean; onToggle: () => void; children?: ReactNode }) {
  const t = useT();

  return (
    <section className="panel context" aria-labelledby="task-title">
      <h1 id="task-title" className="context__title">
        {task.title}
      </h1>
      <span className="tag context__difficulty">{t('task.difficulty', { level: t(`tasks.difficulty.${task.difficulty}`), time: t(`tasks.time.${task.difficulty}`) })}</span>

      <div className="context__brief">
        <span className="eyebrow">{t('task.brief')}</span>
        <Markdown source={task.brief} />
      </div>

      {open && (
        <div className="context__more" id="task-context-more">
          {/* Prose written in the content files, a list of facts allowed (CONTENT_FORMAT §6). */}
          <Markdown source={task.context} />
          <p className="context__meta">
            <Link to="/tasks">{t('task.tasks')}</Link>
            <span aria-hidden="true">{'/'}</span>
            <span>{task.title}</span>
          </p>
        </div>
      )}

      <button type="button" className={`link-button context__toggle${open ? ' context__toggle--open' : ''}`} aria-expanded={open} aria-controls="task-context-more" onClick={onToggle}>
        {open ? t('task.hideContext') : t('task.showContext')}
        <Icon name="down" />
      </button>

      {children}
    </section>
  );
}
