import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { revealAnswer, useApiClient } from '../../api';
import type { Attempt, AttemptOutcome } from '../../api';
import { useT } from '../../i18n';

/** More pips than this and a row says its number only: the drawing would stop being countable. */
const MAX_PIPS = 12;

type PipKind = 'found' | 'missed' | 'treated' | 'untreated' | 'extra';

/**
 * The outcome of an answer drawn small, in the arena's manner (docs/DESIGN.md › The review): a ring
 * of the findings found out of those present, then a row each — found, treated right, missed, extra
 * — with its number in words and a pip per finding or pick. The pips are always in the same order,
 * found before missed, so they say how many, never which. The drawing is hidden from a screen
 * reader; the words carry it.
 */
export function OutcomeTally({ outcome }: { outcome: AttemptOutcome }) {
  const t = useT();
  const share = outcome.present === 0 ? 0 : Math.round((outcome.found / outcome.present) * 100);

  return (
    <div className="rc-tally">
      <div className="rc-tally__ring" style={{ ['--p' as string]: share }} aria-hidden="true">
        <span className="rc-tally__hole">
          <span className="rc-tally__ring-value">{outcome.found}</span>
          <span className="rc-tally__ring-of">{t('review.tally.ofPresent', { count: outcome.present })}</span>
        </span>
      </div>

      <dl className="rc-tally__rows">
        <Row
          label={t('review.tally.found')}
          value={t('review.tally.of', { count: outcome.found, total: outcome.present })}
          pips={[...pips('found', outcome.found), ...pips('missed', outcome.missed)]}
        />
        <Row
          label={t('review.tally.treated')}
          value={t('review.tally.of', { count: outcome.treated, total: outcome.found })}
          pips={[...pips('treated', outcome.treated), ...pips('untreated', outcome.found - outcome.treated)]}
        />
        <Row label={t('review.tally.missed')} value={String(outcome.missed)} pips={pips('missed', outcome.missed)} />
        <Row
          label={t('review.tally.extra')}
          value={outcome.extra === 0 ? t('review.tally.none') : String(outcome.extra)}
          pips={pips('extra', outcome.extra)}
        />
      </dl>
    </div>
  );
}

function pips(kind: PipKind, count: number): PipKind[] {
  return Array.from({ length: Math.max(0, count) }, () => kind);
}

function Row({ label, value, pips: kinds }: { label: string; value: string; pips: readonly PipKind[] }) {
  return (
    <div className="rc-tally__row">
      <dt className="rc-tally__label">{label}</dt>
      <dd className="rc-tally__value">{value}</dd>
      {kinds.length > 0 && kinds.length <= MAX_PIPS && (
        <dd className="rc-tally__pips" aria-hidden="true">
          {kinds.map((kind, index) => (
            <span key={index} className={`rc-tally__pip rc-tally__pip--${kind}`} />
          ))}
        </dd>
      )}
    </div>
  );
}

type Stage = 'closed' | 'asking' | 'revealing' | 'failed';

/**
 * *Попробовать снова* first, then `next` (the way to the next task, #199), and *Показать ответ*
 * last, which asks before it acts (ADR 0017): the confirmation says what revealing does to the
 * task's status — gave up, unless the task is solved already, in which case it stays solved — and
 * that trying again stays open as practice.
 */
export function RevealAnswer({
  attempt,
  task,
  next,
  onRevealed,
}: {
  attempt: Attempt;
  task: string;
  next?: ReactNode;
  onRevealed: (attempt: Attempt) => void;
}) {
  const client = useApiClient();
  const t = useT();
  const [stage, setStage] = useState<Stage>('closed');
  const question = useRef<HTMLDivElement>(null);
  const opener = useRef<HTMLButtonElement>(null);
  // Set by *Не сейчас*: the button that was pressed leaves the page, so focus goes back to the one
  // that opened the question rather than falling to the page's start.
  const returnFocus = useRef(false);

  useEffect(() => {
    if (stage === 'asking') {
      question.current?.focus();
    } else if (stage === 'closed' && returnFocus.current) {
      returnFocus.current = false;
      opener.current?.focus();
    }
  }, [stage]);

  const reveal = async () => {
    setStage('revealing');

    try {
      onRevealed(await revealAnswer(client, attempt.id));
    } catch {
      setStage('failed');
    }
  };

  return (
    <>
      <p className="review__actions">
        <Link className="button button--primary" to={`/tasks/${task}`}>
          {t('review.tryAgain')}
        </Link>
        {next}
        {stage === 'closed' && (
          <button
            ref={opener}
            type="button"
            className="button"
            onClick={() => {
              setStage('asking');
            }}
          >
            {t('review.reveal')}
          </button>
        )}
      </p>

      {stage !== 'closed' && (
        <div className="review__reveal" role="group" aria-labelledby="reveal-question" ref={question} tabIndex={-1}>
          <p id="reveal-question" className="review__reveal-question">
            {t('review.revealQuestion')}
          </p>
          <p className="review__reveal-text">{attempt.taskStatus === 'solved' ? t('review.revealKeepsSolved') : t('review.revealGivesUp')}</p>
          {stage === 'failed' && (
            <p className="review__reveal-error" role="alert">
              {t('review.revealFailed')}
            </p>
          )}
          <p className="review__reveal-actions">
            <button
              type="button"
              className="button button--primary"
              disabled={stage === 'revealing'}
              onClick={() => {
                void reveal();
              }}
            >
              {stage === 'revealing' ? t('review.revealing') : t('review.revealConfirm')}
            </button>
            <button
              type="button"
              className="button"
              disabled={stage === 'revealing'}
              onClick={() => {
                returnFocus.current = true;
                setStage('closed');
              }}
            >
              {t('review.revealCancel')}
            </button>
          </p>
        </div>
      )}
    </>
  );
}
