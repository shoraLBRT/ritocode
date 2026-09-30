import type { ApiClient } from './client';
import type {
  Attempt,
  AttemptStep,
  AttemptSummary,
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

/** `POST /attempts` — starts an attempt at a published task. */
export function startAttempt(client: ApiClient, task: string): Promise<Attempt> {
  return client.request<Attempt>('/attempts', { method: 'POST', body: { task } });
}

/** `PATCH /attempts/{id}` — records the step reached. */
export function recordStep(client: ApiClient, id: string, step: AttemptStep): Promise<Attempt> {
  return client.request<Attempt>(`/attempts/${encodeURIComponent(id)}`, { method: 'PATCH', body: { step } });
}

/** `POST /attempts/{id}/submit` — checks the answer; the result reveals the key. */
export function submitAttempt(client: ApiClient, id: string, picks: readonly Pick[]): Promise<Attempt> {
  return client.request<Attempt>(`/attempts/${encodeURIComponent(id)}/submit`, { method: 'POST', body: { picks } });
}

/** `GET /attempts/{id}` — an attempt and, once submitted, its result. */
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

/** `GET /meta/modules` — which modules this host composed in. Diagnostics, not a product surface. */
export function listModules(client: ApiClient, signal?: AbortSignal): Promise<ModuleInfo[]> {
  return client.request<ModuleInfo[]>('/meta/modules', { ...(signal ? { signal } : {}) });
}
