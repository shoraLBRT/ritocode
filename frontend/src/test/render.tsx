import type { ReactNode } from 'react';
import { render } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import type { RouteObject } from 'react-router';
import { ApiClient, ApiClientProvider } from '../api';
import { I18nProvider } from '../i18n';
import { routes } from '../routes';
import { SessionProvider } from '../session';

/**
 * Mounts a route table — the real one by default — on a memory router, inside the providers the
 * application has, so a test navigates it the way a user does — through the layout, not around
 * it — without a browser history.
 */
export function renderApp(fetchStub: typeof globalThis.fetch, initialPath = '/', table: RouteObject[] = routes) {
  const client = new ApiClient({ baseUrl: 'http://api.test/api/v1', fetch: fetchStub });
  const router = createMemoryRouter(table, { initialEntries: [initialPath] });

  return render(
    <I18nProvider>
      <ApiClientProvider client={client}>
        <SessionProvider>
          <RouterProvider router={router} />
        </SessionProvider>
      </ApiClientProvider>
    </I18nProvider>,
  );
}

/** Mounts a single component with a client in context, for the cases that need no routing. */
export function renderWithClient(ui: ReactNode, fetchStub: typeof globalThis.fetch) {
  const client = new ApiClient({ baseUrl: 'http://api.test/api/v1', fetch: fetchStub });
  return render(
    <I18nProvider>
      <ApiClientProvider client={client}>{ui}</ApiClientProvider>
    </I18nProvider>,
  );
}
