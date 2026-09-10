/**
 * The one place the ADR 0003 error envelope is understood.
 *
 * Every non-2xx response the API produces is RFC 9457 `application/problem+json` with two
 * Ritocode extensions — `code` and `requestId` — and validation failures add `errors`. Parsing
 * that here, once, is what keeps the rest of the app from growing a special case per endpoint,
 * which is the failure ADR 0003 was written to prevent.
 */

/** The RFC 9457 body, with the two extensions ADR 0003 adds. */
export interface ApiProblemBody {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly instance?: string;
  /** Stable, machine-readable. This is the member a client is allowed to branch on. */
  readonly code?: string;
  /** Matches the `X-Request-Id` response header, so a user-reported failure maps to a log line. */
  readonly requestId?: string;
  /** Present on validation failures: camelCase field path to messages. */
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}

/**
 * Why a request failed, in the terms the UI actually branches on.
 *
 * `problem` means the server answered with the documented envelope, so `code` is trustworthy.
 * `http` means it answered with a status but not that envelope — a proxy's 502 page, say — and
 * `network` means it did not answer at all. The three are kept apart because they call for
 * different words on screen: only the first can explain itself.
 */
export type ApiErrorKind = 'problem' | 'http' | 'network';

/**
 * A failed API call.
 *
 * `code` is `undefined` for anything but a `problem`, which is deliberate: a caller that
 * branches on a code is asserting the server explained itself, and there is no honest code to
 * invent for a dropped connection.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number | undefined;
  readonly code: string | undefined;
  readonly requestId: string | undefined;
  readonly fieldErrors: Readonly<Record<string, readonly string[]>> | undefined;

  constructor(
    message: string,
    kind: ApiErrorKind,
    options: {
      status?: number | undefined;
      code?: string | undefined;
      requestId?: string | undefined;
      fieldErrors?: Readonly<Record<string, readonly string[]>> | undefined;
      cause?: unknown;
    } = {},
  ) {
    super(message, options.cause === undefined ? undefined : { cause: options.cause });
    this.name = 'ApiError';
    this.kind = kind;
    this.status = options.status;
    this.code = options.code;
    this.requestId = options.requestId;
    this.fieldErrors = options.fieldErrors;
  }

  /** True when the resource is absent or the caller may not see it — ADR 0003 conflates them on purpose. */
  get isNotFound(): boolean {
    return this.status === 404;
  }

  /** True when the caller has no identity yet. Nothing issues one until #6; the branch exists so it is not forgotten. */
  get isUnauthenticated(): boolean {
    return this.status === 401;
  }

  /** True when the request body or query failed validation, in which case `fieldErrors` says where. */
  get isValidation(): boolean {
    return this.code === 'validation_failed' || (this.status === 400 && this.fieldErrors !== undefined);
  }
}

export function isApiError(value: unknown): value is ApiError {
  return value instanceof ApiError;
}

/**
 * Reads an error envelope out of a parsed JSON body.
 *
 * Returns `null` when the body is not the documented shape, so the caller can fall back to a
 * plain HTTP error rather than presenting a half-read envelope as if the server had explained
 * itself. Every member is optional on the wire, so each is checked rather than assumed.
 */
export function parseProblemBody(body: unknown): ApiProblemBody | null {
  if (typeof body !== 'object' || body === null || Array.isArray(body)) {
    return null;
  }

  const candidate = body as Record<string, unknown>;

  // `code` is the extension that makes this envelope Ritocode's rather than any RFC 9457 body,
  // and it is the only member a caller may branch on. Without it there is nothing to gain by
  // treating the response as a problem body.
  if (typeof candidate.code !== 'string') {
    return null;
  }

  return {
    ...pickString(candidate, 'type'),
    ...pickString(candidate, 'title'),
    ...pickNumber(candidate, 'status'),
    ...pickString(candidate, 'detail'),
    ...pickString(candidate, 'instance'),
    code: candidate.code,
    ...pickString(candidate, 'requestId'),
    ...pickFieldErrors(candidate),
  };
}

function pickString<K extends string>(source: Record<string, unknown>, key: K): Record<K, string> | object {
  const value = source[key];
  return typeof value === 'string' ? { [key]: value } : {};
}

function pickNumber<K extends string>(source: Record<string, unknown>, key: K): Record<K, number> | object {
  const value = source[key];
  return typeof value === 'number' ? { [key]: value } : {};
}

function pickFieldErrors(source: Record<string, unknown>): { errors: Record<string, string[]> } | object {
  const value = source.errors;
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return {};
  }

  const errors: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(value as Record<string, unknown>)) {
    if (Array.isArray(messages)) {
      errors[field] = messages.filter((message): message is string => typeof message === 'string');
    }
  }

  return Object.keys(errors).length > 0 ? { errors } : {};
}
