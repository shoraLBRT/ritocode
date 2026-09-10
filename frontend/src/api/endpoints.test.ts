import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { getProblem, listModules, listProblems } from './endpoints';
import { exampleProblem, exampleProblemDetail, jsonResponse, pageOf } from '../test/responses';

function stub(response: Response) {
  const fetchStub = vi.fn<typeof globalThis.fetch>().mockResolvedValue(response);
  return { fetchStub, client: new ApiClient({ baseUrl: 'http://api.test/api/v1', fetch: fetchStub }) };
}

describe('endpoints', () => {
  it('lists problems in the page envelope', async () => {
    const { client, fetchStub } = stub(jsonResponse(pageOf([exampleProblem])));

    const page = await listProblems(client, { page: 2, pageSize: 5 });

    expect(fetchStub.mock.calls[0]?.[0]).toBe('http://api.test/api/v1/problems?page=2&pageSize=5');
    expect(page.items).toHaveLength(1);
    expect(page.items[0]?.slug).toBe('example-order-total');
    expect(page.hasPreviousPage).toBe(false);
  });

  it('asks for no paging parameters when the caller gave none', async () => {
    const { client, fetchStub } = stub(jsonResponse(pageOf([])));

    await listProblems(client);

    expect(fetchStub.mock.calls[0]?.[0]).toBe('http://api.test/api/v1/problems');
  });

  it('escapes a slug so a path separator in it cannot change the route', async () => {
    const { client, fetchStub } = stub(jsonResponse(exampleProblemDetail));

    await getProblem(client, 'a/../b');

    expect(fetchStub.mock.calls[0]?.[0]).toBe('http://api.test/api/v1/problems/a%2F..%2Fb');
  });

  it('reads a problem detail, description included', async () => {
    const { client } = stub(jsonResponse(exampleProblemDetail));

    const detail = await getProblem(client, 'example-order-total');

    expect(detail.description).toContain('Order total');
    expect(detail.problemVersionId).toBe(exampleProblem.problemVersionId);
  });

  it('reads the module list', async () => {
    const { client } = stub(jsonResponse([{ name: 'Problems', routePrefix: '/problems' }]));

    await expect(listModules(client)).resolves.toEqual([{ name: 'Problems', routePrefix: '/problems' }]);
  });
});
