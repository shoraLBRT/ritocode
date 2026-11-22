import { useSyncExternalStore } from 'react';
import { useT } from '../i18n';
import { applyTheme, currentTheme, saveTheme, themeStorage } from './theme';
import type { Theme } from './theme';

/** The theme the root carries, followed as the toggle changes it. */
function subscribe(onChange: () => void): () => void {
  const observer = new MutationObserver(onChange);
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  return () => {
    observer.disconnect();
  };
}

const snapshot = (): Theme => currentTheme();
// The prerendered page is light; a hydrated toggle starts from it and then reads the theme chosen (#310).
const serverSnapshot = (): Theme => 'light';

/**
 * Switches between the light and the dark theme, and remembers the choice. `labelled` makes it a row
 * with its words (the phone menu); otherwise it is the header's icon button.
 */
export function ThemeToggle({ className, labelled = false }: { className?: string; labelled?: boolean }) {
  const t = useT();
  const theme = useSyncExternalStore(subscribe, snapshot, serverSnapshot);
  const next = theme === 'dark' ? 'light' : 'dark';
  const label = t(next === 'dark' ? 'app.themeDark' : 'app.themeLight');

  return (
    <button
      type="button"
      className={labelled ? 'app__sheet-item app__sheet-theme' : className === undefined ? 'icon-button' : `icon-button ${className}`}
      {...(labelled ? {} : { 'aria-label': label, title: label })}
      onClick={() => {
        applyTheme(next);
        saveTheme(themeStorage(), next);
      }}
    >
      <svg viewBox="0 0 24 24" aria-hidden="true" className="icon">
        <circle cx="12" cy="12" r="8" />
        <path d="M12 4a8 8 0 0 1 0 16z" fill="currentColor" />
      </svg>
      {labelled && <span>{label}</span>}
    </button>
  );
}
