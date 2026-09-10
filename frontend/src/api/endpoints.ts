import type { ApiClient } from './client';
import type { CatalogProblem, CatalogProblemDetail, ModuleInfo, Page, PageQuery } from './types';

/**
 * One function per endpoint the API serves today. They hold no state and no fetching policy —
 * that belongs to the caller and to {@link useApiResource} — so each is a name, a path and a
 * return type, and adding an endpoint is adding a function here.
 */

/** `GET /problems` — published problems, newest first, in the ADR 0003 page envelope. */
export function listProblems(
  client: ApiClient,
  query: PageQuery = {},
  signal?: AbortSignal,
): Promise<Page<CatalogProblem>> {
  return client.request<Page<CatalogProblem>>('/problems', {
    query: { page: query.page, pageSize: query.pageSize },
    ...(signal ? { signal } : {}),
  });
}

/**
 * `GET /problems/{slug}` — one problem by its stable slug, at its highest published version.
 *
 * Fails with `code: "problem_not_found"` when there is no such slug, or when the only versions
 * it has are drafts.
 */
export function getProblem(client: ApiClient, slug: string, signal?: AbortSignal): Promise<CatalogProblemDetail> {
  return client.request<CatalogProblemDetail>(`/problems/${encodeURIComponent(slug)}`, {
    ...(signal ? { signal } : {}),
  });
}

/** `GET /meta/modules` — which modules this host composed in. Diagnostics, not a product surface. */
export function listModules(client: ApiClient, signal?: AbortSignal): Promise<ModuleInfo[]> {
  return client.request<ModuleInfo[]>('/meta/modules', { ...(signal ? { signal } : {}) });
}
