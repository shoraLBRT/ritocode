import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../api';

/**
 * Loads one thing from the API and reports exactly which of three states it is in.
 *
 * A discriminated union rather than the usual `{ data, loading, error }` triple, because that
 * triple has states that cannot happen — loading and errored at once, settled with neither a
 * value nor a reason — and a screen written against it has to decide what those mean. Here it
 * cannot: `status` says which members exist.
 *
 * Deliberately not a caching layer. Nothing in the slice re-reads the same resource often
 * enough to pay for one, and a cache is a decision about staleness that the screens which
 * arrive in stage 6 are better placed to make.
 */

export type ApiResourceState<T> =
  | { readonly status: 'loading' }
  | { readonly status: 'success'; readonly data: T }
  | { readonly status: 'error'; readonly error: ApiError };

export interface ApiResource<T> {
  readonly state: ApiResourceState<T>;
  /** Runs the load again — what a "Try again" button calls. */
  readonly reload: () => void;
}

/**
 * A settled request, together with the inputs it was issued for.
 *
 * Recording the inputs is what makes `loading` derivable during render rather than something an
 * effect has to switch on: once the deps change, this snapshot no longer matches them and the
 * very first render reports `loading`. Without it there is a committed frame showing the
 * previous problem's data under the new problem's url.
 */
interface Settled<T> {
  readonly deps: readonly unknown[];
  readonly attempt: number;
  readonly state: ApiResourceState<T>;
}

/**
 * @param load Performs the request. It receives an {@link AbortSignal} and must pass it on, or
 * a superseded request will still be in flight when a newer one settles.
 * @param deps Says when the request has changed, with the same meaning as a `useEffect`
 * dependency list: a `load` closing over a changing value puts that value in here. Its length
 * must be stable across renders, as React requires of any dependency list.
 */
export function useApiResource<T>(load: (signal: AbortSignal) => Promise<T>, deps: readonly unknown[]): ApiResource<T> {
  const [attempt, setAttempt] = useState(0);
  const [settled, setSettled] = useState<Settled<T> | null>(null);

  // `load` is a fresh closure on every render, so it cannot be an effect dependency without
  // reloading forever — and it cannot be memoised on `deps` either, because a caller that
  // happens to pass a stable function reference would then produce a stable memo whatever
  // `deps` did, and the reload would silently never happen. So the latest closure is held in a
  // ref and `deps` alone decides when to run. The ref is refreshed in an effect declared first,
  // which React runs before the loading effect below on the same commit.
  const loadRef = useRef(load);
  useEffect(() => {
    loadRef.current = load;
  });

  useEffect(() => {
    const controller = new AbortController();
    const issuedFor = { deps, attempt };

    loadRef.current(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) {
          setSettled({ ...issuedFor, state: { status: 'success', data } });
        }
      },
      (cause: unknown) => {
        // An abort is this effect's own cleanup, not a failure to show anyone.
        if (controller.signal.aborted) {
          return;
        }

        setSettled({
          ...issuedFor,
          state: {
            status: 'error',
            error: cause instanceof ApiError ? cause : new ApiError('Something went wrong.', 'network', { cause }),
          },
        });
      },
    );

    return () => {
      controller.abort();
    };
    // The caller's `deps` is the contract for when the request has changed, exactly as it is
    // for `useEffect` itself, so the rule cannot see what it needs to check.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, attempt]);

  const reload = useCallback(() => {
    setAttempt((previous) => previous + 1);
  }, []);

  const state: ApiResourceState<T> = isCurrent(settled, deps, attempt)
    ? settled.state
    : { status: 'loading' };

  return { state, reload };
}

function isCurrent<T>(
  settled: Settled<T> | null,
  deps: readonly unknown[],
  attempt: number,
): settled is Settled<T> {
  return settled !== null && settled.attempt === attempt && shallowEqual(settled.deps, deps);
}

/** `Object.is` per element, which is what a `useEffect` dependency list compares by. */
function shallowEqual(left: readonly unknown[], right: readonly unknown[]): boolean {
  return left.length === right.length && left.every((value, index) => Object.is(value, right[index]));
}
