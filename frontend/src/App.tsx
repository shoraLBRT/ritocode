import { useEffect } from 'react';
import { RouterProvider } from 'react-router';
import type { DataRouter } from 'react-router';
import { ApiClientProvider } from './api';
import type { ApiClient } from './api';
import { markApplicationMounted } from './hooks/useCountUp';
import { I18nProvider } from './i18n';
import { SessionProvider } from './session';
import { PrerenderedContext, SiteConfigContext } from './site';
import type { PrerenderedData, SiteConfig } from './site';

/**
 * The application root. It takes its client and its configuration as parameters rather than
 * building them, so the composition happens in `main.tsx` — the only file that reads the
 * environment — and a test mounts the same tree against a stub. `prerendered` is the data the page
 * was prerendered from, when the application hydrates it (#310).
 */
export function App({
  client,
  config,
  router,
  prerendered = null,
}: {
  client: ApiClient;
  config: SiteConfig;
  router: DataRouter;
  prerendered?: PrerenderedData | null;
}) {
  // Components mounted after this point may count their numbers up; prerendered ones may not.
  useEffect(() => {
    markApplicationMounted();
  }, []);

  return (
    <I18nProvider>
      <SiteConfigContext value={config}>
        <ApiClientProvider client={client}>
          <SessionProvider>
            <PrerenderedContext value={prerendered}>
              <RouterProvider router={router} />
            </PrerenderedContext>
          </SessionProvider>
        </ApiClientProvider>
      </SiteConfigContext>
    </I18nProvider>
  );
}
