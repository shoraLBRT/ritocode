// `defineConfig` comes from vitest rather than vite so the `test` block below is typed; it is
// vite's own function with the test options added.
import { defineConfig } from 'vitest/config';
import { loadEnv } from 'vite';
import type { Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import { verificationTags } from './src/prerender/verification.ts';

/**
 * The search consoles' meta tags (#303) in the shell's head, from VITE_YANDEX_VERIFICATION and
 * VITE_GOOGLE_SITE_VERIFICATION (build-time, as Umami's are); every prerendered page is made from
 * the shell, so each carries them. No tag for a value that is unset.
 */
function searchConsoles(env: Record<string, string>): Plugin {
  return {
    name: 'ritocode-search-consoles',
    transformIndexHtml: () =>
      verificationTags(env).map(({ name, content }) => ({ tag: 'meta', attrs: { name, content }, injectTo: 'head' as const })),
  };
}

// The dev server port is not incidental: the API's appsettings.Development.json allows
// http://localhost:5173 as a CORS origin, so changing it here silently breaks every request
// the browser makes to the backend. Change both or neither.
export default defineConfig(({ mode }) => ({
  plugins: [react(), searchConsoles(loadEnv(mode, process.cwd(), 'VITE_'))],
  server: {
    port: 5173,
    strictPort: true,
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    restoreMocks: true,
  },
}));
