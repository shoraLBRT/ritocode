import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { track } from '../../analytics';
import { getTask, getTreatments, listAttempts, recordStep, startAttempt, submitAttempt, useApiClient } from '../../api';
import type { ApiClient, ApiError, TaskDetail, TreatmentTree } from '../../api';
import { ErrorState } from '../../components/ErrorState';
import { LoadingState } from '../../components/LoadingState';
import { Markdown } from '../../components/Markdown';
import { useApiResource } from '../../hooks/useApiResource';
import { useT } from '../../i18n';
import { checkReturnPath, SignInLinks, useCheckRequest, useSession } from '../../session';
import { canCheck, cardsWithoutLeaves, toggleCard, toggleLeaf } from './answer';
import type { Answer } from './answer';
import { clearDraft, draftStorage, restoreDraft, saveDraft } from './draft';
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
 * on it.
 *
 * Anyone can work through both steps; checking needs a signed-in learner (SPEC §4.6). The answer
 * is kept in the browser as it changes (`draft.ts`). *Check* while signed out offers the providers,
 * which return to this task asking for a check; the kept answer is then restored and submitted,
 * and the review opens. If the answer did not survive, the task opens at its start with a note.
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
  const path = `/tasks/${task.slug}`;
  const { requested: checkRequested, consume: consumeCheck } = useCheckRequest(path);

  // Read once: what the browser kept for this task, and whether it was asked to check it.
  const [storage] = useState(draftStorage);
  const [restored] = useState(() => restoreDraft(storage, task, tree));
  const [answerLost] = useState(() => checkRequested && restored === null);

  const [area, setArea] = useState<Area>('context');
  const [step, setStep] = useState<1 | 2>(restored?.step ?? 1);
  const [answer, setAnswer] = useState<Answer>(restored?.answer ?? []);
  const [checking, setChecking] = useState(false);
  const [failure, setFailure] = useState<ApiError | null>(null);
  const [signInPrompt, setSignInPrompt] = useState<{ readonly kept: boolean } | null>(null);
  const attemptId = useAttempt(client, task.slug, session.status === 'signedIn');

  const missing = cardsWithoutLeaves(answer).map((card) => task.cards.find((each) => each.slug === card)?.name ?? card);
  const signedIn = session.status === 'signedIn';
  const signedOut = session.status === 'signedOut';

  useEffect(() => {
    saveDraft(storage, task.slug, { answer, step });
  }, [storage, task.slug, answer, step]);

  // Once per task screen, StrictMode's second effect run included.
  const opened = useRef(false);
  useEffect(() => {
    if (!opened.current) {
      opened.current = true;
      track('task-opened', { task: task.slug, difficulty: task.difficulty });
    }
  }, [task.slug, task.difficulty]);

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
    track('step-2-reached', { task: task.slug });
    setStep(2);
  };

  const submit = useCallback(
    async (attempt: string, picks: Answer) => {
      const submitted = await submitAttempt(client, attempt, picks);
      track('attempt-submitted', { task: task.slug });
      clearDraft(storage, task.slug);
      await navigate(`/tasks/${task.slug}/attempts/${submitted.id}`);
    },
    [client, navigate, storage, task.slug],
  );

  // Back from signing in to check: the kept answer is submitted as it was, once there is an
  // attempt to submit it on. Nothing to submit, or nobody signed in after all, and the request
  // is simply dropped — the answer, if any, waits on the screen.
  const autoChecked = useRef(false);
  useEffect(() => {
    if (!checkRequested || autoChecked.current || session.status === 'loading') {
      return;
    }

    if (restored === null || !canCheck(restored.answer) || !signedIn) {
      autoChecked.current = true;
      consumeCheck();
      return;
    }

    if (attemptId !== null) {
      autoChecked.current = true;
      submit(attemptId, restored.answer).then(consumeCheck, (cause: unknown) => {
        setFailure(cause as ApiError);
        consumeCheck();
      });
    }
  }, [checkRequested, consumeCheck, session.status, signedIn, restored, attemptId, submit]);

  // While that check is on its way, the screen says so as if *Check* had been pressed.
  const autoChecking = checkRequested && restored !== null && canCheck(restored.answer) && session.status !== 'signedOut' && session.status !== 'error';
  const busy = checking || autoChecking;

  const pressCheck = () => {
    if (signedIn && attemptId !== null) {
      setChecking(true);
      setFailure(null);
      submit(attemptId, answer).catch((cause: unknown) => {
        setFailure(cause as ApiError);
        setChecking(false);
      });
      return;
    }

    // Where signed-out visitors drop off is what Umami is for (SPEC §8).
    track('check-signed-out', { task: task.slug });

    // Saved again here, so the prompt can say truthfully whether the answer will survive.
    setSignInPrompt({ kept: saveDraft(storage, task.slug, { answer, step }) });
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
      {answerLost && (
        <p className="notice" role="status">
          {t('task.answerLost')}
        </p>
      )}

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
              {signedOut && signInPrompt === null && <p className="task-screen__hint">{t('task.signInToCheck')}</p>}
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
                  disabled={!canCheck(answer) || busy || !(signedOut || attemptId !== null)}
                  onClick={pressCheck}
                >
                  {busy ? t('task.checking') : t('task.check')}
                </button>
              </div>

              {signedOut && signInPrompt !== null && <SignInPrompt returnPath={checkReturnPath(path)} kept={signInPrompt.kept} />}
            </>
          )}
        </section>
      </div>
    </section>
  );
}

/**
 * Asks a signed-out learner to sign in to check (SPEC §4.6), and says whether their answer will be
 * waiting. Focus moves to it, so a keyboard or screen-reader user lands where the next step is.
 */
function SignInPrompt({ returnPath, kept }: { returnPath: string; kept: boolean }) {
  const t = useT();
  const heading = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    heading.current?.focus();
  }, []);

  return (
    <section className="sign-in-prompt" aria-labelledby="sign-in-prompt-title">
      <h3 id="sign-in-prompt-title" ref={heading} tabIndex={-1}>
        {t('task.signInTitle')}
      </h3>
      <p>{kept ? t('task.signInKept') : t('task.signInNotKept')}</p>
      <SignInLinks returnPath={returnPath} />
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
