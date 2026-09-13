import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import {
  getProblem,
  getWorkspace,
  getWorkspaceFile,
  listModules,
  listProblems,
  listWorkspaceFiles,
  openWorkspace,
} from './endpoints';
import {
  exampleFile,
  exampleFileTree,
  exampleProblem,
  exampleProblemDetail,
  exampleWorkspace,
  jsonResponse,
  pageOf,
  problemResponse,
} from '../test/responses';

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

describe('workspace endpoints', () => {
  it('opens a workspace by posting the version id, and nothing about the user', async () => {
    const { client, fetchStub } = stub(jsonResponse(exampleWorkspace, 201));

    const workspace = await openWorkspace(client, exampleProblem.problemVersionId);

    const [url, init] = fetchStub.mock.calls[0] ?? [];
    expect(url).toBe('http://api.test/api/v1/workspaces');
    expect(init?.method).toBe('POST');
    expect(init?.body).toBe(JSON.stringify({ problemVersionId: exampleProblem.problemVersionId }));
    expect(workspace).toEqual(exampleWorkspace);
  });

  it('reads a workspace back by its id', async () => {
    const { client, fetchStub } = stub(jsonResponse(exampleWorkspace));

    await getWorkspace(client, exampleWorkspace.id);

    expect(fetchStub.mock.calls[0]?.[0]).toBe(`http://api.test/api/v1/workspaces/${exampleWorkspace.id}`);
  });

  it('lists the whole file tree, not a page of it', async () => {
    const { client, fetchStub } = stub(jsonResponse(exampleFileTree));

    const tree = await listWorkspaceFiles(client, exampleWorkspace.id);

    expect(fetchStub.mock.calls[0]?.[0]).toBe(`http://api.test/api/v1/workspaces/${exampleWorkspace.id}/files`);
    expect(tree.files.map((file) => file.path)).toEqual(['Orders.csproj', 'src/OrderTotal.cs']);
  });

  it('sends a file path as an escaped query value, never as route segments', async () => {
    const { client, fetchStub } = stub(jsonResponse(exampleFile));

    await getWorkspaceFile(client, exampleWorkspace.id, 'src/../OrderTotal.cs');

    expect(fetchStub.mock.calls[0]?.[0]).toBe(
      `http://api.test/api/v1/workspaces/${exampleWorkspace.id}/files/content?path=src%2F..%2FOrderTotal.cs`,
    );
  });

  it('reads a file with its line endings intact', async () => {
    const { client } = stub(jsonResponse(exampleFile));

    const file = await getWorkspaceFile(client, exampleWorkspace.id, exampleFile.path);

    expect(file.content).toBe('namespace Orders;\r\n');
  });

  it('surfaces a refused path as validation_failed on path', async () => {
    const { client } = stub(
      problemResponse(400, 'validation_failed', 'The path is not a workspace file path.', {
        path: ["Must be relative and use '/', with no empty, '.' or '..' segment."],
      }),
    );

    await expect(getWorkspaceFile(client, exampleWorkspace.id, '../problem.yaml')).rejects.toMatchObject({
      status: 400,
      code: 'validation_failed',
    });
  });
});
