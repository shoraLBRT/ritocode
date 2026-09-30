import { useMemo } from 'react';
import type { ReactNode } from 'react';
import { getMe, useApiClient } from '../api';
import { useApiResource } from '../hooks/useApiResource';
import { SessionContext } from './SessionContext';
import type { Session } from './SessionContext';

/**
 * Asks `GET /me` once, when the application starts, and tells every screen the answer. A 401 is
 * the signed-out answer, not a failure: it is what the endpoint says to a visitor.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const client = useApiClient();
  const { state, reload } = useApiResource((signal) => getMe(client, signal), [client]);

  const session = useMemo<Session>(() => {
    switch (state.status) {
      case 'loading':
        return { status: 'loading' };
      case 'success':
        return { status: 'signedIn', user: state.data };
      case 'error':
        return state.error.isUnauthenticated ? { status: 'signedOut' } : { status: 'error', error: state.error, retry: reload };
    }
  }, [state, reload]);

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}
