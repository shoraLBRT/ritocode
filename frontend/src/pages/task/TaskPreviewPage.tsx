import { Link } from 'react-router';
import type { TaskPreview } from '../../api';
import { Markdown } from '../../components/Markdown';
import { useT } from '../../i18n';

/**
 * What the build prerenders at `/tasks/{slug}` for every task of the content export (docs/SPEC.md
 * §4.1, #301): the title, the difficulty, the brief and the context — what the task screen shows
 * before the learner answers, and nothing more. It takes a `TaskPreview`, which has no field for the
 * answer key, the notes, a card weight or the cards to pick from, so none can reach the page. Once
 * the bundle loads, the task screen itself renders over it.
 */
export function TaskPreviewPage({ task }: { task: TaskPreview }) {
  const t = useT();

  return (
    <section className="page rc-task-preview">
      <p className="context__meta">
        <Link to="/tasks">{t('task.tasks')}</Link>
        <span aria-hidden="true">{'/'}</span>
        <span>{task.title}</span>
      </p>
      <article className="panel context" aria-labelledby="task-title">
        <h1 id="task-title" className="context__title">
          {task.title}
        </h1>
        <span className="tag context__difficulty">{t('task.difficulty', { level: t(`tasks.difficulty.${task.difficulty}`), time: t(`tasks.time.${task.difficulty}`) })}</span>

        <div className="context__brief">
          <span className="eyebrow">{t('task.brief')}</span>
          <Markdown source={task.brief} />
        </div>

        <div className="context__more">
          <span className="eyebrow">{t('task.context')}</span>
          <Markdown source={task.context} />
        </div>

        <noscript>
          <p className="rc-task-preview__note">{t('task.noScript')}</p>
        </noscript>
      </article>
    </section>
  );
}
