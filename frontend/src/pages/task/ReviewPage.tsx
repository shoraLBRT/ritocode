import { Link, useParams } from 'react-router';
import { getAttempt, getProblemCatalogue, getTask, getTreatments, useApiClient } from '../../api';
import type { Attempt, AttemptResult, ProblemCard, TaskDetail, TreatmentTree } from '../../api';
import { CardSectionsView } from '../../components/CardSectionsView';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { Markdown } from '../../components/Markdown';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';

type Line = AttemptResult['cards'][number];

/**
 * `/tasks/{slug}/attempts/{id}` — the review of a checked answer (docs/SPEC.md §4.5), for its owner.
 * The score in points with its composition; every finding of the key, found or missed, with the
 * learner's leaves beside the author's, the author's note and the full card on expanding; every
 * extra pick with its card; the lesson; the same code in another context; and trying again.
 *
 * Found, missed and extra each carry a mark and a word, never colour alone. The note and the lesson
 * are the ones kept with the attempt, so the review always matches the key it was scored against;
 * the cards are the catalogue's, which are general and may be newer.
 */
export function ReviewPage() {
  const { slug = '', id = '' } = useParams();
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource(
    (signal) =>
      Promise.all([
        getAttempt(client, id, signal),
        getTask(client, slug, signal),
        getTreatments(client, signal),
        getProblemCatalogue(client, signal),
      ]),
    [client, id, slug],
  );

  if (state.status === 'loading') {
    return <LoadingState label={t('review.loading')} />;
  }

  if (state.status === 'error') {
    return <ErrorState error={state.error} onRetry={reload} />;
  }

  const [attempt, task, tree, catalogue] = state.data;

  return <Review attempt={attempt} task={task} tree={tree} cards={catalogue.cards} />;
}

function Review({ attempt, task, tree, cards }: { attempt: Attempt; task: TaskDetail; tree: TreatmentTree; cards: readonly ProblemCard[] }) {
  const t = useT();
  const result = attempt.result;

  if (result === null) {
    return null;
  }

  const findings = result.cards.filter((line) => line.outcome !== 'extra');
  const extras = result.cards.filter((line) => line.outcome === 'extra');
  const found = findings.filter((line) => line.outcome === 'found');
  const matched = found.filter((line) => line.treatment?.matched === true);
  const clean = result.maximum === 0;

  const card = (slug: string) => cards.find((each) => each.slug === slug);
  const name = (slug: string) => card(slug)?.name ?? task.cards.find((each) => each.slug === slug)?.name ?? slug;
  const label = (leaf: string) => tree.branches.flatMap((branch) => branch.leaves).find((each) => each.id === leaf)?.label ?? leaf;
  const picked = (slug: string) => attempt.answer?.picks.find((pick) => pick.card === slug)?.leaves ?? [];

  return (
    <section className="page review">
      <h1>{t('review.title')}</h1>
      <p className="page__lead">
        <Link to={`/tasks/${task.slug}`}>{task.title}</Link>
      </p>

      <p className="result__score">{t('review.score', { score: result.total, maximum: result.maximum })}</p>
      {!clean && (
        <p className="page__note">
          {t('review.composition', { found: found.length, present: findings.length, extra: extras.length, matched: matched.length })}
        </p>
      )}
      {clean && <p>{extras.length === 0 ? t('review.cleanNothing') : t('review.cleanExtra')}</p>}
      {!clean && result.isCorrect && <p>{t('review.correct')}</p>}
      {attempt.practice && <p className="page__note">{t('review.practice')}</p>}

      {findings.length > 0 && (
        <>
          <h2>{t('review.findings')}</h2>
          {findings.map((line) => (
            <ReviewLine
              key={line.card}
              line={line}
              name={name(line.card)}
              card={card(line.card)}
              yours={picked(line.card).map(label)}
              authors={(line.keyLeaves ?? []).map(label)}
              note={attempt.review?.notes[line.card]}
            />
          ))}
        </>
      )}

      {extras.length > 0 && (
        <>
          <h2>{t('review.extras')}</h2>
          {extras.map((line) => (
            <ReviewLine key={line.card} line={line} name={name(line.card)} card={card(line.card)} yours={picked(line.card).map(label)} />
          ))}
        </>
      )}

      {attempt.review?.lesson != null && (
        <>
          <h2>{t('review.lesson')}</h2>
          <Markdown source={attempt.review.lesson} />
        </>
      )}

      {task.sameMaterial.length > 0 && (
        <>
          <h2>{t('review.sameMaterial')}</h2>
          <ul>
            {task.sameMaterial.map((other) => (
              <li key={other.slug}>
                <Link to={`/tasks/${other.slug}`}>{other.title}</Link>
              </li>
            ))}
          </ul>
        </>
      )}

      <p>
        <Link className="button button--primary" to={`/tasks/${task.slug}`}>
          {t('review.tryAgain')}
        </Link>
      </p>
    </section>
  );
}

function ReviewLine({
  line,
  name,
  card,
  yours,
  authors,
  note,
}: {
  line: Line;
  name: string;
  card: ProblemCard | undefined;
  yours: readonly string[];
  authors?: readonly string[];
  note?: string | undefined;
}) {
  const t = useT();
  const leaves = (list: readonly string[]) => (list.length === 0 ? t('review.noLeaves') : list.join(', '));

  return (
    <article className={`review-line review-line--${line.outcome}`}>
      <h3 className="review-line__head">
        <span className="review-line__outcome">
          <span aria-hidden="true">{t(`review.${line.outcome}Mark`)}</span> {t(`review.${line.outcome}`)}
        </span>
        <span className="review-line__name">{name}</span>
        <span className="review-line__points">{t('review.points', { count: line.points })}</span>
      </h3>

      <dl className="review-line__leaves">
        {line.outcome !== 'missed' && (
          <>
            <dt>{t('review.yourLeaves')}</dt>
            <dd>{leaves(yours)}</dd>
          </>
        )}
        {authors !== undefined && (
          <>
            <dt>{t('review.authorLeaves')}</dt>
            <dd>{leaves(authors)}</dd>
          </>
        )}
      </dl>

      {note !== undefined && (
        <div className="review-line__note">
          <h4>{t('review.note')}</h4>
          <Markdown source={note} />
        </div>
      )}

      {card !== undefined && (
        <details className="review-line__card">
          <summary>{t('review.fullCard')}</summary>
          <p className="problem-card__summary">{card.summary}</p>
          <CardSectionsView sections={card.sections} />
        </details>
      )}
    </article>
  );
}
