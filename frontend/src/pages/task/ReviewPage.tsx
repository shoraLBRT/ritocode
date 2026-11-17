import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import { getAttempt, getProblemCatalogue, getTask, getTreatments, listTasks, useApiClient } from '../../api';
import type { Attempt, AttemptOutcome, AttemptResult, ProblemCard, TaskDetail, TaskSummary, TreatmentTree } from '../../api';
import { CardSectionsView } from '../../components/CardSectionsView';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { Markdown } from '../../components/Markdown';
import { useApiResource } from '../../hooks/useApiResource';
import { useCountUp } from '../../hooks/useCountUp';
import { useT } from '../../i18n';
import { CardDialog } from './CardDialog';
import { ContextPanel } from './ContextPanel';
import { MaterialViewer } from './MaterialViewer';
import { nextTask } from './next';
import { OutcomeTally, RevealAnswer } from './OutcomeTally';
import { Icon } from './parts';
import { lanes } from './lanes';
import { ReviewLanes } from './ReviewLanes';
import { SignalControl } from './SignalControl';
import { StepBar } from './StepBar';

type Line = AttemptResult['cards'][number];

/**
 * `/tasks/{slug}/attempts/{id}` — the review of a checked answer (docs/SPEC.md §4.5), for its owner,
 * in the task's frame with the third step current (docs/DESIGN.md › The task workspace).
 *
 * **Until the learner reveals the answer** (ADR 0017) it is the outcome only: the score, whether the
 * task is solved, and the counts drawn as small diagrams — never which card was right, missed or
 * extra, and neither the lesson nor the same code in another context; the API does not send them.
 * *Попробовать снова* leads; *Показать ответ* asks first, saying what it does to the task's status.
 * *Следующая задача* sits beside trying again, before the reveal and after it (#199).
 *
 * **Once revealed**: at the top of the centre, the score in points with its composition and trying
 * again; under it, the task's cards in three lanes — found, missed, not in the answer — each with
 * its treatment leaves marked on its face (#242, `ReviewLanes`), or the code again. *Подробнее*
 * opens a card in full in their place — the learner's leaves beside the author's, the author's note,
 * the whole card, and for an extra pick the signal button (SPEC §4.8). On the right, the counts and
 * the lesson; on the left, the context and the same code in another context.
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

  return <Review key={attempt.id} initial={attempt} task={task} tree={tree} cards={catalogue.cards} />;
}

type Area = 'context' | 'code' | 'answer';

interface Leaf {
  readonly label: string;
  readonly right: boolean;
}

function Review({ initial, task, tree, cards }: { initial: Attempt; task: TaskDetail; tree: TreatmentTree; cards: readonly ProblemCard[] }) {
  // Revealing answers the attempt in full; it takes the place of the one read on opening.
  const [attempt, setAttempt] = useState(initial);
  // Revealed on this page rather than opened revealed: the confirmation that held the focus is gone,
  // so the full review's score takes it.
  const [revealedHere, setRevealedHere] = useState(false);
  const following = useNextTask(task.slug);
  const next = <NextTaskLink next={following} />;

  if (attempt.outcome === null) {
    return null;
  }

  if (attempt.result === null) {
    return (
      <HiddenReview
        attempt={attempt}
        outcome={attempt.outcome}
        task={task}
        next={next}
        onRevealed={(revealed) => {
          setRevealedHere(true);
          setAttempt(revealed);
        }}
      />
    );
  }

  return (
    <FullReview
      attempt={attempt}
      outcome={attempt.outcome}
      result={attempt.result}
      task={task}
      tree={tree}
      cards={cards}
      next={next}
      focusScore={revealedHere}
    />
  );
}

/**
 * The next unsolved task in the order of the `/tasks` list (`nextTask`), read from the list itself so
 * the order and the statuses are the list's. The review does not wait for it: until the list
 * arrives, or if it fails, there is no link, and the header's *Задачи* reaches the list.
 */
function useNextTask(current: string): TaskSummary | undefined {
  const client = useApiClient();
  // The catalogue is twenty tasks, read in one page as `/tasks` reads it.
  const { state } = useApiResource((signal) => listTasks(client, { pageSize: 100 }, signal), [client]);
  return state.status === 'success' ? nextTask(state.data.items, current) : undefined;
}

/** *Следующая задача →*, a plain button beside *Попробовать снова*; nothing when no task is left to solve. */
function NextTaskLink({ next }: { next: TaskSummary | undefined }) {
  const t = useT();

  if (next === undefined) {
    return null;
  }

  return (
    <Link className="button" to={`/tasks/${next.slug}`}>
      {t('review.nextTask')}
    </Link>
  );
}

/** The workspace's tabs at phone width: the context, the review, and the code or the chosen card. */
function useAreaTabs() {
  const [area, setArea] = useState<Area>('answer');

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

  return { area, setArea, tab };
}

/**
 * The review before the answer is revealed: the score and whether the task is solved at the top of
 * the centre, *Попробовать снова* first and *Показать ответ* beside it; the code under it, to read
 * again; the counts as diagrams on the right.
 */
function HiddenReview({
  attempt,
  outcome,
  task,
  next,
  onRevealed,
}: {
  attempt: Attempt;
  outcome: AttemptOutcome;
  task: TaskDetail;
  next: ReactNode;
  onRevealed: (attempt: Attempt) => void;
}) {
  const t = useT();
  const { area, tab } = useAreaTabs();
  const [contextOpen, setContextOpen] = useState(false);
  // Every pick has a leaf before it can be checked, so the cards picked are the cards treated.
  const picked = outcome.found + outcome.extra;
  const score = useCountUp(outcome.total);

  return (
    <section className="workspace workspace--review review review--hidden" data-area={area}>
      <StepBar step={3} picked={picked} treated={picked} />

      <div className="workspace__tabs" role="tablist" aria-label={t('task.areas')}>
        {tab('context', t('task.contextTab'))}
        {tab('answer', t('review.title'))}
        {tab('code', t('review.codeTab'))}
      </div>

      <div className="workspace__context workspace__area workspace__area--context">
        <ContextPanel
          task={task}
          open={contextOpen}
          onToggle={() => {
            setContextOpen(!contextOpen);
          }}
        />
      </div>

      <section className="panel review__score-bar workspace__area workspace__area--answer" aria-labelledby="review-title">
        <div className="review__score-text">
          <h2 id="review-title" className="eyebrow">
            {t('review.title')}
          </h2>
          <p className="review__score">{t('review.score', { score, maximum: outcome.maximum })}</p>
          {outcome.solved ? <Solved /> : <p className="review__verdict-line">{t('review.notSolved')}</p>}
          {attempt.practice && <p className="step__hint step__hint--quiet">{t('review.practice')}</p>}
        </div>
        <RevealAnswer attempt={attempt} task={task.slug} next={next} onRevealed={onRevealed} />
      </section>

      <section className="panel workspace__code review__pane workspace__area workspace__area--code" aria-label={t('review.codeTab')}>
        <MaterialViewer material={task.material} />
      </section>

      <aside className="panel workspace__aside review__verdicts workspace__area workspace__area--answer" aria-labelledby="review-tally">
        <h3 id="review-tally" className="aside__title">
          {t('review.tally.title')}
        </h3>
        <OutcomeTally outcome={outcome} />
        <p className="review__hidden-hint">{t('review.hiddenHint')}</p>
      </aside>
    </section>
  );
}

/** The review once the answer is revealed (SPEC §4.5): every line of the result, the leaves, the notes, the lesson. */
function FullReview({
  attempt,
  outcome,
  result,
  task,
  tree,
  cards,
  next,
  focusScore,
}: {
  attempt: Attempt;
  outcome: AttemptOutcome;
  result: AttemptResult;
  task: TaskDetail;
  tree: TreatmentTree;
  cards: readonly ProblemCard[];
  next: ReactNode;
  focusScore: boolean;
}) {
  const t = useT();
  const scoreBar = useRef<HTMLElement>(null);
  const score = useCountUp(result.total);

  useEffect(() => {
    if (focusScore) {
      scoreBar.current?.focus();
    }
  }, [focusScore]);
  // The card opened over the centre, or none; the one closed last takes the focus back.
  const [selected, setSelected] = useState<string | null>(null);
  const [closed, setClosed] = useState<string | null>(null);
  const [pane, setPane] = useState<'cards' | 'code'>('cards');
  const { area, tab } = useAreaTabs();
  const [contextOpen, setContextOpen] = useState(false);

  useEffect(() => {
    if (selected === null && closed !== null) {
      document.getElementById(`open-${closed}`)?.focus();
    }
  }, [selected, closed]);

  const extras = result.cards.filter((line) => line.outcome === 'extra');
  const clean = result.maximum === 0;
  const picks = attempt.answer?.picks ?? [];

  const card = (slug: string) => cards.find((each) => each.slug === slug);
  const name = (slug: string) => card(slug)?.name ?? task.cards.find((each) => each.slug === slug)?.name ?? slug;
  const classOf = (slug: string) => card(slug)?.class ?? task.cards.find((each) => each.slug === slug)?.class;
  const className = (slug: string) => task.classes.find((each) => each.id === classOf(slug))?.name;
  const label = (leaf: string) => tree.branches.flatMap((branch) => branch.leaves).find((each) => each.id === leaf)?.label ?? leaf;
  const picked = (slug: string) => picks.find((pick) => pick.card === slug)?.leaves ?? [];
  // The opened card walks the lanes' cards in their order: found, missed, not in the answer.
  const walk = lanes(result.cards).flatMap((each) => each.lines);
  const chosenIndex = walk.findIndex((line) => line.card === selected);
  const chosen = walk[chosenIndex];

  return (
    <section className="workspace workspace--review review" data-area={area}>
      <StepBar step={3} picked={picks.length} treated={picks.filter((pick) => pick.leaves.length > 0).length} />

      <div className="workspace__tabs" role="tablist" aria-label={t('task.areas')}>
        {tab('context', t('task.contextTab'))}
        {tab('answer', t('review.title'))}
        {tab('code', t('review.cardTab'))}
      </div>

      <div className="workspace__context workspace__area workspace__area--context">
        <ContextPanel
          task={task}
          open={contextOpen}
          onToggle={() => {
            setContextOpen(!contextOpen);
          }}
        >
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
        </ContextPanel>
      </div>

      {/* The score at the top of the centre, with the way to try again and to the next task. */}
      <section ref={scoreBar} tabIndex={-1} className="panel review__score-bar workspace__area workspace__area--answer" aria-labelledby="review-title">
        <div className="review__score-text">
          <h2 id="review-title" className="eyebrow">
            {t('review.title')}
          </h2>
          <p className="review__score">{t('review.score', { score, maximum: result.maximum })}</p>
          {!clean && (
            <p className="review__composition">
              {t('review.composition', { found: outcome.found, present: outcome.present, extra: outcome.extra, matched: outcome.treated })}
            </p>
          )}
          {clean && <p className="review__verdict-line">{extras.length === 0 ? t('review.cleanNothing') : t('review.cleanExtra')}</p>}
          {!clean && outcome.solved && <Solved />}
          {attempt.practice && <p className="step__hint step__hint--quiet">{t('review.practice')}</p>}
        </div>
        <p className="review__actions">
          <Link className="button button--primary" to={`/tasks/${task.slug}`}>
            {t('review.tryAgain')}
          </Link>
          {next}
        </p>
      </section>

      <section
        className={`panel workspace__code review__pane workspace__area workspace__area--code${pane === 'code' || result.cards.length === 0 ? '' : ' review__pane--cards'}`}
        aria-label={t('review.cardTab')}
      >
        <div className="pane-tabs" role="group" aria-label={t('review.paneTabs')}>
          <button
            type="button"
            className="pane-tab"
            aria-pressed={pane === 'cards'}
            onClick={() => {
              setPane('cards');
            }}
          >
            {t('review.cardsCount', { count: result.cards.length })}
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

        {pane === 'code' || result.cards.length === 0 ? (
          <MaterialViewer material={task.material} />
        ) : (
          <ReviewLanes
            lines={result.cards}
            name={name}
            picked={picked}
            label={label}
            onOpen={(slug) => {
              setClosed(null);
              setSelected(slug);
            }}
          />
        )}
      </section>

      {chosen !== undefined && (
        <CardDialog
          className="review-card"
          labelledBy="review-card-name"
          band={<span>{className(chosen.card)}</span>}
          heading={
            <h3 id="review-card-name" className="rc-pcard__name">
              {name(chosen.card)}
            </h3>
          }
          index={chosenIndex}
          count={walk.length}
          onMove={(index) => {
            const next = walk[(index + walk.length) % walk.length];
            if (next !== undefined) {
              setSelected(next.card);
            }
          }}
          onClose={() => {
            setClosed(chosen.card);
            setSelected(null);
          }}
        >
          <FindingCard
            line={chosen}
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
        </CardDialog>
      )}

      <aside className="panel workspace__aside review__verdicts workspace__area workspace__area--answer" aria-labelledby="review-counts">
        <h3 id="review-counts" className="aside__title">
          {t('review.tally.title')}
        </h3>
        <OutcomeTally outcome={outcome} />

        {attempt.review?.lesson != null && (
          <div className="review__lesson">
            <h3 className="eyebrow">{t('review.lesson')}</h3>
            <Markdown source={attempt.review.lesson} />
          </div>
        )}
      </aside>
    </section>
  );
}

/** The quiet mark of a solved task: a check that pops once, with a soft ring behind it (workspace.css). */
function Solved() {
  const t = useT();

  return (
    <p className="review__verdict-line review__solved">
      <span className="review__solved-badge" aria-hidden="true">
        <Icon name="check" />
      </span>
      {t('review.solved')}
    </p>
  );
}

/**
 * What an opened card shows on the review (#258), inside the task screen's `CardDialog`: the verdict
 * and the points; then *В этой задаче* — for an extra pick the explanation and the signal, the
 * author's note, the learner's leaves beside the author's; then the summary; then the catalogue's
 * sections, folded, as reference.
 */
function FindingCard({
  line,
  card,
  yours,
  authors,
  note,
  children,
}: {
  line: Line;
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
    <div className={`finding-card finding-card--${line.outcome}`}>
      <p className="finding-card__badges">
        <span className={`verdict-badge verdict-badge--${line.outcome}`}>
          <span aria-hidden="true">{t(`review.${line.outcome}Mark`)}</span> {t(`review.${line.outcome}`)}
        </span>
        <span className="finding-card__points">{t('review.points', { count: line.points })}</span>
      </p>

      <section className="finding-card__task" aria-labelledby="finding-card-task">
        <h4 id="finding-card-task" className="eyebrow">
          {t('review.inThisTask')}
        </h4>
        {children}

        {note !== undefined && (
          <div className="note">
            <h5 className="eyebrow">{t('review.note')}</h5>
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
      </section>

      {card !== undefined && (
        <>
          <p className="finding-card__summary">{card.summary}</p>
          <div className="finding-card__sections">
            <CardSectionsView sections={card.sections} folded />
          </div>
        </>
      )}
    </div>
  );
}
