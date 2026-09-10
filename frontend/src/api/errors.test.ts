import { describe, expect, it } from 'vitest';
import { ApiError, isApiError, parseProblemBody } from './errors';

describe('parseProblemBody', () => {
  it('reads a full envelope', () => {
    const parsed = parseProblemBody({
      type: 'https://ritocode.dev/errors/problem_not_found',
      title: 'Not found',
      status: 404,
      detail: 'Problem does not exist.',
      instance: '/api/v1/problems/x',
      code: 'problem_not_found',
      requestId: 'abc',
    });

    expect(parsed).toEqual({
      type: 'https://ritocode.dev/errors/problem_not_found',
      title: 'Not found',
      status: 404,
      detail: 'Problem does not exist.',
      instance: '/api/v1/problems/x',
      code: 'problem_not_found',
      requestId: 'abc',
    });
  });

  it('rejects a body with no code, because code is the only member a client may branch on', () => {
    expect(parseProblemBody({ title: 'Not found', status: 404 })).toBeNull();
  });

  it.each([['a string'], [42], [null], [['array']]])('rejects %p, which is not an object', (body) => {
    expect(parseProblemBody(body)).toBeNull();
  });

  it('drops members whose type is wrong rather than passing them through', () => {
    const parsed = parseProblemBody({ code: 'internal_error', status: 'five hundred', detail: 12 });

    expect(parsed).toEqual({ code: 'internal_error' });
  });

  it('keeps only string messages in the field error map', () => {
    const parsed = parseProblemBody({
      code: 'validation_failed',
      errors: { pageSize: ['too big', 7], page: 'not an array' },
    });

    expect(parsed?.errors).toEqual({ pageSize: ['too big'] });
  });
});

describe('ApiError', () => {
  it('exposes 401 as its own question, so the identity branch is not folded into "not found"', () => {
    const error = new ApiError('No.', 'problem', { status: 401, code: 'unauthenticated' });

    expect(error.isUnauthenticated).toBe(true);
    expect(error.isNotFound).toBe(false);
  });

  it('is recognised by isApiError and not confused with a plain Error', () => {
    expect(isApiError(new ApiError('x', 'network'))).toBe(true);
    expect(isApiError(new Error('x'))).toBe(false);
  });

  it('has no code when the server never sent one', () => {
    expect(new ApiError('x', 'network').code).toBeUndefined();
  });
});
