import type { ApiError } from './errors';

/**
 * Whether the API answered the last request it was sent (#298).
 *
 * The edge answers 503 with its maintenance page when the API cannot be reached or a release is
 * migrating (deploy/Caddyfile); a 502 or 504 is the same trouble from the other side of a proxy,
 * and a fetch that throws never reached anything. Every such failure marks the API unreachable and
 * any other answer — an error the API explained included — marks it reachable again. The page shows
 * one banner while it is unreachable instead of each panel explaining the same outage.
 */
export class ApiAvailability {
  #reachable = true;
  readonly #listeners = new Set<() => void>();

  get reachable(): boolean {
    return this.#reachable;
  }

  /** In the shape `useSyncExternalStore` takes. */
  readonly subscribe = (listener: () => void): (() => void) => {
    this.#listeners.add(listener);
    return () => {
      this.#listeners.delete(listener);
    };
  };

  readonly getSnapshot = (): boolean => this.#reachable;

  report(reachable: boolean): void {
    if (reachable === this.#reachable) {
      return;
    }

    this.#reachable = reachable;
    for (const listener of this.#listeners) {
      listener();
    }
  }
}

/** The statuses that say the API is not there, rather than that it answered. */
const UNAVAILABLE_STATUSES = new Set([502, 503, 504]);

export function isUnavailableStatus(status: number): boolean {
  return UNAVAILABLE_STATUSES.has(status);
}

/** A failure that is the outage itself, which the banner already explains. */
export function isUnavailable(error: ApiError): boolean {
  return error.kind === 'network' || (error.kind !== 'problem' && error.status !== undefined && isUnavailableStatus(error.status));
}
