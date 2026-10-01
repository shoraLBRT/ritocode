// Where the pieces of the end-to-end run live (#39). The ports are the development ones: the API's
// appsettings.Development.json allows http://localhost:5173 as an origin and sends a finished sign-in
// back there, and the pages' default API address is http://localhost:5199/api/v1. A server already on
// one of them stops the run rather than being reused.
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

export const root = resolve(import.meta.dirname, '..');

export const pagesUrl = 'http://localhost:5173';
export const apiUrl = 'http://localhost:5199';
export const providerUrl = 'http://localhost:5299';

/** The compose PostgreSQL's settings, from the repository's .env (as scripts/dev-up.sh reads them). */
/** @returns {Record<string, string>} */
function composeSettings() {
  const settings = {
    POSTGRES_USER: 'ritocode',
    POSTGRES_PASSWORD: 'ritocode',
    POSTGRES_PORT: '55432',
  };

  for (const file of ['.env.example', '.env']) {
    try {
      for (const line of readFileSync(resolve(root, file), 'utf8').split(/\r?\n/)) {
        const match = /^([A-Z_]+)=(.*)$/.exec(line.trim());
        if (match) settings[match[1]] = match[2];
      }
    } catch {
      // No such file: the defaults, or what the other file said, stand.
    }
  }

  return settings;
}

const compose = composeSettings();

/** A database of its own beside the development one, so a run never writes into it. */
export const database = 'ritocode_e2e';
export const databaseUser = compose.POSTGRES_USER;

export const connectionString =
  process.env.E2E_DATABASE_CONNECTION_STRING
  ?? `Host=localhost;Port=${compose.POSTGRES_PORT};Database=${database};Username=${compose.POSTGRES_USER};Password=${compose.POSTGRES_PASSWORD}`;
