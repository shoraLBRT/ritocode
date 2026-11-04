import { use } from 'react';
import { createPortal } from 'react-dom';
import { HeaderSlotContext } from '../../components/HeaderSlot';
import { useT } from '../../i18n';

/**
 * The three steps — diagnosis, treatment, review — at the centre of the header (docs/DESIGN.md),
 * with what has been done in each. A step behind the current one can be gone back to while the
 * answer is still being worked on; once it is checked, none can. Outside the application's frame —
 * in a test that mounts the screen alone — the bar renders where it stands.
 */
export function StepBar({
  step,
  picked,
  treated,
  onStep,
}: {
  step: 1 | 2 | 3;
  picked: number;
  treated?: number;
  onStep?: (step: 1 | 2) => void;
}) {
  const t = useT();
  const slot = use(HeaderSlotContext);
  const names = [t('task.stepNames.diagnosis'), t('task.stepNames.treatment'), t('task.stepNames.review')];

  const bar = (
    <nav className="steps" aria-label={t('task.steps')}>
      <ol className="steps__list">
        {names.map((name, index) => {
          const number = (index + 1) as 1 | 2 | 3;
          const done = number < step;
          const back = done && step < 3 && onStep !== undefined;
          const badge = number === 1 && picked > 0 ? String(picked) : number === 2 && step >= 2 && picked > 0 && treated !== undefined ? `${String(treated)}/${String(picked)}` : null;

          return (
            <li key={name} className="steps__item">
              <button
                type="button"
                className={`step-pill${number === step ? ' step-pill--current' : ''}${done ? ' step-pill--done' : ''}`}
                aria-current={number === step ? 'step' : undefined}
                disabled={!back}
                onClick={() => {
                  if (back && number < 3) {
                    onStep(number as 1 | 2);
                  }
                }}
              >
                <span className="step-pill__number" aria-hidden="true">
                  {done ? '✓' : number}
                </span>
                {name}
                {badge !== null && <span className="step-pill__badge">{badge}</span>}
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );

  return slot === null ? bar : createPortal(bar, slot);
}
