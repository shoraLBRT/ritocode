/**
 * Cloudflare Turnstile, the captcha in front of `POST /feedback` (ADR 0019). Used only when the build
 * names a site key (`VITE_TURNSTILE_SITE_KEY`); without one the API asks for no captcha either.
 */

const SCRIPT_URL = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';

interface TurnstileOptions {
  sitekey: string;
  callback: (token: string) => void;
  'expired-callback': () => void;
  'error-callback': () => void;
}

interface TurnstileApi {
  render(container: HTMLElement, options: TurnstileOptions): string;
  reset(widgetId: string): void;
  remove(widgetId: string): void;
}

declare global {
  interface Window {
    turnstile?: TurnstileApi;
  }
}

/** The site key from the build, or null for no captcha. */
export function resolveTurnstileSiteKey(env: Readonly<Record<string, string | undefined>>): string | null {
  const key = env.VITE_TURNSTILE_SITE_KEY?.trim() ?? '';
  return key === '' ? null : key;
}

let loading: Promise<TurnstileApi> | null = null;

function loadTurnstile(): Promise<TurnstileApi> {
  loading ??= new Promise<TurnstileApi>((resolve, reject) => {
    const script = document.createElement('script');
    script.src = SCRIPT_URL;
    script.async = true;
    script.onload = () => {
      if (window.turnstile) {
        resolve(window.turnstile);
      } else {
        reject(new Error('Turnstile loaded without its API.'));
      }
    };
    script.onerror = () => {
      loading = null;
      reject(new Error('Turnstile could not be loaded.'));
    };
    document.head.append(script);
  });
  return loading;
}

/** A mounted challenge: `reset` runs it again after a send, since a token is single-use. */
export interface TurnstileWidget {
  reset(): void;
  remove(): void;
}

/** Renders the challenge into `container`; `onToken` receives each token, and null when it lapses. */
export function mountTurnstile(container: HTMLElement, siteKey: string, onToken: (token: string | null) => void): TurnstileWidget {
  let widgetId: string | null = null;
  let removed = false;
  const lapse = () => {
    onToken(null);
  };

  loadTurnstile().then((api) => {
    if (!removed) {
      widgetId = api.render(container, { sitekey: siteKey, callback: onToken, 'expired-callback': lapse, 'error-callback': lapse });
    }
  }, lapse);

  return {
    reset() {
      if (widgetId !== null) {
        window.turnstile?.reset(widgetId);
      }
    },
    remove() {
      removed = true;
      if (widgetId !== null) {
        window.turnstile?.remove(widgetId);
      }
    },
  };
}
