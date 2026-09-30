import { Link } from 'react-router';
import { useT } from '../i18n';
import { useSiteConfig } from '../site';

/**
 * `/` — the landing page (docs/SPEC.md §4.1, docs/CONCEPT.md): what Ritocode is and who it is for,
 * a demo task, and the way to the tasks and the problem catalogue.
 *
 * It asks the API for nothing, so what it shows does not depend on a request — the prerender of
 * [#132](https://github.com/shoraLBRT/ritocode/issues/132) can take it as it is. The demo task is
 * an easy task named in configuration (`VITE_DEMO_TASK`), and opens signed out.
 */
export function HomePage() {
  const t = useT();
  const { demoTask } = useSiteConfig();

  return (
    <section className="page landing">
      <header className="landing__hero">
        <h1>{t('app.name')}</h1>
        <p className="landing__lead">{t('home.lead')}</p>
        <p className="page__lead">{t('home.audience')}</p>

        <nav className="landing__actions" aria-label={t('home.actions')}>
          <Link className="button button--primary" to={`/tasks/${demoTask}`}>
            {t('home.demo')}
          </Link>
          <Link className="button" to="/tasks">
            {t('home.tasks')}
          </Link>
          <Link className="button" to="/problems">
            {t('home.problems')}
          </Link>
        </nav>
        <p className="page__note">{t('home.demoNote')}</p>
      </header>

      <h2>{t('home.taskTitle')}</h2>
      <p>{t('home.taskLead')}</p>
      <ol className="landing__steps">
        <li>{t('home.step1')}</li>
        <li>{t('home.step2')}</li>
        <li>{t('home.step3')}</li>
      </ol>

      <h2>{t('home.proportionTitle')}</h2>
      <p>{t('home.proportionText')}</p>

      <h2>{t('home.briefTitle')}</h2>
      <p>{t('home.briefText')}</p>

      <h2>{t('home.catalogueTitle')}</h2>
      <p>{t('home.catalogueText')}</p>
      <p>
        <Link to="/problems">{t('home.catalogueLink')}</Link>
      </p>
    </section>
  );
}
