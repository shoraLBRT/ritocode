/**
 * Where the API lives.
 *
 * Read from `VITE_API_BASE_URL` at build time, defaulting to the port `scripts/dev-up` and the
 * verification section of `PROJECT_STATE.md` both use. It is a build-time value because Vite
 * inlines it; a deployment that needs to point at a different host rebuilds, which is the
 * trade the rest of the toolchain already makes.
 */
const DEFAULT_API_BASE_URL = 'http://127.0.0.1:5199/api/v1';

export function resolveApiBaseUrl(env: Readonly<Record<string, string | undefined>>): string {
  const configured = env.VITE_API_BASE_URL?.trim();
  return configured === undefined || configured === '' ? DEFAULT_API_BASE_URL : configured;
}

export { DEFAULT_API_BASE_URL };
