import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist', 'dist-ssr', 'coverage', 'node_modules'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      ...tseslint.configs.strictTypeChecked,
      ...tseslint.configs.stylisticTypeChecked,
      // The `flat` namespace is the flat-config shape; the top-level one is still eslintrc's.
      reactHooks.configs.flat['recommended-latest'],
    ],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: {
      'react-refresh': reactRefresh,
    },
    rules: {
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      // The API client narrows `unknown` by hand rather than trusting a cast, so the
      // guard functions that do it are the one place `any`-adjacent checks are honest.
      '@typescript-eslint/no-unnecessary-condition': 'off',
    },
  },
  {
    // No user-visible string is written in a component (docs/SPEC.md §3.6): text in JSX, and the
    // attributes a person reads or hears, come from the translation catalogue in src/i18n.
    files: ['src/**/*.tsx'],
    ignores: ['**/*.test.tsx', 'src/test/**'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          // Any text that is not only whitespace. Doubled, so the string holds `\S` and not `S`.
          selector: 'JSXText[value=/\\S/]',
          message: 'User-visible text goes in the translation catalogue (src/i18n/ru.ts) and is read with useT().',
        },
        {
          selector: 'JSXAttribute[name.name=/^(aria-label|title|alt|placeholder|label)$/] > Literal',
          message: 'User-visible text goes in the translation catalogue (src/i18n/ru.ts) and is read with useT().',
        },
      ],
    },
  },
  {
    // Rendered once at build time, never hot-reloaded: a page component beside the function is fine.
    files: ['src/prerender/**/*.tsx'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },
  {
    files: ['**/*.test.{ts,tsx}', 'src/test/**/*.ts'],
    languageOptions: { globals: { ...globals.browser, ...globals.node } },
  },
  {
    files: ['vite.config.ts', 'eslint.config.js', 'scripts/**/*.mjs'],
    extends: [tseslint.configs.disableTypeChecked],
    languageOptions: { globals: globals.node },
  },
);
