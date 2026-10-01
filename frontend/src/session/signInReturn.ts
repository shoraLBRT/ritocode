import { createContext, useContext } from 'react';

/**
 * What the browser brings back from a sign-in round trip (docs/SPEC.md §4.6), and how a page asks
 * for it.
 *
 * The server returns the browser to the local path it was given (`/auth/login/{provider}?returnUrl=`),
 * adding `signInError` when nobody was signed in. A task asks to be checked on return by giving its
 * own path with {@link CHECK_PARAM}: the flag travels in the address rather than in storage, so a
 * page that finds the flag but no saved answer knows the browser lost it and can say so.
 *
 * Both are read once, when the application loads — a return is a full page load — and taken out of
 * the address, so a reload or a later visit does not act on them again.
 */
export const CHECK_PARAM = 'check';

export const SIGN_IN_ERROR_PARAM = 'signInError';

/** The codes the server returns with (#7); anything else reads as a provider failure. */
export const SIGN_IN_ERRORS = ['email_unverified', 'provider_already_linked', 'provider_failed'] as const;

export type SignInError = (typeof SIGN_IN_ERRORS)[number];

export interface SignInReturn {
  /** Why the sign-in reached nobody, or null when it did not fail. */
  readonly error: SignInError | null;
  /** The path that asked to be checked on return; null when none did, or the sign-in failed. */
  readonly checkRequestedAt: string | null;
}

/** The return address that asks the page at `pathname` to check its saved answer once signed in. */
export function checkReturnPath(pathname: string): string {
  return `${pathname}?${CHECK_PARAM}=1`;
}

export function readSignInReturn(pathname: string, search: string): SignInReturn {
  const params = new URLSearchParams(search);
  const code = params.get(SIGN_IN_ERROR_PARAM);
  const error = code === null ? null : (SIGN_IN_ERRORS.find((each) => each === code) ?? 'provider_failed');

  // A sign-in that failed signed nobody in, so there is nothing to check: the answer is restored and
  // waits for the learner, who can try another provider.
  return { error, checkRequestedAt: error === null && params.has(CHECK_PARAM) ? pathname : null };
}

/** `search` without the sign-in return's parameters, as `?a=b` or empty. */
export function withoutSignInReturn(search: string): string {
  const params = new URLSearchParams(search);
  params.delete(CHECK_PARAM);
  params.delete(SIGN_IN_ERROR_PARAM);
  return params.size > 0 ? `?${params.toString()}` : '';
}

/** Whether the page at a path was asked to check on return, and how it says it has acted on it. */
export interface CheckRequest {
  readonly requested: boolean;
  readonly consume: () => void;
}

export interface SignInReturnState {
  readonly checkRequestedAt: string | null;
  readonly consumeCheck: () => void;
}

export const SignInReturnContext = createContext<SignInReturnState>({ checkRequestedAt: null, consumeCheck: () => undefined });

export function useCheckRequest(pathname: string): CheckRequest {
  const { checkRequestedAt, consumeCheck } = useContext(SignInReturnContext);
  return { requested: checkRequestedAt === pathname, consume: consumeCheck };
}
