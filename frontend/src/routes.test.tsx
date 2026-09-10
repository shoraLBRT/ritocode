import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { renderApp } from './test/render';
import { exampleProblem, exampleProblemDetail, jsonResponse, pageOf, problemResponse } from './test/responses';

/**
 * The shell end to end: the layout renders, each route resolves, and the three states a screen
 * can be in all reach the page. The client is stubbed at `fetch`, so what is under test is this
 * application rather than the backend.
 */

/** `fetch` accepts a `Request` as well as a string, so the url is read rather than stringified. */
function urlOf(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }
  return input instanceof URL ? input.href : input.url;
}

function fetchStub(handler: (url: string) => Response) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => Promise.resolve(handler(urlOf(input))));
}

function firstRequestUrl(stub: ReturnType<typeof fetchStub>): string {
  const input = stub.mock.calls[0]?.[0];
  return input === undefined ? '' : urlOf(input);
}

describe('the application shell', () => {
  it('renders the layout chrome around every route', async () => {
    renderApp(fetchStub(() => jsonResponse([])));

    expect(screen.getByRole('navigation', { name: 'Primary' })).toBeInTheDocument();
    expect(screen.getByRole('main')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByRole('heading', { level: 1, name: 'Ritocode' })).toBeInTheDocument();
    });
  });

  it('shows the loading state before the first response arrives', () => {
    renderApp(vi.fn<typeof globalThis.fetch>().mockReturnValue(new Promise<Response>(() => undefined)));

    expect(screen.getByRole('status')).toHaveTextContent('Checking the API…');
  });

  it('shows the failure panel when the API cannot be reached', async () => {
    const stub = vi.fn<typeof globalThis.fetch>().mockRejectedValue(new TypeError('Failed to fetch'));
    renderApp(stub);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Check that the backend is running.');
    });
  });

  it('lists problems on /problems', async () => {
    renderApp(fetchStub(() => jsonResponse(pageOf([exampleProblem]))), '/problems');

    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Order total' })).toBeInTheDocument();
    });
    expect(screen.getByText('medium')).toBeInTheDocument();
  });

  it('says a catalog with no published problems is empty, not still loading', async () => {
    renderApp(fetchStub(() => jsonResponse(pageOf([]))), '/problems');

    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('No published problems yet.');
    });
  });

  it('takes the page number from the url so a page is linkable', async () => {
    const stub = fetchStub(() => jsonResponse(pageOf([exampleProblem], 3, 20, 50)));
    renderApp(stub, '/problems?page=3');

    await waitFor(() => {
      expect(screen.getByText('Page 3 of 3')).toBeInTheDocument();
    });
    expect(firstRequestUrl(stub)).toContain('page=3');
  });

  it('ignores a page number the url could not have meant', async () => {
    const stub = fetchStub(() => jsonResponse(pageOf([exampleProblem])));
    renderApp(stub, '/problems?page=not-a-number');

    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Order total' })).toBeInTheDocument();
    });
    expect(firstRequestUrl(stub)).not.toContain('page=not-a-number');
  });

  it('renders one problem on /problems/:slug', async () => {
    renderApp(fetchStub(() => jsonResponse(exampleProblemDetail)), '/problems/example-order-total');

    await waitFor(() => {
      expect(screen.getByRole('heading', { level: 1, name: 'Order total' })).toBeInTheDocument();
    });
    expect(screen.getByText(/Improve the calculation/)).toBeInTheDocument();
  });

  it('branches on the error code, not the status, for an unknown slug', async () => {
    renderApp(
      fetchStub(() => problemResponse(404, 'problem_not_found', 'Problem does not exist.')),
      '/problems/nope',
    );

    await waitFor(() => {
      expect(screen.getByRole('heading', { level: 1, name: 'No such problem' })).toBeInTheDocument();
    });
    // A 404 with a different code is a different situation and gets the generic panel instead.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('falls back to the failure panel for a 404 it does not recognise', async () => {
    renderApp(
      fetchStub(() => problemResponse(404, 'something_else', 'Gone.')),
      '/problems/nope',
    );

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Gone.');
    });
  });

  it('renders the not-found page for an unrouted address', () => {
    renderApp(fetchStub(() => jsonResponse([])), '/nowhere');

    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument();
  });
});
