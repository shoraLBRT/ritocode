import { Link } from 'react-router';
import type { TaskDetail } from '../../api';
import { Markdown } from '../../components/Markdown';
import { useT } from '../../i18n';
import { Icon } from './parts';

/**
 * The task's context in the sidebar (docs/SPEC.md §4.4, docs/DESIGN.md). The brief — what the agent
 * was asked — is always shown, first; expanding adds where the task sits, its difficulty and the
 * context itself. The page decides when it is open: open on diagnosis, folded from treatment on.
 */
export function ContextPanel({ task, open, onToggle }: { task: TaskDetail; open: boolean; onToggle: () => void }) {
  const t = useT();

  return (
    <section className="panel context" aria-labelledby="task-title">
      <div className="context__head">
        <h1 id="task-title" className="context__title">
          {task.title}
        </h1>
        <button type="button" className="link-button" aria-expanded={open} aria-controls="task-context-more" onClick={onToggle}>
          {open ? t('task.hideContext') : t('task.showContext')}
          <Icon name="down" />
        </button>
      </div>

      <div className="context__brief">
        <span className="eyebrow">{t('task.brief')}</span>
        <Markdown source={task.brief} />
      </div>

      {open && (
        <div className="context__more" id="task-context-more">
          <p className="context__meta">
            <Link to="/tasks">{t('task.tasks')}</Link>
            <span aria-hidden="true">{'/'}</span>
            <span>{task.title}</span>
            <span className="tag">{t('task.difficulty', { level: t(`tasks.difficulty.${task.difficulty}`), time: t(`tasks.time.${task.difficulty}`) })}</span>
          </p>
          {/* Prose written in the content files, a list of facts allowed (CONTENT_FORMAT §6). */}
          <Markdown source={task.context} />
        </div>
      )}
    </section>
  );
}
