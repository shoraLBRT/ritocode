import { createContext, useContext } from 'react';
import type { ApiError, Me } from '../api';

/**
 * Who is using the application, as `GET /me` last said.
 *
 * Four states rather than a nullable user, because "not signed in" and "could not find out" call
 * for different screens: the first asks the visitor to sign in, the second offers to try again.
 */
export type Session =
  | { readonly status: 'loading' }
  | { readonly status: 'signedIn'; readonly user: Me; readonly signOut: () => Promise<void> }
  | { readonly status: 'signedOut' }
  | { readonly status: 'error'; readonly error: ApiError; readonly retry: () => void };

/** Loading until a provider says otherwise, so a component outside one never claims a user. */
export const SessionContext = createContext<Session>({ status: 'loading' });

export function useSession(): Session {
  return useContext(SessionContext);
}
