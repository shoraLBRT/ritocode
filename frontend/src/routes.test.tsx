import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { renderApp } from './test/render';
import { jsonResponse } from './test/responses';

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

  it('renders the not-found page for an unrouted address', () => {
    renderApp(fetchStub(() => jsonResponse([])), '/nowhere');

    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument();
  });
});
