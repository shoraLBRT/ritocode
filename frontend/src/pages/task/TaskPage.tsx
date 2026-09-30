import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { getTask, getTreatments, listAttempts, recordStep, startAttempt, submitAttempt, useApiClient } from '../../api';
import type { ApiClient, ApiError, TaskDetail, TreatmentTree } from '../../api';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { Markdown } from '../../components/Markdown';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { useSession } from '../../session';
import { canCheck, cardsWithoutLeaves, toggleCard, toggleLeaf } from './answer';
import type { Answer } from './answer';
import { DiagnosisStep } from './DiagnosisStep';
import { MaterialViewer } from './MaterialViewer';
import { TreatmentStep } from './TreatmentStep';

type Area = 'context' | 'code' | 'answer';

/**
 * `/tasks/{slug}` — solving a task (docs/SPEC.md §4.4). Three areas — context and brief, material,
 * answer — side by side on a desktop and three tabs at phone width. The answer is two steps: pick
 * the cards you see, then the treatment for each.
 *
 * It reads the task and the treatment tree, never a card's full text. A signed-in learner works in
 * an attempt: the newest open one at this task, or a new one, and the steps reached are recorded
 * on it. Checking needs a signed-in learner; the flow that signs a visitor in and brings them back
 * is #127.
 */
export function TaskPage() {
  const { slug = '' } = useParams();
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource(
    (signal) => Promise.all([getTask(client, slug, signal), getTreatments(client, signal)]),
    [client, slug],
  );

  if (state.status === 'loading') {
    return <LoadingState label={t('task.loading')} />;
  }

  if (state.status === 'error') {
    return <ErrorState error={state.error} onRetry={reload} />;
  }

  const [task, tree] = state.data;

  return <TaskScreen key={task.slug} task={task} tree={tree} />;
}

function TaskScreen({ task, tree }: { task: TaskDetail; tree: TreatmentTree }) {
  const t = useT();
  const client = useApiClient();
  const session = useSession();
  const navigate = useNavigate();

  const [area, setArea] = useState<Area>('context');
  const [step, setStep] = useState<1 | 2>(1);
  const [answer, setAnswer] = useState<Answer>([]);
  const [checking, setChecking] = useState(false);
  const [failure, setFailure] = useState<ApiError | null>(null);
  const attemptId = useAttempt(client, task.slug, session.status === 'signedIn');

  const missing = cardsWithoutLeaves(answer).map((card) => task.cards.find((each) => each.slug === card)?.name ?? card);
  const signedIn = session.status === 'signedIn';

  // Step 2 reached goes into the journal (SPEC §8) once there is an attempt to record it on — the
  // learner may get there before the attempt has been found or started. A failure to record it
  // must not stop them.
  const recorded = useRef(false);
  useEffect(() => {
    if (step === 2 && attemptId !== null && !recorded.current) {
      recorded.current = true;
      recordStep(client, attemptId, 'treatment').catch(() => undefined);
    }
  }, [client, step, attemptId]);

  const toStep2 = () => {
    setStep(2);
  };

  const check = async () => {
    if (attemptId === null) {
      return;
    }

    setChecking(true);
    setFailure(null);

    try {
      const attempt = await submitAttempt(client, attemptId, answer);
      await navigate(`/tasks/${task.slug}/attempts/${attempt.id}`);
    } catch (cause) {
      setFailure(cause as ApiError);
      setChecking(false);
    }
  };

  const tab = (value: Area, label: string) => (
    <button
      type="button"
      role="tab"
      aria-selected={area === value}
      className="task-tabs__tab"
      onClick={() => {
        setArea(value);
      }}
    >
      {label}
    </button>
  );

  return (
    <section className="task-screen">
      <h1 className="task-screen__title">{task.title}</h1>

      <div className="task-tabs" role="tablist" aria-label={t('task.areas')}>
        {tab('context', t('task.contextTab'))}
        {tab('code', t('task.codeTab'))}
        {tab('answer', t('task.answerTab'))}
      </div>

      <div className="task-screen__areas">
        <section className={areaClass('context', area)} aria-labelledby="task-context">
          <h2 id="task-context">{t('task.context')}</h2>
          {/* Prose written in the content files, a list of facts allowed (CONTENT_FORMAT §6). */}
          <Markdown source={task.context} />
          <h2>{t('task.brief')}</h2>
          <blockquote className="task-screen__brief">
            <Markdown source={task.brief} />
          </blockquote>
        </section>

        <section className={areaClass('code', area)} aria-labelledby="task-material">
          <h2 id="task-material">{t('task.material')}</h2>
          <MaterialViewer material={task.material} />
        </section>

        <section className={areaClass('answer', area)} aria-labelledby="task-answer">
          {step === 1 ? (
            <>
              <h2 id="task-answer">{t('task.step1')}</h2>
              <DiagnosisStep
                classes={task.classes}
                cards={task.cards}
                answer={answer}
                onToggle={(card) => {
                  setAnswer((current) => toggleCard(current, card));
                }}
              />
              <div className="task-screen__actions">
                <span className="page__note">{t('task.picked', { count: answer.length })}</span>
                <button type="button" className="button button--primary" onClick={toStep2}>
                  {t('task.toStep2')}
                </button>
              </div>
            </>
          ) : (
            <>
              <h2 id="task-answer">{t('task.step2')}</h2>
              <TreatmentStep
                cards={task.cards}
                tree={tree}
                answer={answer}
                onToggleLeaf={(card, leaf) => {
                  setAnswer((current) => toggleLeaf(current, card, leaf));
                }}
              />

              {missing.length > 0 && <p className="task-screen__hint">{t('task.needsLeaf', { cards: missing.join(', ') })}</p>}
              {!signedIn && <p className="task-screen__hint">{t('task.signInToCheck')}</p>}
              {failure !== null && <ErrorState error={failure} />}

              <div className="task-screen__actions">
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setStep(1);
                  }}
                >
                  {t('task.backToStep1')}
                </button>
                <button
                  type="button"
                  className="button button--primary"
                  disabled={!canCheck(answer) || !signedIn || attemptId === null || checking}
                  onClick={() => {
                    void check();
                  }}
                >
                  {checking ? t('task.checking') : t('task.check')}
                </button>
              </div>
            </>
          )}
        </section>
      </div>
    </section>
  );
}

/**
 * The attempt a signed-in learner works in: the newest open one at this task if there is one, so
 * opening a task twice does not leave attempts behind, and a new one otherwise. `null` while there
 * is none — signed out, still asking, or the API refused.
 */
function useAttempt(client: ApiClient, task: string, signedIn: boolean): string | null {
  const [attemptId, setAttemptId] = useState<string | null>(null);

  useEffect(() => {
    if (!signedIn) {
      return;
    }

    let cancelled = false;

    const find = async () => {
      const latest = await listAttempts(client, { task, pageSize: 1 });
      const open = latest.items.find((attempt) => attempt.submittedAt === null);
      return open?.id ?? (await startAttempt(client, task)).id;
    };

    find().then(
      (id) => {
        if (!cancelled) {
          setAttemptId(id);
        }
      },
      () => undefined,
    );

    return () => {
      cancelled = true;
    };
  }, [client, task, signedIn]);

  return attemptId;
}

function areaClass(value: Area, active: Area): string {
  return value === active ? 'task-screen__area task-screen__area--active' : 'task-screen__area';
}
