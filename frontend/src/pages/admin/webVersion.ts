/**
 * The commit the web image was built from, as the `version.json` beside the pages says it (#297), or `null`
 * when the build was not told it or the file is not there — the development server has none.
 */
export async function readWebCommit(signal?: AbortSignal): Promise<string | null> {
  try {
    const response = await fetch(new URL('/version.json', window.location.origin), {
      headers: { Accept: 'application/json' },
      cache: 'no-store',
      ...(signal ? { signal } : {}),
    });
    if (!response.ok) {
      return null;
    }
    const body: unknown = await response.json();
    const commit = typeof body === 'object' && body !== null ? (body as { commit?: unknown }).commit : null;
    return typeof commit === 'string' && commit.length > 0 ? commit : null;
  } catch {
    // Not JSON (a development server's fallback page) or not reachable: unknown, not an error.
    return null;
  }
}
