import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent, MouseEvent as ReactMouseEvent, ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useLocation } from 'react-router';
import { track } from '../analytics';
import { getProblemCatalogue, useApiClient } from '../api';
import type { ProblemCard, ProblemCatalogue, ProblemClass } from '../api';
import { CardSectionsView } from '../components/CardSectionsView';
import { EmptyState } from '../components/EmptyState';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { Markdown } from '../components/Markdown';
import { useApiResource } from '../hooks/useApiResource';
import { useT } from '../i18n';
import { Icon } from './task/parts';

/**
 * `/problems` — the problem catalogue (docs/SPEC.md §4.2, DESIGN.md › The problem catalogue): every
 * card as a card in its class's colours, grouped by the six classes, with a search and a class
 * filter. A folded card shows its name and summary; opening it grows it to the centre of the screen
 * with all its sections. `/problems#god-class` opens at that card, already open.
 *
 * Every card's sections are in the page, inside a native `<details>`: without JavaScript — the
 * prerendered page — a card unfolds in place, so the catalogue is readable and indexable as it is.
 */
export function ProblemsPage() {
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource((signal) => getProblemCatalogue(client, signal), [client]);

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
    <section className="page rc-catalogue">
      <header className="rc-page-head">
        <h1>{t('problems.title')}</h1>
        <p>{t('problems.lead')}</p>
      </header>
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
  const location = useLocation();
  const [query, setQuery] = useState('');
  const [classFilter, setClassFilter] = useState<string | null>(null);
  // The card the learner opened with a click, and the navigation whose card they closed: a card the
  // address names is open until it is closed, and opens again on the next link to it.
  const [clicked, setClicked] = useState<string | null>(null);
  const [closedAt, setClosedAt] = useState<string | null>(null);
  const searched = useRef(false);
  const returnFocus = useRef<string | null>(null);

  const terms = normalise(query).split(/\s+/).filter((term) => term.length > 0);
  const found = catalogue.cards.filter((card) => matches(card, terms));
  const shown = found.filter((card) => classFilter === null || card.class === classFilter);
  const named = decodeURIComponent(location.hash.slice(1));
  const fromAddress = closedAt === location.key ? null : named;
  const openCard = catalogue.cards.find((card) => card.slug === (clicked ?? fromAddress));

  // The address names a card: bring it into view — on arrival, and on every link to a card followed
  // later (the same `#slug` twice is two navigations, so this follows the location).
  useEffect(() => {
    const card = named === '' ? null : document.getElementById(named);
    if (card?.classList.contains('rc-pcard') === true) {
      card.scrollIntoView({ block: 'center' });
    }
  }, [location, named]);

  const openAt = useCallback((card: ProblemCard) => {
    setClicked(card.slug);
    replaceHash(card.slug);
  }, []);

  const close = useCallback(() => {
    returnFocus.current = openCard?.slug ?? null;
    setClicked(null);
    setClosedAt(location.key);
    replaceHash(null);
  }, [openCard, location.key]);

  // Focus goes back to the card the open one came from, once that card is shown again.
  useEffect(() => {
    if (openCard === undefined && returnFocus.current !== null) {
      document.getElementById(returnFocus.current)?.querySelector<HTMLElement>('summary')?.focus();
      returnFocus.current = null;
    }
  }, [openCard]);

  return (
    <>
      <div className="rc-panel rc-catalogue__bar" inert={openCard !== undefined}>
        <div className="rc-catalogue__search-row">
          <label className="rc-catalogue__search">
            <span className="visually-hidden">{t('problems.search')}</span>
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
          <span className="rc-catalogue__count">{t('problems.count', { count: shown.length })}</span>
        </div>
        <ClassFilter classes={catalogue.classes} cards={found} value={classFilter} onChange={setClassFilter} />
      </div>

      {shown.length === 0 && <EmptyState>{t('problems.noMatch')}</EmptyState>}

      <div className="rc-catalogue__classes" inert={openCard !== undefined}>
        {catalogue.classes.map((problemClass) => {
          const cards = shown.filter((card) => card.class === problemClass.id);

          return (
            cards.length > 0 && (
              <section key={problemClass.id} className={`rc-catalogue__class k-${problemClass.id}`} aria-labelledby={`class-${problemClass.id}`}>
                <div className="rc-catalogue__class-head">
                  <h2 id={`class-${problemClass.id}`}>{problemClass.name}</h2>
                  {problemClass.description !== null && <p>{problemClass.description}</p>}
                </div>
                <div className="rc-catalogue__grid">
                  {cards.map((card) => (
                    <Card key={card.slug} card={card} className={problemClass.name} lifted={openCard?.slug === card.slug} onOpen={openAt} />
                  ))}
                </div>
              </section>
            )
          );
        })}
      </div>

      {openCard !== undefined && (
        <OpenCard
          card={openCard}
          className={catalogue.classes.find((each) => each.id === openCard.class)?.name ?? openCard.class}
          onClose={close}
        />
      )}
    </>
  );
}

/** Any number of classes is one: a click on a class shows only it, and «Все» shows them all again. */
function ClassFilter({
  classes,
  cards,
  value,
  onChange,
}: {
  classes: readonly ProblemClass[];
  cards: readonly ProblemCard[];
  value: string | null;
  onChange: (value: string | null) => void;
}) {
  const t = useT();
  const count = (id: string | null) => cards.filter((card) => id === null || card.class === id).length;

  return (
    <div className="rc-catalogue__filter" role="group" aria-label={t('problems.classFilter')}>
      <button
        type="button"
        className="rc-catalogue__chip"
        aria-pressed={value === null}
        onClick={() => {
          onChange(null);
        }}
      >
        {t('problems.allClasses')} <span className="rc-catalogue__chip-count">{count(null)}</span>
      </button>
      {classes.map((problemClass) => (
        <button
          key={problemClass.id}
          type="button"
          className={`rc-catalogue__chip k-${problemClass.id}`}
          aria-pressed={value === problemClass.id}
          onClick={() => {
            onChange(value === problemClass.id ? null : problemClass.id);
          }}
        >
          <span className="rc-catalogue__dot" aria-hidden="true" />
          {problemClass.name} <span className="rc-catalogue__chip-count">{count(problemClass.id)}</span>
        </button>
      ))}
    </div>
  );
}

/**
 * A folded card: the class on its frame, the name and the summary on its face, and its seal a link
 * to it (`#slug`). The whole card opens it — the `<summary>` of the `<details>` that holds its sections stretches over the card. With
 * JavaScript the click opens the card at the centre instead of unfolding it in place.
 */
function Card({
  card,
  className,
  lifted,
  onOpen,
}: {
  card: ProblemCard;
  className: string;
  lifted: boolean;
  onOpen: (card: ProblemCard) => void;
}) {
  const t = useT();

  const open = (event: ReactMouseEvent<HTMLElement>) => {
    event.preventDefault();
    onOpen(card);
  };

  return (
    <article id={card.slug} className={`rc-pcard k-${card.class}${lifted ? ' rc-pcard--lifted' : ''}`} aria-labelledby={`${card.slug}-name`}>
      <div className="rc-pcard__band">
        <span>{className}</span>
        {/* The seal is the card's anchor: a link to it that works without JavaScript too. */}
        <a className="rc-pcard__seal" href={`#${card.slug}`} aria-label={t('problems.anchor', { name: card.name })} />
      </div>
      <div className="rc-pcard__face">
        <h3 id={`${card.slug}-name`} className="rc-pcard__name">
          {card.name}
        </h3>
        <span className="rc-pcard__rule" aria-hidden="true" />
        <div className="rc-pcard__summary">
          <Markdown source={card.summary} />
        </div>
        <details className="rc-pcard__more">
          <summary aria-label={t('problems.openCard', { name: card.name })} onClick={open}>
            <span>{t('problems.open')}</span>
          </summary>
          <div className="rc-pcard__sections">
            <CardSectionsView sections={card.sections} />
          </div>
        </details>
      </div>
    </article>
  );
}

/**
 * The open card: a modal dialog over the dimmed page, in the same frame as the folded card, with
 * every section. It grows out of the folded card's place unless motion is reduced; ×, Esc and a
 * click outside close it, and Tab stays inside it while it is open.
 */
function OpenCard({ card, className, onClose }: { card: ProblemCard; className: string; onClose: () => void }) {
  const t = useT();
  const dialog = useRef<HTMLDivElement>(null);
  const closeButton = useRef<HTMLButtonElement>(null);

  useLayoutEffect(() => {
    const element = dialog.current;
    const from = document.getElementById(card.slug)?.getBoundingClientRect();
    closeButton.current?.focus();
    if (element === null || from === undefined || typeof element.animate !== 'function' || prefersReducedMotion()) {
      return;
    }

    // FLIP: start the dialog where the folded card is, and let it grow into its own place.
    const to = element.getBoundingClientRect();
    if (to.width === 0 || to.height === 0) {
      return;
    }
    const dx = from.left + from.width / 2 - (to.left + to.width / 2);
    const dy = from.top + from.height / 2 - (to.top + to.height / 2);
    element.animate(
      [
        { transform: `translate(${String(dx)}px, ${String(dy)}px) scale(${String(from.width / to.width)}, ${String(from.height / to.height)})`, opacity: 0.6 },
        { transform: 'none', opacity: 1 },
      ],
      { duration: 320, easing: 'cubic-bezier(.2,.8,.2,1)' },
    );
  }, [card.slug]);

  // The page behind does not scroll while a card is open.
  useEffect(() => {
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = previous;
    };
  }, []);

  const onKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }
    if (event.key !== 'Tab' || dialog.current === null) {
      return;
    }
    const focusable = [...dialog.current.querySelectorAll<HTMLElement>('a[href], button, [tabindex]:not([tabindex="-1"])')];
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  };

  return createPortal(
    <div className="rc-pcard-overlay" onKeyDown={onKeyDown}>
      <button type="button" className="rc-pcard-overlay__backdrop" tabIndex={-1} aria-label={t('problems.close')} onClick={onClose} />
      <div ref={dialog} className={`rc-pcard rc-pcard--open k-${card.class}`} role="dialog" aria-modal="true" aria-labelledby="open-card-name">
        <div className="rc-pcard__band">
          <span className="rc-pcard__band-label">
            <span>{className}</span>
            <a href={`#${card.slug}`} aria-label={t('problems.anchor', { name: card.name })}>
              {t('problems.slug', { slug: card.slug })}
            </a>
          </span>
          <span className="rc-pcard__seal" aria-hidden="true" />
        </div>
        <div className="rc-pcard__face">
          <div className="rc-pcard__open-head">
            <h2 id="open-card-name" className="rc-pcard__name">
              {card.name}
            </h2>
            <button ref={closeButton} type="button" className="icon-button" aria-label={t('problems.close')} onClick={onClose}>
              <Icon name="close" />
            </button>
          </div>
          <span className="rc-pcard__rule" aria-hidden="true" />
          <div className="rc-pcard__summary">
            <Markdown source={card.summary} />
          </div>
          <div className="rc-pcard__sections">
            <CardSectionsView sections={card.sections} />
          </div>
        </div>
      </div>
    </div>,
    document.body,
  );
}

/** Puts a card's slug in the address — or takes it out — without a navigation, keeping the router's state. */
function replaceHash(slug: string | null) {
  const url = `${window.location.pathname}${window.location.search}${slug === null ? '' : `#${slug}`}`;
  window.history.replaceState(window.history.state, '', url);
}

function prefersReducedMotion(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/** Every term appears in the name, the summary or a keyword, ignoring case and ё. */
function matches(card: ProblemCard, terms: readonly string[]): boolean {
  const haystack = normalise([card.name, card.summary, ...card.keywords].join('\n'));
  return terms.every((term) => haystack.includes(term));
}

function normalise(text: string): string {
  return text.toLocaleLowerCase('ru').replaceAll('ё', 'е');
}
