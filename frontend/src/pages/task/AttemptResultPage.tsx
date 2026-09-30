import { Link, useParams } from 'react-router';
import { getAttempt, getTask, useApiClient } from '../../api';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';

/**
 * `/tasks/{slug}/attempts/{id}` — where checking an answer lands. For now the score and a line per
 * card: found, missed, or not in this task's answer, with the points each brought. The review of
 * [#29](https://github.com/shoraLBRT/ritocode/issues/29) replaces it — the learner's leaves beside
 * the author's, notes, the full cards, the lesson, signals, another context and trying again.
 */
export function AttemptResultPage() {
  const { slug = '', id = '' } = useParams();
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource(
    (signal) => Promise.all([getAttempt(client, id, signal), getTask(client, slug, signal)]),
    [client, id, slug],
  );

  if (state.status === 'loading') {
    return <LoadingState label={t('result.loading')} />;
  }

  if (state.status === 'error') {
    return <ErrorState error={state.error} onRetry={reload} />;
  }

  const [attempt, task] = state.data;
  const result = attempt.result;
  const name = (card: string) => task.cards.find((each) => each.slug === card)?.name ?? card;

  return (
    <section className="page">
      <h1>{t('result.title')}</h1>
      <p className="page__lead">{task.title}</p>

      {result !== null && (
        <>
          <p className="result__score">{t('result.score', { score: result.total, maximum: result.maximum })}</p>
          {result.isCorrect && <p>{result.maximum === 0 ? t('result.cleanCorrect') : t('result.correct')}</p>}
          {attempt.practice && <p className="page__note">{t('result.practice')}</p>}

          <ul className="task-list">
            {result.cards.map((line) => (
              <li key={line.card} className="task-list__item">
                <span className="task-list__title">{name(line.card)}</span>
                <span className="task-list__meta">
                  <span className={line.outcome === 'found' ? 'badge badge--done' : 'badge'}>{t(`result.${line.outcome}`)}</span>
                  <span>{t('result.points', { count: line.points })}</span>
                </span>
              </li>
            ))}
          </ul>
        </>
      )}

      <p>
        <Link to={`/tasks/${task.slug}`}>{t('result.backToTask')}</Link>
      </p>
    </section>
  );
}
