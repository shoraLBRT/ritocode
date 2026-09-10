import { ApiError, parseProblemBody } from './errors';

/**
 * The single seam between this application and the HTTP API.
 *
 * Everything the rest of the app knows about the backend goes through `ApiClient.request`:
 * where it lives, how a failure is read, and what a 204 means. Nothing else calls `fetch`,
 * which is what makes the ADR 0003 error envelope handled in one place rather than in each
 * screen — and what will make adding a credential header, when #6 issues one, a change here
 * instead of a change everywhere.
 */

export interface ApiClientOptions {
  /**
   * Absolute or relative base for the versioned API, without a trailing slash — for example
   * `http://localhost:5199/api/v1`.
   */
  readonly baseUrl: string;
  /** Injected in tests; defaults to the platform `fetch`. */
  readonly fetch?: typeof globalThis.fetch;
}

export interface RequestOptions {
  readonly method?: string;
  readonly query?: Readonly<Record<string, string | number | boolean | undefined>>;
  readonly body?: unknown;
  readonly signal?: AbortSignal;
}

/** Header ADR 0003 correlates a response with its log line by. Echoed back on every response. */
export const REQUEST_ID_HEADER = 'X-Request-Id';

export class ApiClient {
  readonly #baseUrl: string;
  readonly #fetch: typeof globalThis.fetch;

  constructor(options: ApiClientOptions) {
    this.#baseUrl = options.baseUrl.replace(/\/+$/, '');
    // Bound to `globalThis` because an unbound `fetch` throws an illegal-invocation TypeError
    // in the browser the moment it is called as a bare function reference.
    this.#fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
  }

  get baseUrl(): string {
    return this.#baseUrl;
  }

  /**
   * Performs one request and returns the decoded body.
   *
   * Throws {@link ApiError} for every failure — a transport fault, a status outside 2xx, or a
   * body that will not decode. A caller therefore never inspects a status code itself; it
   * catches, and branches on `code` when the server explained itself.
   */
  async request<T>(path: string, options: RequestOptions = {}): Promise<T> {
    const url = this.#resolve(path, options.query);
    const hasBody = options.body !== undefined;

    let response: Response;
    try {
      response = await this.#fetch(url, {
        method: options.method ?? 'GET',
        headers: {
          Accept: 'application/json',
          ...(hasBody ? { 'Content-Type': 'application/json' } : {}),
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

  #resolve(path: string, query: RequestOptions['query']): string {
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
    return `${this.#baseUrl}${normalisedPath}${suffix}`;
  }
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
