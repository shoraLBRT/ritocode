import { CSRF_HEADER, REQUEST_ID_HEADER, csrfTokenFrom } from '../api/client';
import { readWebCommit } from '../pages/admin/webVersion';

/**
 * The browser's error reporter (ADR 0020, #294): `window.onerror`, `unhandledrejection` and the route error
 * boundary send `POST /client-errors` — the error, the page's path and the commit the bundle was built
 * from, never what the learner typed. It never throws and never retries: a report that fails is lost.
 *
 * Installed once, at start, by `installErrorReporter` in `main.tsx`. Until then (and in tests, unless a
 * test installs it) `reportError` sends nothing and answers `null`.
 */

/** At most this many reports per page load, so a render loop cannot flood the endpoint (it is limited too). */
export const MAX_REPORTS_PER_PAGE = 10;

const MAX_MESSAGE = 1000;
const MAX_STACK = 8000;

interface Reporter {
  readonly baseUrl: string;
  readonly fetch: typeof fetch;
  readonly commit: () => Promise<string | null>;
}

let reporter: Reporter | null = null;
let reports = new Map<string, Promise<string | null>>();

/**
 * Starts reporting the page's uncaught errors to the API at `baseUrl` (the client's, `/api/v1` by default).
 * Answers a function that stops it, for tests.
 */
export function installErrorReporter(
  baseUrl: string,
  options: { fetch?: typeof fetch; commit?: () => Promise<string | null> } = {},
): () => void {
  let commit: Promise<string | null> | null = null;
  reporter = {
    baseUrl: baseUrl.replace(/\/+$/, ''),
    fetch: options.fetch ?? globalThis.fetch.bind(globalThis),
    // Read once, when the first error needs it, not on every page load.
    commit: options.commit ?? (() => (commit ??= readWebCommit())),
  };
  reports = new Map();

  const onError = (event: ErrorEvent) => {
    void reportError(event.error ?? event.message);
  };
  const onRejection = (event: PromiseRejectionEvent) => {
    void reportError(event.reason);
  };

  window.addEventListener('error', onError);
  window.addEventListener('unhandledrejection', onRejection);

  return () => {
    window.removeEventListener('error', onError);
    window.removeEventListener('unhandledrejection', onRejection);
    reporter = null;
  };
}

/**
 * Sends one error and answers the report's request id — the id the admin list shows beside it — or `null`
 * when nothing was sent. The same error twice in one page load is sent once, and both callers get its id:
 * the boundary and `window.onerror` may both see a render error.
 */
export function reportError(error: unknown): Promise<string | null> {
  const current = reporter;
  if (current === null) {
    return Promise.resolve(null);
  }

  const { type, message, stack } = describe(error);
  const key = `${type}\n${message}`;
  const known = reports.get(key);
  if (known !== undefined) {
    return known;
  }
  if (reports.size >= MAX_REPORTS_PER_PAGE) {
    return Promise.resolve(null);
  }

  const sent = send(current, { type, message, stack, page: window.location.pathname });
  reports.set(key, sent);
  return sent;
}

async function send(
  current: Reporter,
  report: { type: string; message: string; stack: string | null; page: string },
): Promise<string | null> {
  try {
    const commit = await current.commit();
    const csrf = typeof document === 'undefined' ? undefined : csrfTokenFrom(document.cookie);
    const response = await current.fetch(`${current.baseUrl}/client-errors`, {
      method: 'POST',
      credentials: 'include',
      keepalive: true,
      headers: {
        'Content-Type': 'application/json',
        ...(csrf !== undefined ? { [CSRF_HEADER]: csrf } : {}),
      },
      body: JSON.stringify({ ...report, commit }),
    });
    return response.ok ? response.headers.get(REQUEST_ID_HEADER) : null;
  } catch {
    // Offline, or the API is what is down: the error is lost, and that is not worth an error of its own.
    return null;
  }
}

/** The type, message and stack of anything thrown — an `Error`, or whatever else a script threw or rejected with. */
export function describe(error: unknown): { type: string; message: string; stack: string | null } {
  if (error instanceof Error) {
    return {
      type: error.name || 'Error',
      message: error.message.slice(0, MAX_MESSAGE),
      stack: typeof error.stack === 'string' ? error.stack.slice(0, MAX_STACK) : null,
    };
  }

  let message: string;
  try {
    message = typeof error === 'string' ? error : JSON.stringify(error) ?? String(error);
  } catch {
    message = String(error);
  }
  return { type: 'Error', message: message.slice(0, MAX_MESSAGE), stack: null };
}
