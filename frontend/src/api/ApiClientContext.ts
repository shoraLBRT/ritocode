import { createContext, useContext } from 'react';
import type { ApiClient } from './client';

/**
 * The client is passed through context rather than imported as a module singleton, so a test
 * renders a component against a stubbed `fetch` without reaching into module state, and so a
 * future host that talks to two backends is a second provider rather than a rewrite.
 */
const ApiClientContext = createContext<ApiClient | null>(null);

export { ApiClientContext };

export function useApiClient(): ApiClient {
  const client = useContext(ApiClientContext);

  if (client === null) {
    throw new Error('useApiClient was called outside an <ApiClientProvider>.');
  }

  return client;
}
