import { ApiError } from '../api';

/**
 * The one failure panel.
 *
 * What it shows is decided by what the server actually said, which is the distinction
 * {@link ApiError} exists to keep: a `problem` explained itself and its `detail` is meant to be
 * read, while an `http` or `network` failure has nothing trustworthy to quote and gets a
 * sentence written here instead. The request id is shown whenever there is one, because it is
 * the only thing that turns "it broke" into a log line someone can find.
 */
export function ErrorState({ error, onRetry }: { error: ApiError; onRetry?: () => void }) {
  const fieldErrors = error.fieldErrors;

  return (
    <div className="state state--error" role="alert">
      <p className="state__message">{describe(error)}</p>

      {fieldErrors && (
        <ul className="state__fields">
          {Object.entries(fieldErrors).map(([field, messages]) => (
            <li key={field}>
              <code>{field}</code>: {messages.join(' ')}
            </li>
          ))}
        </ul>
      )}

      {error.requestId !== undefined && (
        <p className="state__request-id">
          Request id: <code>{error.requestId}</code>
        </p>
      )}

      {onRetry && (
        <button type="button" className="button" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  );
}

function describe(error: ApiError): string {
  switch (error.kind) {
    case 'problem':
      return error.message;
    case 'network':
      return 'The API could not be reached. Check that the backend is running.';
    case 'http':
      return `The API answered with an unexpected status${error.status === undefined ? '' : ` (${String(error.status)})`}.`;
  }
}
