import { Link, useParams } from 'react-router';
import { getProblemCatalogue, useApiClient } from '../api';
import type { ProblemCard, ProblemCatalogue } from '../api';
import { BackToTop } from '../components/BackToTop';
import { CardSectionsView } from '../components/CardSectionsView';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { Markdown } from '../components/Markdown';
import { useApiResource } from '../hooks/useApiResource';
import { useStudyMarks } from '../hooks/useStudyMarks';
import { useT } from '../i18n';
import { plainText, problemMeta, usePageMeta, usePrerendered } from '../site';
import { NotFoundPage } from './NotFoundPage';
import { StudyToggle } from './ProblemsPage';

/**
 * `/problems/{slug}` — one problem card on a page of its own (docs/SPEC.md §4.2, #301): the card in
 * full in its class's colours, the toggle «Изучено», and the other cards of its class. A query about
 * one problem lands here rather than on the long catalogue; `/problems#slug` still opens the card
 * in the catalogue. A slug that names no card is the page of an unknown address.
 */
export function ProblemPage() {
  const client = useApiClient();
  const t = useT();
  const { slug = '' } = useParams();
  // Prerendered, the page hydrates with the card and its class it shows, and loads nothing (#310).
  const prerendered = usePrerendered(`/problems/${slug}`)?.catalogue;
  const { state, reload } = useApiResource((signal) => getProblemCatalogue(client, signal), [client], prerendered && { data: prerendered });

  if (state.status === 'loading') {
    return (
      <section className="page rc-problem">
        <LoadingState label={t('problems.loading')} />
      </section>
    );
  }

  if (state.status === 'error') {
    return (
      <section className="page rc-problem">
        <ErrorState error={state.error} onRetry={reload} />
      </section>
    );
  }

  const card = state.data.cards.find((each) => each.slug === slug);

  return card === undefined ? <NotFoundPage /> : <CardPage catalogue={state.data} card={card} />;
}

function CardPage({ catalogue, card }: { catalogue: ProblemCatalogue; card: ProblemCard }) {
  const t = useT();
  const marks = useStudyMarks();
  const className = catalogue.classes.find((each) => each.id === card.class)?.name ?? card.class;
  const related = catalogue.cards.filter((each) => each.class === card.class && each.slug !== card.slug);

  usePageMeta(problemMeta(card, t));

  return (
    <section className="page rc-problem">
      <p className="rc-problem__crumbs">
        <Link to="/problems">{t('problems.catalogue')}</Link>
        <span aria-hidden="true">{'/'}</span>
        <span>{className}</span>
      </p>

      <article className={`rc-pcard rc-pcard--open rc-problem__card k-${card.class}`} aria-labelledby="problem-name">
        <div className="rc-pcard__band">
          <span className="rc-pcard__band-label">
            <span>{className}</span>
            <span>{t('problems.slug', { slug: card.slug })}</span>
          </span>
          <span className="rc-pcard__seal" aria-hidden="true" />
        </div>
        <div className="rc-pcard__face">
          <h1 id="problem-name" className="rc-pcard__name">
            {card.name}
          </h1>
          <span className="rc-pcard__rule" aria-hidden="true" />
          <div className="rc-pcard__summary">
            <Markdown source={card.summary} />
          </div>
          <StudyToggle slug={card.slug} marks={marks} />
          <div className="rc-pcard__sections">
            <CardSectionsView sections={card.sections} />
          </div>
        </div>
      </article>

      {related.length > 0 && (
        <section className={`rc-problem__related k-${card.class}`} aria-labelledby="problem-related">
          <h2 id="problem-related">{t('problems.related', { name: className })}</h2>
          <ul>
            {related.map((each) => (
              <li key={each.slug}>
                <Link to={`/problems/${each.slug}`}>{each.name}</Link>
                <span>{plainText(each.summary, 120)}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
      <BackToTop />
    </section>
  );
}
