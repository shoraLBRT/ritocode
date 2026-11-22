import { StrictMode } from 'react';
import { createRoot, hydrateRoot } from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import type { DataRouter } from 'react-router';
import { loadAnalytics, resolveAnalyticsConfig } from './analytics';
import { ApiClient, resolveApiBaseUrl } from './api';
import { App } from './App';
import { installErrorReporter } from './errors/reporter';
import { routes } from './routes';
import { readPrerendered, resolveSiteConfig } from './site';
import { applyTheme, readTheme, themeStorage } from './theme';
// IBM Plex, served with the site rather than from a font CDN (docs/DESIGN.md).
import '@fontsource/ibm-plex-sans/400.css';
import '@fontsource/ibm-plex-sans/500.css';
import '@fontsource/ibm-plex-sans/600.css';
import '@fontsource/ibm-plex-sans/700.css';
import '@fontsource/ibm-plex-mono/400.css';
import '@fontsource/ibm-plex-mono/500.css';
import '@fontsource/ibm-plex-mono/600.css';
import './styles.css';
import './pages/task/workspace.css';

// Before the first render, so a learner who chose the dark theme never sees a light page first.
applyTheme(readTheme(themeStorage()));

const root = document.getElementById('root');

if (root === null) {
  throw new Error('index.html is missing the #root element.');
}

// The one place the environment is read. Everything below takes the client and configuration it is given.
const client = new ApiClient({ baseUrl: resolveApiBaseUrl(import.meta.env) });
const config = resolveSiteConfig(import.meta.env);

// Uncaught errors reach the maintainer (#294); the route error boundary reports through it too.
installErrorReporter(client.baseUrl);

// Umami, only when the build names it (docs/SPEC.md §8).
const analytics = resolveAnalyticsConfig(import.meta.env);
if (analytics !== null) {
  loadAnalytics(analytics);
}

// A prerendered page is hydrated with the data it was rendered from, so its markup stays as it is
// and nothing it shows is fetched again (#310). Under another address than its own — the task page
// under an attempt's, 404.html under any — or with a query, which a page may read but the prerender
// never had, the application renders afresh over it instead.
const prerendered = window.location.search === '' ? readPrerendered(document, window.location.pathname) : null;
const router = createBrowserRouter(routes);

// A route of its own chunk (routes.tsx) is loaded before the first render, so that render is the page.
void routerReady(router).then(() => {
  const app = (
    <StrictMode>
      <App client={client} config={config} router={router} prerendered={prerendered} />
    </StrictMode>
  );

  if (prerendered !== null) {
    hydrateRoot(root, app);
  } else {
    createRoot(root).render(app);
  }
});

function routerReady(router: DataRouter): Promise<void> {
  return new Promise((resolve) => {
    if (router.state.initialized) {
      resolve();
      return;
    }

    const stop = router.subscribe((state) => {
      if (state.initialized) {
        stop();
        resolve();
      }
    });
  });
}
