import { RouterProvider, createBrowserRouter } from 'react-router';
import { ApiClientProvider } from './api';
import type { ApiClient } from './api';
import { routes } from './routes';

const router = createBrowserRouter(routes);

/**
 * The application root. It takes its client as a parameter rather than building one, so the
 * composition happens in `main.tsx` — the only file that reads the environment — and a test
 * mounts the same tree against a stub.
 */
export function App({ client }: { client: ApiClient }) {
  return (
    <ApiClientProvider client={client}>
      <RouterProvider router={router} />
    </ApiClientProvider>
  );
}
