import { describe, expect, it, vi } from 'vitest';
import { ApiClient, CSRF_HEADER, csrfTokenFrom } from './client';
import { ApiError } from './errors';
import { jsonResponse, problemResponse, TEST_REQUEST_ID } from '../test/responses';

function clientWith(fetchStub: typeof globalThis.fetch, baseUrl = 'http://api.test/api/v1') {
  return new ApiClient({ baseUrl, fetch: fetchStub });
}

describe('ApiClient', () => {
  it('joins the base url and the path without doubling the slash', async () => {
    const fetchStub = vi.fn<typeof globalThis.fetch>().mockResolvedValue(jsonResponse({ ok: true }));

    await clientWith(fetchStub, 'http://api.test/api/v1/').request('/problems');

    expect(fetchStub.mock.calls[0]?.[0]).toBe('http://api.test/api/v1/problems');
  });

  it('omits query parameters that were not asked for', async () => {
    const fetchStub = vi.fn<typeof globalThis.fetch>().mockResolvedValue(jsonResponse({ ok: true }));

    await clientWith(fetchStub).request('/problems', { query: { page: 2, pageSize: undefined } });

    // `pageSize=undefined` would be a string the API rejects as out of range, which is a
    // failure the user cannot act on and never asked for.
    expect(fetchStub.mock.calls[0]?.[0]).toBe('http://api.test/api/v1/problems?page=2');
  });

  it('reads the ADR 0003 error envelope into a branchable code', async () => {
    const fetchStub = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValue(problemResponse(404, 'problem_not_found', 'Problem does not exist.'));

    const error = await clientWith(fetchStub)
      .request('/problems/nope')
      .catch((cause: unknown) => cause);

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.kind).toBe('problem');
    expect(apiError.code).toBe('problem_not_found');
    expect(apiError.status).toBe(404);
    expect(apiError.isNotFound).toBe(true);
    expect(apiError.message).toBe('Problem does not exist.');
    expect(apiError.requestId).toBe(TEST_REQUEST_ID);
  });

  it('carries validation field errors through', async () => {
    const fetchStub = vi.fn<typeof globalThis.fetch>().mockResolvedValue(
      problemResponse(400, 'validation_failed', 'Validation failed.', {
        pageSize: ['Page size must be between 1 and 100.'],
      }),
    );

    const error = (await clientWith(fetchStub)
      .request('/problems')
      .catch((cause: unknown) => cause)) as ApiError;

    expect(error.isValidation).toBe(true);
    expect(error.fieldErrors?.pageSize).toEqual(['Page size must be between 1 and 100.']);
  });

  it('falls back to an http error when the body is not the documented envelope', async () => {
    // A proxy's own 502 page, for instance: there is a status but nothing to branch on.
    const fetchStub = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValue(new Response('<html>gateway</html>', { status: 502 }));

    const error = (await clientWith(fetchStub)
      .request('/problems')
      .catch((cause: unknown) => cause)) as ApiError;

    expect(error.kind).toBe('http');
    expect(error.status).toBe(502);
    expect(error.code).toBeUndefined();
  });

  it('reports an unreachable server as a network failure rather than a status', async () => {
    const fetchStub = vi.fn<typeof globalThis.fetch>().mockRejectedValue(new TypeError('Failed to fetch'));

    const error = (await clientWith(fetchStub)
      .request('/problems')
      .catch((cause: unknown) => cause)) as ApiError;

    expect(error.kind).toBe('network');
    expect(error.status).toBeUndefined();
  });

  it('rethrows an abort untouched so a superseded request is not shown as a failure', async () => {
    const fetchStub = vi
      .fn<typeof globalThis.fetch>()
      .mockRejectedValue(new DOMException('The operation was aborted.', 'AbortError'));

    const error = await clientWith(fetchStub)
      .request('/problems')
      .catch((cause: unknown) => cause);

    expect(error).toBeInstanceOf(DOMException);
    expect(error).not.toBeInstanceOf(ApiError);
  });

  it('returns undefined for a 204 rather than failing to parse an empty body', async () => {
    const fetchStub = vi.fn<typeof globalThis.fetch>().mockResolvedValue(new Response(null, { status: 204 }));

    await expect(clientWith(fetchStub).request('/things', { method: 'DELETE' })).resolves.toBeUndefined();
  });

  it('reports a malformed success body as an http failure, not as data', async () => {
    const fetchStub = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValue(new Response('{not json', { status: 200 }));

    const error = (await clientWith(fetchStub)
      .request('/problems')
      .catch((cause: unknown) => cause)) as ApiError;

    expect(error.kind).toBe('http');
  });

  it('sends a JSON body and content type only when there is a body', async () => {
    // A fresh Response per call: a body can only be read once, so reusing one instance would
    // fail the second request for a reason that has nothing to do with the client.
    const fetchStub = vi
      .fn<typeof globalThis.fetch>()
      .mockImplementation(() => Promise.resolve(jsonResponse({ ok: true })));

    await clientWith(fetchStub).request('/things', { method: 'POST', body: { name: 'x' } });
    const withBody = fetchStub.mock.calls[0]?.[1];
    expect(withBody?.body).toBe('{"name":"x"}');

    await clientWith(fetchStub).request('/things');
    const withoutBody = fetchStub.mock.calls[1]?.[1];
    expect(withoutBody?.body).toBeUndefined();
  });

  it('repeats the session CSRF token on a state-changing request, and only there', async () => {
    const fetchStub = vi
      .fn<typeof globalThis.fetch>()
      .mockImplementation(() => Promise.resolve(jsonResponse({ ok: true })));
    const client = new ApiClient({ baseUrl: 'http://api.test/api/v1', fetch: fetchStub, csrfToken: () => 'the-token' });

    await client.request('/attempts', { method: 'POST', body: {} });
    await client.request('/attempts/1', { method: 'patch', body: {} });
    await client.request('/me');

    const headers = fetchStub.mock.calls.map((call) => call[1]?.headers as Record<string, string>);
    expect(headers[0]?.[CSRF_HEADER]).toBe('the-token');
    expect(headers[1]?.[CSRF_HEADER]).toBe('the-token');
    expect(headers[2]?.[CSRF_HEADER]).toBeUndefined();
  });

  it('sends no CSRF header when there is no session', async () => {
    const fetchStub = vi
      .fn<typeof globalThis.fetch>()
      .mockImplementation(() => Promise.resolve(jsonResponse({ ok: true })));
    const client = new ApiClient({ baseUrl: 'http://api.test/api/v1', fetch: fetchStub, csrfToken: () => undefined });

    await client.request('/attempts', { method: 'POST', body: {} });

    expect((fetchStub.mock.calls[0]?.[1]?.headers as Record<string, string>)[CSRF_HEADER]).toBeUndefined();
  });

  it('sends the session cookie with every request, the API being another origin in development', async () => {
    const fetchStub = vi.fn<typeof globalThis.fetch>().mockResolvedValue(jsonResponse({ ok: true }));

    await clientWith(fetchStub).request('/me');

    expect(fetchStub.mock.calls[0]?.[1]?.credentials).toBe('include');
  });

  it('resolves a path outside the versioned API against the host', async () => {
    const fetchStub = vi.fn<typeof globalThis.fetch>().mockResolvedValue(new Response(null, { status: 204 }));

    await clientWith(fetchStub).request('/auth/logout', { method: 'POST', outsideApi: true });

    expect(fetchStub.mock.calls[0]?.[0]).toBe('http://api.test/auth/logout');
  });

  it('keeps the host empty when the API is on the page origin', () => {
    expect(clientWith(vi.fn(), '/api/v1').hostUrl).toBe('');
    expect(clientWith(vi.fn(), 'http://localhost:5199/api/v1/').hostUrl).toBe('http://localhost:5199');
  });

  it('finds the token among the page cookies', () => {
    expect(csrfTokenFrom('theme=dark; __Host-ritocode-csrf=abc_-123; other=1')).toBe('abc_-123');
    expect(csrfTokenFrom('theme=dark')).toBeUndefined();
    expect(csrfTokenFrom('__Host-ritocode-csrf=')).toBeUndefined();
    expect(csrfTokenFrom('')).toBeUndefined();
  });
});
