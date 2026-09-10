import type { ReactNode } from 'react';
import { ApiClientContext } from './ApiClientContext';
import type { ApiClient } from './client';

export function ApiClientProvider({ client, children }: { client: ApiClient; children: ReactNode }) {
  return <ApiClientContext value={client}>{children}</ApiClientContext>;
}
