import { useId, useState } from 'react';
import { sendSignal, useApiClient } from '../../api';
import type { ApiError } from '../../api';
import { ErrorState } from '../../components/ErrorState';
import { useT } from '../../i18n';

/** The longest comment the API keeps (SPEC §4.8). */
const COMMENT_MAX_LENGTH = 500;

type Stage = 'closed' | 'open' | 'sending' | 'sent';

/**
 * *I'm sure it is here* under an extra pick of the review (docs/SPEC.md §4.5, §4.8): it opens a
 * one-line comment and sends a signal to the author. The score does not move, and the review says
 * so once the signal is sent. A pick signalled before — on an earlier visit — shows as sent.
 */
export function SignalControl({ attempt, card, sent }: { attempt: string; card: string; sent: boolean }) {
  const client = useApiClient();
  const t = useT();
  const inputId = useId();
  const [stage, setStage] = useState<Stage>(sent ? 'sent' : 'closed');
  const [comment, setComment] = useState('');
  const [failure, setFailure] = useState<ApiError | null>(null);

  if (stage === 'sent') {
    return (
      <p className="signal signal--sent" role="status">
        {t('review.signalSent')}
      </p>
    );
  }

  if (stage === 'closed') {
    return (
      <p className="signal">
        <button
          type="button"
          className="button"
          onClick={() => {
            setStage('open');
          }}
        >
          {t('review.signal')}
        </button>
      </p>
    );
  }

  const send = async () => {
    setStage('sending');
    setFailure(null);

    try {
      await sendSignal(client, attempt, card, comment);
      setStage('sent');
    } catch (cause) {
      const error = cause as ApiError;

      // Sent from another tab, or before a reload: the signal is there either way.
      if (error.code === 'signal_already_sent') {
        setStage('sent');
        return;
      }

      setFailure(error);
      setStage('open');
    }
  };

  return (
    <form
      className="signal signal--open"
      onSubmit={(event) => {
        event.preventDefault();
        void send();
      }}
    >
      <label className="signal__label" htmlFor={inputId}>
        {t('review.signalComment')}
      </label>
      <input
        id={inputId}
        className="signal__comment"
        type="text"
        maxLength={COMMENT_MAX_LENGTH}
        placeholder={t('review.signalPlaceholder')}
        value={comment}
        disabled={stage === 'sending'}
        onChange={(event) => {
          setComment(event.target.value);
        }}
      />
      <span className="signal__actions">
        <button type="submit" className="button button--primary" disabled={stage === 'sending'}>
          {stage === 'sending' ? t('review.signalSending') : t('review.signalSend')}
        </button>
        <button
          type="button"
          className="button"
          disabled={stage === 'sending'}
          onClick={() => {
            setStage('closed');
            setFailure(null);
          }}
        >
          {t('review.signalCancel')}
        </button>
      </span>
      {failure !== null && <ErrorState error={failure} />}
    </form>
  );
}
