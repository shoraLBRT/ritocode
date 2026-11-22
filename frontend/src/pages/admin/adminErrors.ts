import type { ApiClient } from '../../api';
import type { Page, PageQuery } from '../../api/types';

/**
 * The admin area's error records (ADR 0020, #294), kept beside their page rather than in `src/api`: nothing
 * else reads them.
 */

export type ErrorGroupStatus = 'open' | 'resolved';

/** One row of `GET /admin/errors`. */
export interface AdminErrorGroup {
  readonly id: string;
  readonly source: 'server' | 'browser';
  readonly type: string;
  readonly message: string;
  readonly location: string;
  readonly count: number;
  readonly firstSeenAt: string;
  readonly lastSeenAt: string;
  readonly resolvedAt: string | null;
  /** The latest occurrences' request ids, newest first: each finds its lines in the API's logs. */
  readonly latestRequestIds: readonly string[];
}

export interface AdminErrorOccurrence {
  readonly id: string;
  readonly occurredAt: string;
  readonly requestId: string | null;
  readonly message: string;
  readonly location: string;
  readonly stack: string | null;
  readonly commit: string | null;
}

/** `GET /admin/errors/{id}`. */
export interface AdminErrorGroupDetail {
  readonly group: AdminErrorGroup;
  readonly occurrences: readonly AdminErrorOccurrence[];
}

/** `GET /admin/errors` — groups, open or resolved, the most recently seen first. Admins only. */
export function listAdminErrors(
  client: ApiClient,
  query: PageQuery & { readonly status?: ErrorGroupStatus } = {},
  signal?: AbortSignal,
): Promise<Page<AdminErrorGroup>> {
  return client.request<Page<AdminErrorGroup>>('/admin/errors', { query: { ...query }, ...(signal ? { signal } : {}) });
}

/** `GET /admin/errors/{id}` — a group and its kept occurrences, the latest first. */
export function getAdminError(client: ApiClient, id: string, signal?: AbortSignal): Promise<AdminErrorGroupDetail> {
  return client.request<AdminErrorGroupDetail>(`/admin/errors/${encodeURIComponent(id)}`, signal ? { signal } : {});
}

/** `POST /admin/errors/{id}/resolve` — marks a group resolved; its next occurrence reopens it. */
export function resolveAdminError(client: ApiClient, id: string): Promise<AdminErrorGroup> {
  return client.request<AdminErrorGroup>(`/admin/errors/${encodeURIComponent(id)}/resolve`, { method: 'POST' });
}
