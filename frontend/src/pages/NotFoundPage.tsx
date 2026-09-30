import { Link } from 'react-router';
import { useT } from '../i18n';

/** The client-side 404: a route this application has no page for. */
export function NotFoundPage() {
  const t = useT();

  return (
    <section className="page">
      <h1>{t('notFound.title')}</h1>
      <p>{t('notFound.text')}</p>
      <Link to="/">{t('notFound.home')}</Link>
    </section>
  );
}
