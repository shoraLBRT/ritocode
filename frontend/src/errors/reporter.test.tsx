import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Mock } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import type { RouteObject } from 'react-router';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse } from '../test/responses';
import { MAX_REPORTS_PER_PAGE, describe as describeError, installErrorReporter, reportError } from './reporter';
import { RouteErrorPage } from './RouteErrorPage';

/** The browser's error reporter and the route error page (#294). */

function accepted(requestId: string): Response {
  return new Response(null, { status: 202, headers: { 'X-Request-Id': requestId } });
}

/** The body of the `index`th report a stub was sent, parsed. */
function bodyOf(stub: Mock<typeof globalThis.fetch>, index = 0): Record<string, unknown> {
  const body = stub.mock.calls[index]?.[1]?.body;
  return typeof body === 'string' ? (JSON.parse(body) as Record<string, unknown>) : {};
}

let uninstall: (() => void) | null = null;

afterEach(() => {
  uninstall?.();
  uninstall = null;
});

describe('the error reporter', () => {
  it('sends the error, the page path and the commit, and answers the report request id', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(accepted('req-1'));
    uninstall = installErrorReporter('http://api.test/api/v1/', { fetch, commit: () => Promise.resolve('abc123') });
    window.history.replaceState(null, '', '/tasks/demo?draft=secret#answer');

    const id = await reportError(new TypeError('x is undefined'));

    expect(id).toBe('req-1');
    expect(fetch.mock.calls[0]?.[0]).toBe('http://api.test/api/v1/client-errors');
    expect(fetch.mock.calls[0]?.[1]?.method).toBe('POST');
    const body = bodyOf(fetch);
    expect(body).toMatchObject({ type: 'TypeError', message: 'x is undefined', page: '/tasks/demo', commit: 'abc123' });
    expect(JSON.stringify(body)).not.toContain('secret');
  });

  it('sends the same error once per page load, and at most a few in all', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(accepted('req'));
    uninstall = installErrorReporter('/api/v1', { fetch, commit: () => Promise.resolve(null) });

    await reportError(new Error('same'));
    await reportError(new Error('same'));
    expect(fetch).toHaveBeenCalledTimes(1);

    for (let i = 0; i < MAX_REPORTS_PER_PAGE + 5; i++) {
      await reportError(new Error(`other ${String(i)}`));
    }
    expect(fetch).toHaveBeenCalledTimes(MAX_REPORTS_PER_PAGE);
  });

  it('catches uncaught errors and rejections', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(accepted('req'));
    uninstall = installErrorReporter('/api/v1', { fetch, commit: () => Promise.resolve(null) });

    window.dispatchEvent(new ErrorEvent('error', { error: new RangeError('out of range'), message: 'out of range' }));
    const rejection = new Event('unhandledrejection') as PromiseRejectionEvent;
    Object.defineProperty(rejection, 'reason', { value: 'plain string reason' });
    window.dispatchEvent(rejection);

    await waitFor(() => {
      expect(fetch).toHaveBeenCalledTimes(2);
    });
    expect(bodyOf(fetch, 0)).toMatchObject({ type: 'RangeError', message: 'out of range' });
    expect(bodyOf(fetch, 1)).toMatchObject({ type: 'Error', message: 'plain string reason', stack: null });
  });

  it('never throws when the report cannot be sent', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockRejectedValue(new TypeError('Failed to fetch'));
    uninstall = installErrorReporter('/api/v1', { fetch, commit: () => Promise.resolve(null) });

    await expect(reportError(new Error('offline'))).resolves.toBeNull();
  });

  it('sends nothing until it is installed', async () => {
    await expect(reportError(new Error('early'))).resolves.toBeNull();
  });

  it('describes anything thrown', () => {
    expect(describeError({ code: 42 })).toEqual({ type: 'Error', message: '{"code":42}', stack: null });
  });
});

function Boom(): never {
  throw new Error('render failed');
}

describe('the route error page', () => {
  it('shows what happened and the report number when a page throws while rendering', async () => {
    const report = vi.fn<typeof globalThis.fetch>().mockResolvedValue(accepted('0f1e2d3c'));
    uninstall = installErrorReporter('http://api.test/api/v1', { fetch: report, commit: () => Promise.resolve(null) });
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);

    const table: RouteObject[] = [{ path: '/', ErrorBoundary: RouteErrorPage, children: [{ path: 'boom', Component: Boom }] }];
    renderApp(vi.fn<typeof globalThis.fetch>().mockResolvedValue(jsonResponse({})), '/boom', table);

    expect(await screen.findByRole('heading', { level: 1, name: ru.errorPage.title })).toBeInTheDocument();
    expect(await screen.findByText(ru.errorPage.requestId.replace('{id}', '0f1e2d3c'))).toBeInTheDocument();
    expect(bodyOf(report)).toMatchObject({ type: 'Error', message: 'render failed' });
  });

  it('shows the not-found page for an address the table has no route for', async () => {
    const table: RouteObject[] = [{ path: '/', ErrorBoundary: RouteErrorPage, children: [] }];
    renderApp(vi.fn<typeof globalThis.fetch>().mockResolvedValue(jsonResponse({})), '/nowhere', table);

    expect(await screen.findByRole('heading', { level: 1, name: ru.notFound.title })).toBeInTheDocument();
  });
});
