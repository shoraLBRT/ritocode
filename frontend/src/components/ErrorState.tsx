import { useState } from 'react';
import { ApiError, isUnavailable } from '../api';
import { useT } from '../i18n';
import type { Translate } from '../i18n';
import { Glyph } from './EmptyState';

/**
 * The one failure panel, in the empty state's frame: a warning mark, the title, what the API said
 * (muted) and *Повторить*.
 *
 * What it shows is decided by what the server actually said, which is the distinction
 * {@link ApiError} exists to keep: a `problem` explained itself and its `detail` is meant to be
 * read, while an `http` or `network` failure has nothing trustworthy to quote and gets a
 * sentence from the catalogue instead. The request id is shown whenever there is one, because it
 * is the only thing that turns "it broke" into a log line someone can find. A retry that returns
 * a promise shows as in flight until it settles.
 */
export function ErrorState({ error, onRetry }: { error: ApiError; onRetry?: () => unknown }) {
  const t = useT();
  const fieldErrors = error.fieldErrors;
  const [retrying, setRetrying] = useState(false);

  const retry = () => {
    const result = onRetry?.();

    if (result instanceof Promise) {
      setRetrying(true);
      const done = () => {
        setRetrying(false);
      };
      result.then(done, done);
    }
  };

  return (
    <div className="state state--error rc-empty" role="alert">
      <span className="rc-disc rc-disc--danger" aria-hidden="true">
        <Glyph icon="warning" />
      </span>
      <p className="rc-empty__title">{t('state.failed')}</p>
      <p className="rc-empty__text state__message">{describe(error, t)}</p>

      {fieldErrors && (
        <ul className="state__fields">
          {Object.entries(fieldErrors).map(([field, messages]) => (
            <li key={field}>{t('state.fieldError', { field, messages: messages.join(' ') })}</li>
          ))}
        </ul>
      )}

      {error.requestId !== undefined && <p className="state__request-id">{t('state.requestId', { id: error.requestId })}</p>}

      {onRetry && (
        <button type="button" className="button" disabled={retrying} aria-busy={retrying} onClick={retry}>
          {retrying && <span className="state__spinner" aria-hidden="true" />}
          {t('state.retry')}
        </button>
      )}
    </div>
  );
}

function describe(error: ApiError, t: Translate): string {
  // The outage itself is the banner's to explain (`ApiUnavailableBanner`, #298).
  if (isUnavailable(error)) {
    return t('state.unavailable');
  }

  switch (error.kind) {
    case 'problem':
      return error.message;
    case 'network':
      return t('state.unavailable');
    case 'http':
      return error.status === undefined ? t('state.unexpectedResponse') : t('state.unexpectedStatus', { status: String(error.status) });
  }
}
