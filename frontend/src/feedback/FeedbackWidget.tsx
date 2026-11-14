import { useEffect, useId, useRef, useState } from 'react';
import { useLocation } from 'react-router';
import { isApiError, sendFeedback, useApiClient } from '../api';
import { useT } from '../i18n';
import type { MessageKey } from '../i18n';
import { mountTurnstile, resolveTurnstileSiteKey } from './turnstile';
import type { TurnstileWidget } from './turnstile';

/** The longest message the API accepts by default (`Feedback:MaxLength`). */
export const FEEDBACK_MAX_LENGTH = 4000;

type Outcome = 'sent' | 'empty' | 'tooLong' | 'rateLimited' | 'notVerified' | 'unavailable' | 'failed';

/** The word the form shows for each outcome. */
const outcomeText: Readonly<Record<Outcome, MessageKey>> = {
  sent: 'feedback.sent',
  empty: 'feedback.empty',
  tooLong: 'feedback.tooLong',
  rateLimited: 'feedback.rateLimited',
  notVerified: 'feedback.notVerified',
  unavailable: 'feedback.unavailable',
  failed: 'feedback.failed',
};

/** Each refusal's code (SPEC §9.6). Anything else is a failure. */
const outcomeByCode: Readonly<Record<string, Outcome>> = {
  feedback_empty: 'empty',
  feedback_too_long: 'tooLong',
  feedback_rate_limited: 'rateLimited',
  feedback_not_verified: 'notVerified',
  feedback_not_configured: 'unavailable',
  feedback_unreachable: 'unavailable',
};

const siteKey = resolveTurnstileSiteKey(import.meta.env as Readonly<Record<string, string | undefined>>);

/**
 * The feedback widget (docs/DESIGN.md › Feedback, ADR 0019): a button in the bottom corner of every
 * page opening a small window — a heading, the text, *Отправить*. No sign-in; the message, the page and
 * the user id when signed in become an issue. The text is kept on a failure and while the window is
 * closed, and cleared only once it is sent.
 */
export function FeedbackWidget() {
  const t = useT();
  const client = useApiClient();
  const location = useLocation();
  const headingId = useId();
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState('');
  const [sending, setSending] = useState(false);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const captchaRef = useRef<HTMLDivElement>(null);
  const captcha = useRef<TurnstileWidget | null>(null);
  const textRef = useRef<HTMLTextAreaElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    textRef.current?.focus();
    const container = captchaRef.current;
    if (siteKey === null || container === null) {
      return undefined;
    }

    const widget = mountTurnstile(container, siteKey, setToken);
    captcha.current = widget;
    return () => {
      widget.remove();
      captcha.current = null;
      setToken(null);
    };
  }, [open]);

  const close = () => {
    setOpen(false);
    triggerRef.current?.focus();
  };

  const submit = async () => {
    const text = message.trim();
    if (text === '') {
      setOutcome('empty');
      return;
    }
    if (text.length > FEEDBACK_MAX_LENGTH) {
      setOutcome('tooLong');
      return;
    }

    setSending(true);
    setOutcome(null);
    try {
      await sendFeedback(client, text, location.pathname, token);
      setMessage('');
      setOutcome('sent');
    } catch (error) {
      setOutcome(isApiError(error) && error.code !== undefined ? (outcomeByCode[error.code] ?? 'failed') : 'failed');
    } finally {
      setSending(false);
      if (captcha.current !== null) {
        setToken(null);
        captcha.current.reset();
      }
    }
  };

  return (
    <div className="feedback">
      {open && (
        <div
          className="feedback__window"
          role="dialog"
          aria-labelledby={headingId}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              close();
            }
          }}
        >
          <div className="feedback__head">
            <h2 className="feedback__heading" id={headingId}>
              {t('feedback.heading')}
            </h2>
            <button type="button" className="feedback__close" aria-label={t('feedback.close')} onClick={close}>
              <span aria-hidden="true">×</span>
            </button>
          </div>
          <form
            className="feedback__form"
            onSubmit={(event) => {
              event.preventDefault();
              void submit();
            }}
          >
            <textarea
              ref={textRef}
              className="feedback__text"
              aria-labelledby={headingId}
              placeholder={t('feedback.placeholder')}
              maxLength={FEEDBACK_MAX_LENGTH}
              rows={5}
              value={message}
              onChange={(event) => {
                setMessage(event.target.value);
                setOutcome(null);
              }}
            />
            {siteKey !== null && <div className="feedback__captcha" ref={captchaRef} />}
            <div className="feedback__foot">
              <p className={outcome === 'sent' ? 'feedback__status feedback__status--good' : 'feedback__status'} role="status">
                {outcome !== null && t(outcomeText[outcome])}
              </p>
              <button type="submit" className="button button--small button--primary" disabled={sending}>
                {sending ? t('feedback.sending') : t('feedback.send')}
              </button>
            </div>
          </form>
        </div>
      )}
      <button
        ref={triggerRef}
        type="button"
        className="feedback__trigger"
        aria-label={t('feedback.open')}
        aria-expanded={open}
        onClick={() => {
          if (open) {
            close();
          } else {
            setOpen(true);
            if (outcome === 'sent') {
              setOutcome(null);
            }
          }
        }}
      >
        <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
          <path d="M4 5h16v11H9l-5 4z" fill="none" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" />
        </svg>
      </button>
    </div>
  );
}
