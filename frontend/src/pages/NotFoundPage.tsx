import { Link } from 'react-router';
import { useT } from '../i18n';

/**
 * The client-side 404: a route this application has no page for. A large mark, what happened and
 * three ways out. `title` and `text` let another page say its own words in the same frame (an
 * unknown task, say).
 */
export function NotFoundPage({ title, text }: { title?: string; text?: string }) {
  const t = useT();

  const ways = [
    { to: '/', label: t('notFound.home'), hint: t('notFound.homeHint'), primary: true },
    { to: '/tasks', label: t('notFound.tasks'), hint: t('notFound.tasksHint'), primary: false },
    { to: '/problems', label: t('notFound.problems'), hint: t('notFound.problemsHint'), primary: false },
  ];

  return (
    <section className="page rc-notfound">
      <p className="rc-notfound__code" aria-hidden="true">
        <span>{t('notFound.digit')}</span>
        <svg className="rc-notfound__mark" viewBox="0 0 26 26" focusable="false">
          <rect width="26" height="26" rx="8" />
          <path d="M6.5 13h13M13 6.5v13" />
        </svg>
        <span>{t('notFound.digit')}</span>
      </p>
      <h1>{title ?? t('notFound.title')}</h1>
      <p className="rc-notfound__text">{text ?? t('notFound.text')}</p>

      <nav className="rc-notfound__ways" aria-label={t('notFound.ways')}>
        {ways.map((way) => (
          <Link key={way.to} to={way.to} className={way.primary ? 'rc-notfound__way rc-notfound__way--primary' : 'rc-notfound__way'}>
            <span className="rc-notfound__way-label">{way.label}</span>
            <span className="rc-notfound__way-hint">{way.hint}</span>
            <span className="rc-notfound__arrow" aria-hidden="true" />
          </Link>
        ))}
      </nav>
    </section>
  );
}
