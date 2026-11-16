import { useLayoutEffect, useRef } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';
import { useT } from '../../i18n';
import { Icon } from './parts';

const focusable = 'button:not(:disabled), a[href], summary, [tabindex]:not([tabindex="-1"])';

/**
 * A card opened over the code on the task screen (docs/DESIGN.md › The task workspace, #258): one
 * shell for *Диагноз* and the review, so a card opens, walks and closes the same way on every step —
 * only what it shows differs (SPEC §3.2). The code behind is dimmed; ×, Esc or a click on the dimmed
 * code closes it; ← → and ‹ n из N › walk the cards; Tab stays inside it; focus starts on ×.
 */
export function CardDialog({
  className,
  labelledBy,
  heading,
  band,
  index,
  count,
  onMove,
  onClose,
  actions,
  children,
}: {
  /** Extra classes on the dialog (`rc-pcard--peek` is always there). */
  className: string;
  labelledBy: string;
  heading: ReactNode;
  band: ReactNode;
  index: number;
  count: number;
  onMove: (index: number) => void;
  onClose: () => void;
  /** What sits at the end of the walk row: *Отметить* in *Диагноз*, nothing on the review. */
  actions?: ReactNode;
  children: ReactNode;
}) {
  const t = useT();
  const dialog = useRef<HTMLDivElement>(null);
  const closeButton = useRef<HTMLButtonElement>(null);

  useLayoutEffect(() => {
    closeButton.current?.focus();
  }, []);

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }
    if ((event.key === 'ArrowLeft' || event.key === 'ArrowRight') && count > 1) {
      event.preventDefault();
      onMove(index + (event.key === 'ArrowRight' ? 1 : -1));
      return;
    }
    if (event.key !== 'Tab' || dialog.current === null) {
      return;
    }
    const all = [...dialog.current.querySelectorAll<HTMLElement>(focusable)];
    const first = all[0];
    const last = all[all.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  };

  return (
    <div className="dock-open" onKeyDown={onKeyDown}>
      <button type="button" className="dock-open__dim" tabIndex={-1} aria-label={t('problems.close')} onClick={onClose} />
      <div ref={dialog} className={`rc-pcard rc-pcard--peek ${className}`} role="dialog" aria-modal="true" aria-labelledby={labelledBy}>
        <div className="rc-pcard__band">{band}</div>
        <div className="rc-pcard__face">
          <div className="rc-pcard__open-head">
            {heading}
            <button ref={closeButton} type="button" className="icon-button" aria-label={t('problems.close')} onClick={onClose}>
              <Icon name="close" />
            </button>
          </div>
          <span className="rc-pcard__rule" aria-hidden="true" />
          {children}
          {(count > 1 || actions !== undefined) && (
            <div className="rc-pcard__walk">
              {count > 1 && (
                <>
                  <button
                    type="button"
                    className="nav-button"
                    aria-label={t('task.previousCard')}
                    onClick={() => {
                      onMove(index - 1);
                    }}
                  >
                    <Icon name="left" />
                  </button>
                  <span className="rc-pcard__position">{t('task.cardPosition', { index: index + 1, count })}</span>
                  <button
                    type="button"
                    className="nav-button"
                    aria-label={t('task.nextCard')}
                    onClick={() => {
                      onMove(index + 1);
                    }}
                  >
                    <Icon name="right" />
                  </button>
                </>
              )}
              {actions}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
