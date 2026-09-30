import { RouterProvider, createBrowserRouter } from 'react-router';
import { ApiClientProvider } from './api';
import type { ApiClient } from './api';
import { I18nProvider } from './i18n';
import { routes } from './routes';
import { SessionProvider } from './session';
import { SiteConfigContext } from './site';
import type { SiteConfig } from './site';

const router = createBrowserRouter(routes);

/**
 * The application root. It takes its client and its configuration as parameters rather than
 * building them, so the composition happens in `main.tsx` — the only file that reads the
 * environment — and a test mounts the same tree against a stub.
 */
export function App({ client, config }: { client: ApiClient; config: SiteConfig }) {
  return (
    <I18nProvider>
      <SiteConfigContext value={config}>
        <ApiClientProvider client={client}>
          <SessionProvider>
            <RouterProvider router={router} />
          </SessionProvider>
        </ApiClientProvider>
      </SiteConfigContext>
    </I18nProvider>
  );
}
