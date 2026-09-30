import { useEffect, useState } from 'react';
import { useLocation } from 'react-router';
import { getProblemCatalogue, useApiClient } from '../api';
import type { CardSections, ProblemCard, ProblemCatalogue } from '../api';
import { EmptyState } from '../components/EmptyState';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { Markdown } from '../components/Markdown';
import { useApiResource } from '../hooks/useApiResource';
import { useT } from '../i18n';

/** The long fields of a card, in the order a card is read. */
const sectionOrder: readonly (keyof CardSections)[] = [
  'signs',
  'whyAiDoesIt',
  'cost',
  'acceptableWhen',
  'detection',
  'treatment',
  'sources',
  'counterArguments',
];

/**
 * `/problems` — the problem catalogue (docs/SPEC.md §4.2): one page, every card in full, grouped by
 * the six classes. A search box filters by name, summary and keywords. Every card is its own anchor,
 * so `/problems#god-class` opens at that card: a card has no page of its own.
 */
export function ProblemsPage() {
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource((signal) => getProblemCatalogue(client, signal), [client]);
  const { hash } = useLocation();

  // The cards arrive after the first render, so the browser's own jump to the anchor finds nothing;
  // once they are on the page, go to the card the address names.
  const loaded = state.status === 'success';
  useEffect(() => {
    if (loaded && hash.length > 1) {
      document.getElementById(decodeURIComponent(hash.slice(1)))?.scrollIntoView();
    }
  }, [loaded, hash]);

  return (
    <section className="page">
      <h1>{t('problems.title')}</h1>
      <p className="page__lead">{t('problems.lead')}</p>

      {state.status === 'loading' && <LoadingState label={t('problems.loading')} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && state.data.cards.length === 0 && <EmptyState>{t('problems.empty')}</EmptyState>}
      {state.status === 'success' && state.data.cards.length > 0 && <Catalogue catalogue={state.data} />}
    </section>
  );
}

function Catalogue({ catalogue }: { catalogue: ProblemCatalogue }) {
  const t = useT();
  const [query, setQuery] = useState('');

  const terms = normalise(query).split(/\s+/).filter((term) => term.length > 0);
  const shown = catalogue.cards.filter((card) => matches(card, terms));

  return (
    <>
      <div className="filters">
        <label className="filters__field filters__field--grow">
          <span>{t('problems.search')}</span>
          <input
            type="search"
            value={query}
            placeholder={t('problems.searchPlaceholder')}
            onChange={(event) => {
              setQuery(event.target.value);
            }}
          />
        </label>
        <span className="filters__count">{t('problems.count', { count: shown.length })}</span>
      </div>

      {shown.length === 0 && <EmptyState>{t('problems.noMatch')}</EmptyState>}

      {catalogue.classes.map((problemClass) => {
        const cards = shown.filter((card) => card.class === problemClass.id);

        return (
          cards.length > 0 && (
            <section key={problemClass.id} className="problem-class" aria-labelledby={`class-${problemClass.id}`}>
              <h2 id={`class-${problemClass.id}`}>{problemClass.name}</h2>
              {problemClass.description !== null && <p className="page__note">{problemClass.description}</p>}
              {cards.map((card) => (
                <Card key={card.slug} card={card} />
              ))}
            </section>
          )
        );
      })}
    </>
  );
}

function Card({ card }: { card: ProblemCard }) {
  const t = useT();

  return (
    <article id={card.slug} className="problem-card">
      <h3 className="problem-card__name">
        {card.name}
        <a className="problem-card__anchor" href={`#${card.slug}`} aria-label={t('problems.anchor', { name: card.name })}>
          {t('problems.anchorMark')}
        </a>
      </h3>
      <p className="problem-card__summary">{card.summary}</p>

      {sectionOrder.map((section) => {
        const text = card.sections[section];

        return (
          text !== null && (
            <div key={section} className="problem-card__section">
              <h4>{t(`problems.sections.${section}`)}</h4>
              <Markdown source={text} />
            </div>
          )
        );
      })}
    </article>
  );
}

/** Every term appears in the name, the summary or a keyword, ignoring case and ё. */
function matches(card: ProblemCard, terms: readonly string[]): boolean {
  const haystack = normalise([card.name, card.summary, ...card.keywords].join('\n'));
  return terms.every((term) => haystack.includes(term));
}

function normalise(text: string): string {
  return text.toLocaleLowerCase('ru').replaceAll('ё', 'е');
}
