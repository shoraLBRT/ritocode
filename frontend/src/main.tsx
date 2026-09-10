import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ApiClient, resolveApiBaseUrl } from './api';
import { App } from './App';
import './styles.css';

const root = document.getElementById('root');

if (root === null) {
  throw new Error('index.html is missing the #root element.');
}

// The one place the environment is read. Everything below takes the client it is given.
const client = new ApiClient({ baseUrl: resolveApiBaseUrl(import.meta.env) });

createRoot(root).render(
  <StrictMode>
    <App client={client} />
  </StrictMode>,
);
