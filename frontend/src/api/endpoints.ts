import type { ApiClient } from './client';
import type {
  AdminAttempt,
  AdminAttemptStatus,
  AdminSignal,
  AdminSignalStatus,
  AdminUser,
  Attempt,
  AttemptStep,
  AttemptSummary,
  FeedbackSent,
  Me,
  ModuleInfo,
  Page,
  PageQuery,
  Pick,
  ProblemCatalogue,
  Progress,
  Signal,
  TaskDetail,
  TaskSummary,
  TreatmentTree,
} from './types';

/**
 * One function per endpoint the API serves today. They hold no state and no fetching policy —
 * that belongs to the caller and to {@link useApiResource} — so each is a name, a path and a
 * return type, and adding an endpoint is adding a function here.
 */

/** `GET /me` — the signed-in caller. Rejects with an unauthenticated {@link ApiError} when signed out. */
export function getMe(client: ApiClient, signal?: AbortSignal): Promise<Me> {
  return client.request<Me>('/me', { ...(signal ? { signal } : {}) });
}

/** `GET /me/progress` — the caller's progress, from first attempts only. */
export function getProgress(client: ApiClient, signal?: AbortSignal): Promise<Progress> {
  return client.request<Progress>('/me/progress', { ...(signal ? { signal } : {}) });
}

/** `GET /problems` — the whole problem catalogue, one object rather than a page (SPEC §4.2). */
export function getProblemCatalogue(client: ApiClient, signal?: AbortSignal): Promise<ProblemCatalogue> {
  return client.request<ProblemCatalogue>('/problems', { ...(signal ? { signal } : {}) });
}

/** `GET /tasks` — a page of the task catalogue, easy first. */
export function listTasks(client: ApiClient, query: PageQuery = {}, signal?: AbortSignal): Promise<Page<TaskSummary>> {
  return client.request<Page<TaskSummary>>('/tasks', { query: { ...query }, ...(signal ? { signal } : {}) });
}

/** `GET /tasks/{slug}` — a task to solve, without its answer key. */
export function getTask(client: ApiClient, slug: string, signal?: AbortSignal): Promise<TaskDetail> {
  return client.request<TaskDetail>(`/tasks/${encodeURIComponent(slug)}`, { ...(signal ? { signal } : {}) });
}

/** `GET /treatments` — the treatment tree. */
export function getTreatments(client: ApiClient, signal?: AbortSignal): Promise<TreatmentTree> {
  return client.request<TreatmentTree>('/treatments', { ...(signal ? { signal } : {}) });
}

/** `GET /attempts?task=` — the caller's attempts, newest first. */
export function listAttempts(
  client: ApiClient,
  query: PageQuery & { readonly task?: string } = {},
  signal?: AbortSignal,
): Promise<Page<AttemptSummary>> {
  return client.request<Page<AttemptSummary>>('/attempts', { query: { ...query }, ...(signal ? { signal } : {}) });
}

/**
 * The caller's attempts started since `since`, newest first, read a hundred at a time until older
 * ones start — what the activity map and the streak are counted from.
 */
export async function listAttemptsSince(client: ApiClient, since: Date, signal?: AbortSignal): Promise<AttemptSummary[]> {
  const attempts: AttemptSummary[] = [];

  for (let page = 1; ; page += 1) {
    const result = await listAttempts(client, { page, pageSize: 100 }, signal);
    attempts.push(...result.items);
    const oldest = result.items.at(-1);

    if (!result.hasNextPage || oldest === undefined || new Date(oldest.startedAt) < since) {
      return attempts;
    }
  }
}

/** `POST /attempts` — starts an attempt at a published task. */
export function startAttempt(client: ApiClient, task: string): Promise<Attempt> {
  return client.request<Attempt>('/attempts', { method: 'POST', body: { task } });
}

/** `PATCH /attempts/{id}` — records the step reached. */
export function recordStep(client: ApiClient, id: string, step: AttemptStep): Promise<Attempt> {
  return client.request<Attempt>(`/attempts/${encodeURIComponent(id)}`, { method: 'PATCH', body: { step } });
}

/** `POST /attempts/{id}/submit` — checks the answer; the outcome comes back, the key only if already revealed. */
export function submitAttempt(client: ApiClient, id: string, picks: readonly Pick[]): Promise<Attempt> {
  return client.request<Attempt>(`/attempts/${encodeURIComponent(id)}/submit`, { method: 'POST', body: { picks } });
}

/**
 * `POST /attempts/{id}/reveal` — reveals the answer to the task of a submitted attempt, for good,
 * and answers the attempt in full. Revealing again changes nothing.
 */
export function revealAnswer(client: ApiClient, id: string): Promise<Attempt> {
  return client.request<Attempt>(`/attempts/${encodeURIComponent(id)}/reveal`, { method: 'POST' });
}

/** `GET /attempts/{id}` — an attempt and, once submitted, its outcome; once revealed, its result. */
export function getAttempt(client: ApiClient, id: string, signal?: AbortSignal): Promise<Attempt> {
  return client.request<Attempt>(`/attempts/${encodeURIComponent(id)}`, { ...(signal ? { signal } : {}) });
}

/**
 * `POST /signals` — says an extra pick of a submitted attempt is really present. Never changes the
 * score; a second signal for the same pick is refused with `signal_already_sent`.
 */
export function sendSignal(client: ApiClient, attempt: string, card: string, comment: string): Promise<Signal> {
  return client.request<Signal>('/signals', { method: 'POST', body: { attempt, card, comment } });
}

/** `POST /feedback` — a message from anyone, signed in or not, becomes an issue (SPEC §9.6). */
export function sendFeedback(
  client: ApiClient,
  message: string,
  page: string,
  captchaToken: string | null,
): Promise<FeedbackSent> {
  return client.request<FeedbackSent>('/feedback', { method: 'POST', body: { message, page, captchaToken } });
}

/** `GET /admin/signals` — signals, open or resolved, newest first. Admins only; anyone else gets a 404. */
export function listAdminSignals(
  client: ApiClient,
  query: PageQuery & { readonly status?: AdminSignalStatus } = {},
  signal?: AbortSignal,
): Promise<Page<AdminSignal>> {
  return client.request<Page<AdminSignal>>('/admin/signals', { query: { ...query }, ...(signal ? { signal } : {}) });
}

/** `POST /admin/signals/{id}/resolve` — marks a signal resolved; resolving it again changes nothing. */
export function resolveSignal(client: ApiClient, id: string): Promise<AdminSignal> {
  return client.request<AdminSignal>(`/admin/signals/${encodeURIComponent(id)}/resolve`, { method: 'POST' });
}

/** `GET /admin/users` — every user, newest first, with their providers and counts. Admins only. */
export function listAdminUsers(client: ApiClient, query: PageQuery = {}, signal?: AbortSignal): Promise<Page<AdminUser>> {
  return client.request<Page<AdminUser>>('/admin/users', { query: { ...query }, ...(signal ? { signal } : {}) });
}

/** `GET /admin/attempts` — every user's attempts, newest first, optionally open or submitted only, or one learner's. Admins only. */
export function listAdminAttempts(
  client: ApiClient,
  query: PageQuery & { readonly status?: AdminAttemptStatus; readonly user?: string } = {},
  signal?: AbortSignal,
): Promise<Page<AdminAttempt>> {
  return client.request<Page<AdminAttempt>>('/admin/attempts', { query: { ...query }, ...(signal ? { signal } : {}) });
}

/** The providers a learner can sign in with (docs/SPEC.md §6.1), in the order they are offered. */
export const SIGN_IN_PROVIDERS = ['github', 'google'] as const;

export type SignInProvider = (typeof SIGN_IN_PROVIDERS)[number];

/**
 * Where the browser goes to sign in: `GET /auth/login/{provider}?returnUrl=`, outside the versioned
 * API. A link, not a request — the provider's pages are the browser's to show. The provider returns
 * the browser to `returnPath`, which must be a local path ({@link isLocalPath}); anything else is
 * replaced by `/` here, as the server would refuse it.
 */
export function signInUrl(client: ApiClient, provider: SignInProvider, returnPath: string): string {
  const query = new URLSearchParams({ returnUrl: isLocalPath(returnPath) ? returnPath : '/' });
  return `${client.hostUrl}/auth/login/${provider}?${query.toString()}`;
}

/**
 * Whether `value` is a path on this site, by the server's own rule (`ReturnUrl.IsLocal`): one
 * leading `/`, not `//` or `/\` — which a browser reads as another host — and no control characters.
 */
export function isLocalPath(value: string): boolean {
  // eslint-disable-next-line no-control-regex -- control characters are exactly what is refused
  return value.startsWith('/') && value[1] !== '/' && value[1] !== '\\' && !/[\u0000-\u001f\u007f-\u009f]/.test(value);
}

/** `POST /auth/logout` — ends the session and clears its cookies. Outside the versioned API. */
export function signOut(client: ApiClient): Promise<void> {
  return client.request<undefined>('/auth/logout', { method: 'POST', outsideApi: true });
}

/** `GET /meta/modules` — which modules this host composed in. Diagnostics, not a product surface. */
export function listModules(client: ApiClient, signal?: AbortSignal): Promise<ModuleInfo[]> {
  return client.request<ModuleInfo[]>('/meta/modules', { ...(signal ? { signal } : {}) });
}
