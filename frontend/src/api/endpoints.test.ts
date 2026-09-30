import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { listModules } from './endpoints';
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
});
