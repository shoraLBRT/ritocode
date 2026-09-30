import { ApiError } from '../api';
import { useT } from '../i18n';
import type { Translate } from '../i18n';

/**
 * The one failure panel.
 *
 * What it shows is decided by what the server actually said, which is the distinction
 * {@link ApiError} exists to keep: a `problem` explained itself and its `detail` is meant to be
 * read, while an `http` or `network` failure has nothing trustworthy to quote and gets a
 * sentence from the catalogue instead. The request id is shown whenever there is one, because it
 * is the only thing that turns "it broke" into a log line someone can find.
 */
export function ErrorState({ error, onRetry }: { error: ApiError; onRetry?: () => void }) {
  const t = useT();
  const fieldErrors = error.fieldErrors;

  return (
    <div className="state state--error" role="alert">
      <p className="state__message">{describe(error, t)}</p>

      {fieldErrors && (
        <ul className="state__fields">
          {Object.entries(fieldErrors).map(([field, messages]) => (
            <li key={field}>{t('state.fieldError', { field, messages: messages.join(' ') })}</li>
          ))}
        </ul>
      )}

      {error.requestId !== undefined && <p className="state__request-id">{t('state.requestId', { id: error.requestId })}</p>}

      {onRetry && (
        <button type="button" className="button" onClick={onRetry}>
          {t('state.retry')}
        </button>
      )}
    </div>
  );
}

function describe(error: ApiError, t: Translate): string {
  switch (error.kind) {
    case 'problem':
      return error.message;
    case 'network':
      return t('state.unreachable');
    case 'http':
      return error.status === undefined ? t('state.unexpectedResponse') : t('state.unexpectedStatus', { status: String(error.status) });
  }
}
