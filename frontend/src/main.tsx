import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { loadAnalytics, resolveAnalyticsConfig } from './analytics';
import { ApiClient, resolveApiBaseUrl } from './api';
import { App } from './App';
import { resolveSiteConfig } from './site';
import './styles.css';

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
