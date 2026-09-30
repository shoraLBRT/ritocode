import type { ApiClient } from './client';
import type { ModuleInfo } from './types';

/**
 * One function per endpoint the API serves today. They hold no state and no fetching policy —
 * that belongs to the caller and to {@link useApiResource} — so each is a name, a path and a
 * return type, and adding an endpoint is adding a function here.
 */

/** `GET /meta/modules` — which modules this host composed in. Diagnostics, not a product surface. */
export function listModules(client: ApiClient, signal?: AbortSignal): Promise<ModuleInfo[]> {
  return client.request<ModuleInfo[]>('/meta/modules', { ...(signal ? { signal } : {}) });
}
