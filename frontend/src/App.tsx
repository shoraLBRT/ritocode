import { RouterProvider, createBrowserRouter } from 'react-router';
import { ApiClientProvider } from './api';
import type { ApiClient } from './api';
import { I18nProvider } from './i18n';
import { routes } from './routes';
import { SessionProvider } from './session';

const router = createBrowserRouter(routes);

/**
 * The application root. It takes its client as a parameter rather than building one, so the
 * composition happens in `main.tsx` — the only file that reads the environment — and a test
 * mounts the same tree against a stub.
 */
export function App({ client }: { client: ApiClient }) {
  return (
    <I18nProvider>
      <ApiClientProvider client={client}>
        <SessionProvider>
          <RouterProvider router={router} />
        </SessionProvider>
      </ApiClientProvider>
    </I18nProvider>
  );
}
