import { useState } from 'react';
import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import { getAttempt, getProblemCatalogue, getTask, getTreatments, useApiClient } from '../../api';
import type { Attempt, AttemptResult, ProblemCard, TaskDetail, TreatmentTree } from '../../api';
import { CardSectionsView } from '../../components/CardSectionsView';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { Markdown } from '../../components/Markdown';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { ContextPanel } from './ContextPanel';
import { MaterialViewer } from './MaterialViewer';
import { SignalControl } from './SignalControl';
import { StepBar } from './StepBar';
import { classTint } from './tint';

type Line = AttemptResult['cards'][number];

/**
 * `/tasks/{slug}/attempts/{id}` — the review of a checked answer (docs/SPEC.md §4.5), for its owner,
 * in the task's workspace with the third step current (docs/DESIGN.md). The sidebar holds the score
 * in points with its composition, every finding of the key, found or missed, every extra pick, the
 * lesson, the same code in another context and trying again. Beside it, the chosen line in full —
 * the learner's leaves beside the author's, the author's note, the whole card, and for an extra
 * pick the signal button (SPEC §4.8) — or the code again.
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

type Area = 'context' | 'code' | 'answer';

interface Leaf {
  readonly label: string;
  readonly right: boolean;
}

function Review({ attempt, task, tree, cards }: { attempt: Attempt; task: TaskDetail; tree: TreatmentTree; cards: readonly ProblemCard[] }) {
  const t = useT();
  const result = attempt.result;
  const [selected, setSelected] = useState<string | null>(null);
  const [pane, setPane] = useState<'card' | 'code'>('card');
  const [area, setArea] = useState<Area>('answer');
  const [contextOpen, setContextOpen] = useState(false);

  if (result === null) {
    return null;
  }

  const findings = result.cards.filter((line) => line.outcome !== 'extra');
  const extras = result.cards.filter((line) => line.outcome === 'extra');
  const found = findings.filter((line) => line.outcome === 'found');
  const matched = found.filter((line) => line.treatment?.matched === true);
  const clean = result.maximum === 0;
  const picks = attempt.answer?.picks ?? [];

  const card = (slug: string) => cards.find((each) => each.slug === slug);
  const name = (slug: string) => card(slug)?.name ?? task.cards.find((each) => each.slug === slug)?.name ?? slug;
  const classOf = (slug: string) => card(slug)?.class ?? task.cards.find((each) => each.slug === slug)?.class;
  const label = (leaf: string) => tree.branches.flatMap((branch) => branch.leaves).find((each) => each.id === leaf)?.label ?? leaf;
  const picked = (slug: string) => picks.find((pick) => pick.card === slug)?.leaves ?? [];
  const chosen = result.cards.find((line) => line.card === selected) ?? result.cards[0];

  const tab = (value: Area, text: string) => (
    <button
      type="button"
      role="tab"
      aria-selected={area === value}
      className="workspace__tab"
      onClick={() => {
        setArea(value);
      }}
    >
      {text}
    </button>
  );

  const lineButton = (line: Line) => (
    <li key={line.card}>
      <button
        type="button"
        className={`verdict verdict--${line.outcome}${line.card === chosen?.card ? ' verdict--current' : ''}`}
        aria-current={line.card === chosen?.card ? 'true' : undefined}
        onClick={() => {
          setSelected(line.card);
          setPane('card');
          setArea('code');
        }}
      >
        <span className="verdict__mark" aria-hidden="true">
          {t(`review.${line.outcome}Mark`)}
        </span>
        <span className="verdict__body">
          <span className="verdict__name">{name(line.card)}</span>
          <span className="verdict__outcome">
            {t(`review.${line.outcome}`)}
            {line.outcome === 'found' && ` · ${line.treatment?.matched === true ? t('review.treatmentMatched') : t('review.treatmentDiffers')}`}
          </span>
        </span>
        <span className="verdict__points">{t('review.points', { count: line.points })}</span>
      </button>
    </li>
  );

  return (
    <section className="workspace review" data-area={area}>
      <StepBar step={3} picked={picks.length} treated={picks.filter((pick) => pick.leaves.length > 0).length} />

      <div className="workspace__tabs" role="tablist" aria-label={t('task.areas')}>
        {tab('context', t('task.contextTab'))}
        {tab('answer', t('review.title'))}
        {tab('code', t('review.cardTab'))}
      </div>

      <div className="workspace__side">
        <div className="workspace__area workspace__area--context">
          <ContextPanel
            task={task}
            open={contextOpen}
            onToggle={() => {
              setContextOpen(!contextOpen);
            }}
          />
        </div>

        <section className="panel workspace__area workspace__area--answer review__summary" aria-labelledby="review-title">
          <h2 id="review-title" className="eyebrow">
            {t('review.title')}
          </h2>
          <p className="review__score">{t('review.score', { score: result.total, maximum: result.maximum })}</p>
          {!clean && (
            <p className="review__composition">
              {t('review.composition', { found: found.length, present: findings.length, extra: extras.length, matched: matched.length })}
            </p>
          )}
          {clean && <p>{extras.length === 0 ? t('review.cleanNothing') : t('review.cleanExtra')}</p>}
          {!clean && result.isCorrect && <p>{t('review.correct')}</p>}
          {attempt.practice && <p className="step__hint step__hint--quiet">{t('review.practice')}</p>}

          {findings.length > 0 && (
            <>
              <h3 className="review__label">{t('review.findings')}</h3>
              <ul className="verdicts">{findings.map(lineButton)}</ul>
            </>
          )}

          {extras.length > 0 && (
            <>
              <h3 className="review__label">{t('review.extras')}</h3>
              <ul className="verdicts">{extras.map(lineButton)}</ul>
            </>
          )}

          {attempt.review?.lesson != null && (
            <div className="review__lesson">
              <h3 className="eyebrow">{t('review.lesson')}</h3>
              <Markdown source={attempt.review.lesson} />
            </div>
          )}

          {task.sameMaterial.length > 0 && (
            <div className="review__more">
              <h3 className="review__label">{t('review.sameMaterial')}</h3>
              <ul>
                {task.sameMaterial.map((other) => (
                  <li key={other.slug}>
                    <Link to={`/tasks/${other.slug}`}>{other.title}</Link>
                  </li>
                ))}
              </ul>
            </div>
          )}

          <p className="review__actions">
            <Link className="button button--primary" to={`/tasks/${task.slug}`}>
              {t('review.tryAgain')}
            </Link>
          </p>
        </section>
      </div>

      <section className="panel workspace__pane workspace__area workspace__area--code" aria-label={t('review.cardTab')}>
        <div className="pane-tabs" role="group" aria-label={t('review.paneTabs')}>
          <button
            type="button"
            className="pane-tab"
            aria-pressed={pane === 'card'}
            onClick={() => {
              setPane('card');
            }}
          >
            {t('review.cardTab')}
          </button>
          <button
            type="button"
            className="pane-tab"
            aria-pressed={pane === 'code'}
            onClick={() => {
              setPane('code');
            }}
          >
            {t('review.codeTab')}
          </button>
        </div>

        {pane === 'code' || chosen === undefined ? (
          <MaterialViewer material={task.material} />
        ) : (
          <FindingCard
            line={chosen}
            name={name(chosen.card)}
            problemClass={classOf(chosen.card)}
            className={task.classes.find((each) => each.id === classOf(chosen.card))?.name}
            card={card(chosen.card)}
            yours={picked(chosen.card).map((leaf) => ({ label: label(leaf), right: chosen.keyLeaves?.includes(leaf) === true }))}
            authors={chosen.keyLeaves?.map((leaf) => ({ label: label(leaf), right: picked(chosen.card).includes(leaf) }))}
            note={attempt.review?.notes[chosen.card]}
          >
            {chosen.outcome === 'extra' && (
              <div className="note note--extra">
                <p>{t('review.extraExplain')}</p>
                <SignalControl attempt={attempt.id} card={chosen.card} sent={attempt.signalledCards.includes(chosen.card)} />
              </div>
            )}
          </FindingCard>
        )}
      </section>
    </section>
  );
}

/** One line of the result in full: the verdict, the learner's leaves beside the author's, the note, the card. */
function FindingCard({
  line,
  name,
  problemClass,
  className,
  card,
  yours,
  authors,
  note,
  children,
}: {
  line: Line;
  name: string;
  problemClass: string | undefined;
  className: string | undefined;
  card: ProblemCard | undefined;
  /** The learner's leaves, each right when the key lists it. */
  yours: readonly Leaf[];
  /** The key's leaves, each right when the learner picked it; any one of them is right. */
  authors: readonly Leaf[] | undefined;
  note: string | undefined;
  children?: ReactNode;
}) {
  const t = useT();
  const chips = (list: readonly Leaf[], wrong: string) =>
    list.length === 0 ? (
      <span className="compare__none">{t('review.noLeaves')}</span>
    ) : (
      list.map((leaf) => (
        <span key={leaf.label} className={`leaf-chip ${leaf.right ? 'leaf-chip--right' : wrong}`}>
          {leaf.label}
        </span>
      ))
    );

  return (
    <article className={`finding-card finding-card--${line.outcome}`} aria-labelledby="finding-card-name">
      <p className="finding-card__badges">
        <span className={`verdict-badge verdict-badge--${line.outcome}`}>
          <span aria-hidden="true">{t(`review.${line.outcome}Mark`)}</span> {t(`review.${line.outcome}`)}
        </span>
        {className !== undefined && (
          <span className={`class-badge${classTint(problemClass)}`}>
            <span className="dot" aria-hidden="true" />
            {className}
          </span>
        )}
        <span className="finding-card__points">{t('review.points', { count: line.points })}</span>
      </p>
      <h3 id="finding-card-name" className="finding-card__name">
        {name}
      </h3>
      {card !== undefined && <p className="finding-card__summary">{card.summary}</p>}

      {children}

      {note !== undefined && (
        <div className="note">
          <h4 className="eyebrow">{t('review.note')}</h4>
          <Markdown source={note} />
        </div>
      )}

      {(line.outcome !== 'missed' || authors !== undefined) && (
        <dl className="compare">
          {line.outcome !== 'missed' && (
            <div>
              <dt className="eyebrow">{t('review.yourLeaves')}</dt>
              <dd className="compare__leaves">{chips(yours, 'leaf-chip--wrong')}</dd>
            </div>
          )}
          {authors !== undefined && (
            <div>
              <dt className="eyebrow">{t('review.authorLeaves')}</dt>
              <dd className="compare__leaves">{chips(authors, '')}</dd>
            </div>
          )}
        </dl>
      )}

      {card !== undefined && (
        <div className="finding-card__sections">
          <CardSectionsView sections={card.sections} />
        </div>
      )}
    </article>
  );
}
