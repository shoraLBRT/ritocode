import { Link } from 'react-router';
import { requestFeedback } from '../feedback';
import { useT } from '../i18n';
import { useSession } from '../session';
import { useSections } from './sections';

/**
 * The footer (docs/DESIGN.md › The arena): the brand with its tagline, the sections, and the
 * service's own pages — privacy and a way into the feedback window. On the page ground, quiet; the
 * columns become one on a phone. The admin area is not listed here.
 */
export function AppFooter() {
  const t = useT();
  const session = useSession();
  const sections = useSections(false);

  return (
    <footer className="app__footer">
      <div className="app__footer-inner">
        <div className="app__footer-brand">
          <Link className="app__brand" to="/">
            <span className="app__mark" aria-hidden="true" />
            {t('app.name')}
          </Link>
          <p>{t('app.tagline')}</p>
        </div>
        <nav className="app__footer-nav" aria-label={t('app.footer.sections')}>
          <p className="app__footer-title">{t('app.footer.sections')}</p>
          {sections.map((section) => (
            <Link key={section.to} to={section.to}>
              {section.label}
            </Link>
          ))}
          {session.status === 'signedIn' && <Link to="/profile">{t('header.toProfile')}</Link>}
        </nav>
        <nav className="app__footer-nav" aria-label={t('app.footer.about')}>
          <p className="app__footer-title">{t('app.footer.about')}</p>
          <Link to="/privacy">{t('app.privacy')}</Link>
          <button type="button" className="button button--small app__footer-feedback" onClick={requestFeedback}>
            {t('app.footer.feedback')}
          </button>
        </nav>
      </div>
    </footer>
  );
}
