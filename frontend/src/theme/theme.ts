/**
 * The colour theme (docs/DESIGN.md). Light is the default for everyone; dark is a choice the
 * learner makes with the toggle in the header, and the browser remembers it.
 *
 * The theme is an attribute on the document's root, which the stylesheet's tokens read. Nothing
 * follows the system's preference on its own: the light theme is the one the design is made for.
 */
export type Theme = 'light' | 'dark';

const KEY = 'ritocode:theme';

/** The browser's local storage, or null where it will not give it — blocked storage, no window. */
export function themeStorage(): Storage | null {
  try {
    return typeof window === 'undefined' ? null : window.localStorage;
  } catch {
    return null;
  }
}

/** The theme kept in the browser; light when nothing, or nothing readable, is kept. */
export function readTheme(storage: Storage | null): Theme {
  try {
    return storage?.getItem(KEY) === 'dark' ? 'dark' : 'light';
  } catch {
    return 'light';
  }
}

/** Keeps the choice; a browser that refuses simply forgets it. */
export function saveTheme(storage: Storage | null, theme: Theme): void {
  try {
    storage?.setItem(KEY, theme);
  } catch {
    // Not kept — the page still switches.
  }
}

export function applyTheme(theme: Theme, root: HTMLElement = document.documentElement): void {
  root.dataset.theme = theme;
}

export function currentTheme(root: HTMLElement = document.documentElement): Theme {
  return root.dataset.theme === 'dark' ? 'dark' : 'light';
}
