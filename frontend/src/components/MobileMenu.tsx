import { useEffect, useRef } from 'react';
import { Link, NavLink } from 'react-router';
import { useT } from '../i18n';
import { useSession } from '../session';
import { useSiteConfig } from '../site';
import { ThemeToggle } from '../theme';
import { SignOutButton } from './SignOutButton';
import { useSections } from './sections';

const FOCUSABLE = 'a[href], button:not([disabled]), input, select, textarea, [tabindex]:not([tabindex="-1"])';

/**
 * The phone header's menu (docs/DESIGN.md › The arena): a sheet under the header with the sections
 * as large targets, the theme and the session's actions. Only rendered while open. Focus goes to the
 * first item; Tab stays inside; Esc, a tap outside or following a link closes it, and `onClose`
 * gives the focus back to the button. `top` is where the header ends.
 */
export function MobileMenu({ id, top, admin, onClose }: { id: string; top: number; admin: boolean; onClose: () => void }) {
  const t = useT();
  const session = useSession();
  const { demoTask } = useSiteConfig();
  const sections = useSections(admin);
  const sheet = useRef<HTMLDivElement>(null);

  useEffect(() => {
    sheet.current?.querySelector<HTMLElement>(FOCUSABLE)?.focus();

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        onClose();
        return;
      }
      if (event.key !== 'Tab' || sheet.current === null) {
        return;
      }
      const items = Array.from(sheet.current.querySelectorAll<HTMLElement>(FOCUSABLE));
      const first = items[0];
      const last = items[items.length - 1];
      if (first === undefined || last === undefined) {
        return;
      }
      const inside = sheet.current.contains(document.activeElement);
      if (!inside || (event.shiftKey && document.activeElement === first)) {
        event.preventDefault();
        (event.shiftKey ? last : first).focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('keydown', onKey);
    };
  }, [onClose]);

  return (
    <>
      <div className="app__sheet-backdrop" style={{ top }} onClick={onClose} aria-hidden="true" />
      <div id={id} className="app__sheet" style={{ top }} ref={sheet} role="dialog" aria-modal="true" aria-label={t('header.menu')}>
        <nav className="app__sheet-nav" aria-label={t('nav.label')}>
          {sections.map((section) => (
            <NavLink
              key={section.to}
              to={section.to}
              className={({ isActive }) => (isActive ? 'app__sheet-item app__sheet-item--active' : 'app__sheet-item')}
              onClick={onClose}
            >
              {section.label}
            </NavLink>
          ))}
        </nav>
        <div className="app__sheet-group">
          <ThemeToggle labelled />
          {session.status === 'signedIn' && (
            <>
              <Link className="app__sheet-item" to="/profile" onClick={onClose}>
                {t('header.toProfile')}
              </Link>
              <SignOutButton className="app__sheet-item" signOut={session.signOut} />
            </>
          )}
          {session.status === 'signedOut' && (
            <Link className="button button--primary app__sheet-start" to={`/tasks/${demoTask}`} onClick={onClose}>
              {t('header.start')}
            </Link>
          )}
        </div>
      </div>
    </>
  );
}
