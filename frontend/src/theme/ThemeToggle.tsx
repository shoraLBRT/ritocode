import { useState } from 'react';
import { useT } from '../i18n';
import { applyTheme, currentTheme, saveTheme, themeStorage } from './theme';

/** Switches between the light and the dark theme, and remembers the choice. */
export function ThemeToggle() {
  const t = useT();
  const [theme, setTheme] = useState(() => (typeof document === 'undefined' ? 'light' : currentTheme()));
  const next = theme === 'dark' ? 'light' : 'dark';

  return (
    <button
      type="button"
      className="icon-button"
      aria-label={t(next === 'dark' ? 'app.themeDark' : 'app.themeLight')}
      title={t(next === 'dark' ? 'app.themeDark' : 'app.themeLight')}
      onClick={() => {
        applyTheme(next);
        saveTheme(themeStorage(), next);
        setTheme(next);
      }}
    >
      <svg viewBox="0 0 24 24" aria-hidden="true" className="icon">
        <circle cx="12" cy="12" r="8" />
        <path d="M12 4a8 8 0 0 1 0 16z" fill="currentColor" />
      </svg>
    </button>
  );
}
