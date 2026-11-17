import { useState } from 'react';
import { useT } from '../i18n';
import { applyTheme, currentTheme, saveTheme, themeStorage } from './theme';

/**
 * Switches between the light and the dark theme, and remembers the choice. `labelled` makes it a row
 * with its words (the phone menu); otherwise it is the header's icon button.
 */
export function ThemeToggle({ className, labelled = false }: { className?: string; labelled?: boolean }) {
  const t = useT();
  const [theme, setTheme] = useState(() => (typeof document === 'undefined' ? 'light' : currentTheme()));
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
        setTheme(next);
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
