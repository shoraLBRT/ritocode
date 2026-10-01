import { ApiError, parseProblemBody } from './errors';

/**
 * The single seam between this application and the HTTP API.
 *
 * Everything the rest of the app knows about the backend goes through `ApiClient.request`:
 * where it lives, how a failure is read, and what a 204 means. Nothing else calls `fetch`,
 * which is what makes the ADR 0003 error envelope handled in one place rather than in each
 * screen — and what makes the session's CSRF token (#6) a header added here rather than in
 * every screen.
 */

export interface ApiClientOptions {
  /**
   * Absolute or relative base for the versioned API, without a trailing slash — for example
   * `http://localhost:5199/api/v1`.
   */
  readonly baseUrl: string;
  /** Injected in tests; defaults to the platform `fetch`. */
  readonly fetch?: typeof globalThis.fetch;
  /** The session's CSRF token, if there is one. Injected in tests; defaults to reading its cookie. */
  readonly csrfToken?: () => string | undefined;
}

export interface RequestOptions {
  readonly method?: string;
  readonly query?: Readonly<Record<string, string | number | boolean | undefined>>;
  readonly body?: unknown;
  readonly signal?: AbortSignal;
  /**
   * The path is the host's, not the versioned API's: sign-in and sign-out live outside `/api/v1`
   * (docs/SPEC.md §9.3), so `/auth/logout` resolves against {@link ApiClient.hostUrl}.
   */
  readonly outsideApi?: boolean;
}

/** Header ADR 0003 correlates a response with its log line by. Echoed back on every response. */
export const REQUEST_ID_HEADER = 'X-Request-Id';

/**
 * Header a state-changing request repeats the session's CSRF token in (ADR 0012). The token is in a
 * cookie this page can read and another site cannot; the session cookie itself is out of reach.
 */
export const CSRF_HEADER = 'X-CSRF-Token';

const CSRF_COOKIE = '__Host-ritocode-csrf';

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

export class ApiClient {
  readonly #baseUrl: string;
  readonly #fetch: typeof globalThis.fetch;
  readonly #csrfToken: () => string | undefined;

  constructor(options: ApiClientOptions) {
    this.#baseUrl = options.baseUrl.replace(/\/+$/, '');
    // Bound to `globalThis` because an unbound `fetch` throws an illegal-invocation TypeError
    // in the browser the moment it is called as a bare function reference.
    this.#fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
    this.#csrfToken = options.csrfToken ?? readCsrfCookie;
  }

  get baseUrl(): string {
    return this.#baseUrl;
  }

  /**
   * Where the host serves what lives outside the versioned API — `/auth/login/{provider}`,
   * `/auth/logout`: the base without its `/api/v1`. Empty when the API is on the page's own origin
   * and its base is the relative `/api/v1`, so a path stays a path.
   */
  get hostUrl(): string {
    return this.#baseUrl.replace(/\/api\/v1$/, '');
  }

  /**
   * Performs one request and returns the decoded body.
   *
   * Throws {@link ApiError} for every failure — a transport fault, a status outside 2xx, or a
   * body that will not decode. A caller therefore never inspects a status code itself; it
   * catches, and branches on `code` when the server explained itself.
   */
  async request<T>(path: string, options: RequestOptions = {}): Promise<T> {
    const url = this.#resolve(options.outsideApi === true ? this.hostUrl : this.#baseUrl, path, options.query);
    const hasBody = options.body !== undefined;
    const method = (options.method ?? 'GET').toUpperCase();
    const csrf = SAFE_METHODS.has(method) ? undefined : this.#csrfToken();

    let response: Response;
    try {
      response = await this.#fetch(url, {
        method,
        // The session cookie goes with every request. In development the pages (Vite's port) and
        // the API are two origins of one site, and a cross-origin fetch sends no cookie without
        // this; in production they share an origin and it changes nothing.
        credentials: 'include',
        headers: {
          Accept: 'application/json',
          ...(hasBody ? { 'Content-Type': 'application/json' } : {}),
          ...(csrf !== undefined ? { [CSRF_HEADER]: csrf } : {}),
        },
        ...(hasBody ? { body: JSON.stringify(options.body) } : {}),
        ...(options.signal ? { signal: options.signal } : {}),
      });
    } catch (cause) {
      // An abort is the caller's own doing — a navigation, a superseded render — and must not
      // reach the screen as a failure the user can act on. It is rethrown untouched so the
      // caller's own `signal.aborted` check is what decides.
      if (cause instanceof DOMException && cause.name === 'AbortError') {
        throw cause;
      }

      throw new ApiError('The server could not be reached.', 'network', { cause });
    }

    if (!response.ok) {
      throw await toApiError(response);
    }

    return (await decodeBody(response)) as T;
  }

  #resolve(base: string, path: string, query: RequestOptions['query']): string {
    const normalisedPath = path.startsWith('/') ? path : `/${path}`;
    const search = new URLSearchParams();

    for (const [key, value] of Object.entries(query ?? {})) {
      // `undefined` means "not asked for", and must not become the string "undefined" in a
      // query the API would then reject as out of range.
      if (value !== undefined) {
        search.set(key, String(value));
      }
    }

    const suffix = search.size > 0 ? `?${search.toString()}` : '';
    return `${base}${normalisedPath}${suffix}`;
  }
}

/** The session's CSRF token from the page's cookies; none when signed out, or where there is no document. */
function readCsrfCookie(): string | undefined {
  return typeof document === 'undefined' ? undefined : csrfTokenFrom(document.cookie);
}

/** The CSRF token in a `document.cookie` string, if the session's cookie is there. */
export function csrfTokenFrom(cookies: string): string | undefined {
  const prefix = `${CSRF_COOKIE}=`;
  const cookie = cookies.split(/;\s*/).find((part) => part.startsWith(prefix));
  return cookie === undefined || cookie.length === prefix.length ? undefined : decodeURIComponent(cookie.slice(prefix.length));
}

async function toApiError(response: Response): Promise<ApiError> {
  const requestIdHeader = response.headers.get(REQUEST_ID_HEADER) ?? undefined;
  const body = await readJsonOrNull(response);
  const problem = parseProblemBody(body);

  if (problem === null) {
    return new ApiError(`The server returned ${String(response.status)}.`, 'http', {
      status: response.status,
      requestId: requestIdHeader,
    });
  }

  return new ApiError(problem.detail ?? problem.title ?? `The server returned ${String(response.status)}.`, 'problem', {
    // The status line is what actually happened; `status` in the body is a copy of it.
    status: response.status,
    code: problem.code,
    requestId: problem.requestId ?? requestIdHeader,
    fieldErrors: problem.errors,
  });
}

async function decodeBody(response: Response): Promise<unknown> {
  if (response.status === 204) {
    return undefined;
  }

  try {
    const text = await response.text();
    return text.length === 0 ? undefined : (JSON.parse(text) as unknown);
  } catch (cause) {
    throw new ApiError('The server sent a response that could not be read.', 'http', {
      status: response.status,
      requestId: response.headers.get(REQUEST_ID_HEADER) ?? undefined,
      cause,
    });
  }
}

/** Reading an error body must never throw: a failure with no body is still a failure to report. */
async function readJsonOrNull(response: Response): Promise<unknown> {
  try {
    const text = await response.text();
    return text.length === 0 ? null : (JSON.parse(text) as unknown);
  } catch {
    return null;
  }
}
