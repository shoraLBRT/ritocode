import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useLocation } from 'react-router';
import { track } from '../analytics';
import { getProblemCatalogue, useApiClient } from '../api';
import type { ProblemCard, ProblemCatalogue } from '../api';
import { EmptyState } from '../components/EmptyState';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { CardSectionsView } from '../components/CardSectionsView';
import { useApiResource } from '../hooks/useApiResource';
import { useT } from '../i18n';

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
    <ProblemsFrame>
      {state.status === 'loading' && <LoadingState label={t('problems.loading')} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && <CatalogueOrEmpty catalogue={state.data} />}
    </ProblemsFrame>
  );
}

/**
 * The same page for a catalogue already in hand: what the build prerenders from the content export
 * (docs/SPEC.md §4.1), so a search engine reads every card without running JavaScript.
 */
export function StaticProblemsPage({ catalogue }: { catalogue: ProblemCatalogue }) {
  return (
    <ProblemsFrame>
      <CatalogueOrEmpty catalogue={catalogue} />
    </ProblemsFrame>
  );
}

function ProblemsFrame({ children }: { children: ReactNode }) {
  const t = useT();

  return (
    <section className="page">
      <h1>{t('problems.title')}</h1>
      <p className="page__lead">{t('problems.lead')}</p>
      {children}
    </section>
  );
}

function CatalogueOrEmpty({ catalogue }: { catalogue: ProblemCatalogue }) {
  const t = useT();

  return catalogue.cards.length === 0 ? <EmptyState>{t('problems.empty')}</EmptyState> : <Catalogue catalogue={catalogue} />;
}

function Catalogue({ catalogue }: { catalogue: ProblemCatalogue }) {
  const t = useT();
  const [query, setQuery] = useState('');
  const searched = useRef(false);

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
              // Counted once a visit, on the first thing typed — never what was typed.
              if (!searched.current && event.target.value.trim() !== '') {
                searched.current = true;
                track('catalogue-search');
              }
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

      <CardSectionsView sections={card.sections} />
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
