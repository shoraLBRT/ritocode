import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { loadAnalytics, resolveAnalyticsConfig } from './analytics';
import { ApiClient, resolveApiBaseUrl } from './api';
import { App } from './App';
import { resolveSiteConfig } from './site';
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

// Umami, only when the build names it (docs/SPEC.md §8).
const analytics = resolveAnalyticsConfig(import.meta.env);
if (analytics !== null) {
  loadAnalytics(analytics);
}

createRoot(root).render(
  <StrictMode>
    <App client={client} config={config} />
  </StrictMode>,
);
