/**
 * Umami: how anyone uses the site, signed in or not (docs/SPEC.md §8). Self-hosted and cookie-less,
 * so it needs no consent banner. The script is added only when the build names one; otherwise every
 * `track` is a no-op, and nothing about analytics reaches the page at all.
 *
 * No event carries anything about a person: what was done, and at most a task's or a card's slug, a
 * difficulty or a provider's name — never a user id, an address or anything the visitor typed.
 */

/** The events of SPEC §8, by the names Umami shows them under. */
export type AnalyticsEvent =
  | 'task-opened'
  | 'step-2-reached'
  | 'check-signed-out'
  | 'sign-in-completed'
  | 'attempt-submitted'
  | 'signal-sent'
  | 'catalogue-search';

export type AnalyticsData = Readonly<Record<string, string | number>>;

/** Where the tracker is served from and which site it counts for: Umami's script address and website id. */
export interface AnalyticsConfig {
  readonly scriptUrl: string;
  readonly websiteId: string;
}

interface UmamiTracker {
  track(event: string, data?: AnalyticsData): unknown;
}

declare global {
  interface Window {
    umami?: UmamiTracker;
  }
}

/** Both values from the build, or no analytics: a half-configured tracker would count nothing. */
export function resolveAnalyticsConfig(env: Readonly<Record<string, string | undefined>>): AnalyticsConfig | null {
  const scriptUrl = env.VITE_UMAMI_SCRIPT_URL?.trim() ?? '';
  const websiteId = env.VITE_UMAMI_WEBSITE_ID?.trim() ?? '';

  return scriptUrl !== '' && websiteId !== '' ? { scriptUrl, websiteId } : null;
}

/**
 * Events sent before the script has loaded — a page that tracks on its first render, a sign-in
 * noticed as soon as `/me` answers — wait here and go once it has. Null when no script was added:
 * then there is nothing to wait for and an event is dropped.
 */
let pending: { event: AnalyticsEvent; data: AnalyticsData | undefined }[] | null = null;

/** Adds Umami's script to the page, once. Page views it counts by itself, route changes included. */
export function loadAnalytics(config: AnalyticsConfig, doc: Document = document): void {
  if (doc.querySelector('script[data-website-id]') !== null) {
    return;
  }

  pending ??= [];

  const script = doc.createElement('script');
  script.defer = true;
  script.src = config.scriptUrl;
  script.dataset.websiteId = config.websiteId;
  script.addEventListener('load', flush);
  doc.head.appendChild(script);
}

/** Counts `event`. Never throws: analytics must not break the thing it measures. */
export function track(event: AnalyticsEvent, data?: AnalyticsData): void {
  const umami = typeof window === 'undefined' ? undefined : window.umami;

  if (umami === undefined) {
    pending?.push({ event, data });
    return;
  }

  try {
    umami.track(event, data);
  } catch {
    // A tracker that fails is the tracker's problem, not the learner's.
  }
}

function flush(): void {
  const waiting = pending ?? [];
  pending = [];

  for (const { event, data } of waiting) {
    track(event, data);
  }
}

/** For tests: forget a script added and the events waiting for it. */
export function resetAnalytics(): void {
  pending = null;
}

/**
 * A sign-in is a round trip through the provider and back, as a new page load. The provider chosen is
 * noted in the tab before leaving, and taken once the page is back and knows whether it worked.
 */
const SIGN_IN_KEY = 'ritocode:sign-in-started';

export function noteSignInStarted(provider: string, storage: Storage | null = sessionStorageOrNull()): void {
  try {
    storage?.setItem(SIGN_IN_KEY, provider);
  } catch {
    // Storage refused: the sign-in still works, it is just not counted.
  }
}

/** The provider a sign-in started with in this tab, forgotten as it is read; null if none did. */
export function takeSignInStarted(storage: Storage | null = sessionStorageOrNull()): string | null {
  try {
    const provider = storage?.getItem(SIGN_IN_KEY) ?? null;
    storage?.removeItem(SIGN_IN_KEY);
    return provider;
  } catch {
    return null;
  }
}

function sessionStorageOrNull(): Storage | null {
  try {
    return typeof window === 'undefined' ? null : window.sessionStorage;
  } catch {
    return null;
  }
}
