// `defineConfig` comes from vitest rather than vite so the `test` block below is typed; it is
// vite's own function with the test options added.
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// The dev server port is not incidental: the API's appsettings.Development.json allows
// http://localhost:5173 as a CORS origin, so changing it here silently breaks every request
// the browser makes to the backend. Change both or neither.
export default defineConfig({
  plugins: [react()],
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
});
