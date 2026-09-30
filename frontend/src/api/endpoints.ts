import type { ApiClient } from './client';
import type { Me, ModuleInfo, Page, PageQuery, ProblemCatalogue, TaskSummary } from './types';

/**
 * One function per endpoint the API serves today. They hold no state and no fetching policy —
 * that belongs to the caller and to {@link useApiResource} — so each is a name, a path and a
 * return type, and adding an endpoint is adding a function here.
 */

/** `GET /me` — the signed-in caller. Rejects with an unauthenticated {@link ApiError} when signed out. */
export function getMe(client: ApiClient, signal?: AbortSignal): Promise<Me> {
  return client.request<Me>('/me', { ...(signal ? { signal } : {}) });
}

/** `GET /problems` — the whole problem catalogue, one object rather than a page (SPEC §4.2). */
export function getProblemCatalogue(client: ApiClient, signal?: AbortSignal): Promise<ProblemCatalogue> {
  return client.request<ProblemCatalogue>('/problems', { ...(signal ? { signal } : {}) });
}

/** `GET /tasks` — a page of the task catalogue, easy first. */
export function listTasks(client: ApiClient, query: PageQuery = {}, signal?: AbortSignal): Promise<Page<TaskSummary>> {
  return client.request<Page<TaskSummary>>('/tasks', { query: { ...query }, ...(signal ? { signal } : {}) });
}

/** `GET /meta/modules` — which modules this host composed in. Diagnostics, not a product surface. */
export function listModules(client: ApiClient, signal?: AbortSignal): Promise<ModuleInfo[]> {
  return client.request<ModuleInfo[]>('/meta/modules', { ...(signal ? { signal } : {}) });
}
