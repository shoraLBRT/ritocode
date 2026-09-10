import type { ReactNode } from 'react';
import { render } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { ApiClient, ApiClientProvider } from '../api';
import { routes } from '../routes';

/**
 * Mounts the real route table on a memory router, so a test navigates the application the way a
 * user does — through the layout, not around it — without a browser history.
 */
export function renderApp(fetchStub: typeof globalThis.fetch, initialPath = '/') {
  const client = new ApiClient({ baseUrl: 'http://api.test/api/v1', fetch: fetchStub });
  const router = createMemoryRouter(routes, { initialEntries: [initialPath] });

  return render(
    <ApiClientProvider client={client}>
      <RouterProvider router={router} />
    </ApiClientProvider>,
  );
}

/** Mounts a single component with a client in context, for the cases that need no routing. */
export function renderWithClient(ui: ReactNode, fetchStub: typeof globalThis.fetch) {
  const client = new ApiClient({ baseUrl: 'http://api.test/api/v1', fetch: fetchStub });
  return render(<ApiClientProvider client={client}>{ui}</ApiClientProvider>);
}
