import type { ApiClient } from './client';
import type {
  CatalogProblem,
  CatalogProblemDetail,
  ModuleInfo,
  Page,
  PageQuery,
  Workspace,
  WorkspaceFile,
  WorkspaceFileTree,
} from './types';

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

/**
 * `POST /workspaces` — opens the caller's workspace on a published version. Answers the same body
 * whether it created the workspace (201) or found the one already open (200), so it is safe to repeat.
 *
 * Fails with `code: "problem_version_not_found"` for a version that does not exist or is a draft.
 */
export function openWorkspace(client: ApiClient, problemVersionId: string, signal?: AbortSignal): Promise<Workspace> {
  return client.request<Workspace>('/workspaces', {
    method: 'POST',
    body: { problemVersionId },
    ...(signal ? { signal } : {}),
  });
}

/** `GET /workspaces/{id}` — fails with `code: "workspace_not_found"` for anything the caller does not own. */
export function getWorkspace(client: ApiClient, workspaceId: string, signal?: AbortSignal): Promise<Workspace> {
  return client.request<Workspace>(`/workspaces/${encodeURIComponent(workspaceId)}`, {
    ...(signal ? { signal } : {}),
  });
}

/** `GET /workspaces/{id}/files` — every file in the workspace, ordered by path. */
export function listWorkspaceFiles(
  client: ApiClient,
  workspaceId: string,
  signal?: AbortSignal,
): Promise<WorkspaceFileTree> {
  return client.request<WorkspaceFileTree>(`/workspaces/${encodeURIComponent(workspaceId)}/files`, {
    ...(signal ? { signal } : {}),
  });
}

/**
 * `GET /workspaces/{id}/files/content?path=` — one file as text.
 *
 * The path travels as a query value, never as URL segments: the server removes `.` and `..` from a
 * URL path before routing, so a path sent there would not be the path the API checks.
 *
 * Fails with `code: "validation_failed"` and `errors.path` for a path that could leave the tree,
 * `"workspace_file_not_found"` for one the tree does not hold, and `"workspace_file_not_text"` for a
 * file that is not UTF-8.
 */
export function getWorkspaceFile(
  client: ApiClient,
  workspaceId: string,
  path: string,
  signal?: AbortSignal,
): Promise<WorkspaceFile> {
  return client.request<WorkspaceFile>(`/workspaces/${encodeURIComponent(workspaceId)}/files/content`, {
    query: { path },
    ...(signal ? { signal } : {}),
  });
}

/** `GET /meta/modules` — which modules this host composed in. Diagnostics, not a product surface. */
export function listModules(client: ApiClient, signal?: AbortSignal): Promise<ModuleInfo[]> {
  return client.request<ModuleInfo[]>('/meta/modules', { ...(signal ? { signal } : {}) });
}
