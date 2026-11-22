import { useSyncExternalStore } from 'react';

// Nothing changes it after the first render, so there is nothing to follow.
const unsubscribe = () => undefined;
const subscribe = () => unsubscribe;

/**
 * Whether the page is past its first render over prerendered markup (#310): `false` while React
 * hydrates — and while rendering to a string — so that render matches the markup, then `true`; a
 * page rendered afresh is `true` from its first render. What the prerender cannot know — today's
 * date, the browser's choices — waits for it.
 */
export function useHydrated(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false,
  );
}
