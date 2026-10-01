import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { isLocalPath, listModules, signInUrl, signOut } from './endpoints';
import { jsonResponse } from '../test/responses';

function stub(response: Response) {
  const fetchStub = vi.fn<typeof globalThis.fetch>().mockResolvedValue(response);
  return { fetchStub, client: new ApiClient({ baseUrl: 'http://api.test/api/v1', fetch: fetchStub }) };
}

describe('endpoints', () => {
  it('reads the module list', async () => {
    const { client } = stub(jsonResponse([{ name: 'Problems', routePrefix: '/problems' }]));

    await expect(listModules(client)).resolves.toEqual([{ name: 'Problems', routePrefix: '/problems' }]);
  });

  it('addresses sign-in on the host, returning to a local path', () => {
    const { client } = stub(jsonResponse({}));

    expect(signInUrl(client, 'github', '/tasks/some-task?check=1')).toBe(
      'http://api.test/auth/login/github?returnUrl=%2Ftasks%2Fsome-task%3Fcheck%3D1',
    );
  });

  it('never asks the server to return anywhere but this site', () => {
    const { client } = stub(jsonResponse({}));

    for (const address of ['https://evil.test/', '//evil.test/', '/\\evil.test', 'tasks', '', '/tasks\n']) {
      expect(isLocalPath(address)).toBe(false);
      expect(signInUrl(client, 'google', address)).toBe('http://api.test/auth/login/google?returnUrl=%2F');
    }
    expect(isLocalPath('/')).toBe(true);
    expect(isLocalPath('/tasks/a?check=1')).toBe(true);
  });

  it('signs out through the host, outside the versioned API', async () => {
    const { client, fetchStub } = stub(new Response(null, { status: 204 }));

    await signOut(client);

    expect(fetchStub.mock.calls[0]?.[0]).toBe('http://api.test/auth/logout');
    expect(fetchStub.mock.calls[0]?.[1]?.method).toBe('POST');
  });
});
