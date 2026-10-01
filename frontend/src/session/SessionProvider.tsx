import { useCallback, useEffect, useMemo } from 'react';
import type { ReactNode } from 'react';
import { takeSignInStarted, track } from '../analytics';
import { getMe, signOut, useApiClient } from '../api';
import { useApiResource } from '../hooks/useApiResource';
import { SessionContext } from './SessionContext';
import type { Session } from './SessionContext';

/**
 * Asks `GET /me` once, when the application starts, and tells every screen the answer. A 401 is
 * the signed-out answer, not a failure: it is what the endpoint says to a visitor.
 *
 * Signing in is a round trip through the provider that reloads the page, so it needs nothing here.
 * Signing out ends the session on the server, then asks `/me` again — every screen then sees
 * whoever the server says is left, which outside development is nobody.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const client = useApiClient();
  const { state, reload } = useApiResource((signal) => getMe(client, signal), [client]);

  const endSession = useCallback(async () => {
    await signOut(client);
    reload();
  }, [client, reload]);

  const session = useMemo<Session>(() => {
    switch (state.status) {
      case 'loading':
        return { status: 'loading' };
      case 'success':
        return { status: 'signedIn', user: state.data, signOut: endSession };
      case 'error':
        return state.error.isUnauthenticated ? { status: 'signedOut' } : { status: 'error', error: state.error, retry: reload };
    }
  }, [state, reload, endSession]);

  // Back from a provider: signed in, the sign-in is counted (SPEC §8); signed out, it failed, and the
  // note is dropped all the same.
  useEffect(() => {
    if (session.status === 'signedIn' || session.status === 'signedOut') {
      const provider = takeSignInStarted();

      if (provider !== null && session.status === 'signedIn') {
        track('sign-in-completed', { provider });
      }
    }
  }, [session.status]);

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}
