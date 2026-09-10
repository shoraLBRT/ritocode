import { REQUEST_ID_HEADER } from '../api';

/**
 * Response builders shaped like the ones the API actually sends.
 *
 * Tests stub `fetch` rather than run a mock server: the contract under test is what this client
 * does with a response, and a mock server would add a second implementation of the API to keep
 * in step with the first. The bodies here are copied from the verification table in
 * `docs/PROJECT_STATE.md`, so a change to the real contract shows up as a failing test here.
 */

export const TEST_REQUEST_ID = 'e2b1a7c0d4f34a5e9c1b2d3e4f5a6b70';

export function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      'Content-Type': 'application/json',
      [REQUEST_ID_HEADER]: TEST_REQUEST_ID,
    },
  });
}

/** The RFC 9457 body from ADR 0003, with the `code` and `requestId` extensions. */
export function problemResponse(
  status: number,
  code: string,
  detail: string,
  errors?: Record<string, string[]>,
): Response {
  return new Response(
    JSON.stringify({
      type: `https://ritocode.dev/errors/${code}`,
      title: 'Not found',
      status,
      detail,
      instance: '/api/v1/problems/x',
      code,
      requestId: TEST_REQUEST_ID,
      ...(errors ? { errors } : {}),
    }),
    {
      status,
      headers: {
        'Content-Type': 'application/problem+json',
        [REQUEST_ID_HEADER]: TEST_REQUEST_ID,
      },
    },
  );
}

export function pageOf<T>(items: T[], pageNumber = 1, pageSize = 20, totalItems = items.length) {
  const totalPages = pageSize <= 0 ? 0 : Math.ceil(totalItems / pageSize);
  return {
    items,
    pageNumber,
    pageSize,
    totalItems,
    totalPages,
    hasNextPage: pageNumber < totalPages,
    hasPreviousPage: pageNumber > 1,
  };
}

export const exampleProblem = {
  id: '0199a1d2-0000-7000-8000-000000000001',
  slug: 'example-order-total',
  title: 'Order total',
  difficulty: 'medium' as const,
  tags: ['refactoring', 'tests'],
  problemVersionId: '0199a1d2-0000-7000-8000-000000000002',
  version: 1,
  publishedAt: '2026-09-08T10:00:00Z',
};

export const exampleProblemDetail = {
  ...exampleProblem,
  description: '# Order total\n\nImprove the calculation.',
};
