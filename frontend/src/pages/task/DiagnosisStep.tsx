import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import type { CandidateCard, ProblemClass } from '../../api';
import { Markdown } from '../../components/Markdown';
import { useT } from '../../i18n';
import type { Answer } from './answer';
import { useRisingDock } from './dock';
import { DockBar, FilterChips, Highlighted, Icon } from './parts';
import { rank, stems } from './search';
import { classTint, toggled } from './tint';

/**
 * Step 1 — what do you see here? (docs/SPEC.md §4.4, docs/DESIGN.md › The task workspace) Search
 * first, in the dock at the bottom of the code: the learner describes what they see in their own
 * words, or presses a class, and the dock rises with the matching cards under the field — the
 * catalogue's cards, four or so to a row, the matched words marked. Each card says *Подробнее* and
 * has its own *Отметить*. With nothing typed and no class on, the dock is the field and the
 * filters, and the code is all there is to read.
 *
 * An opened card shows only what a task may show of a card (SPEC §3.2): the class, the full summary
 * and the class's description — never the signs or the treatment. It opens over the code, walks the
 * matches with ‹ n из N ›, and closes with ×, Esc or a click beside it.
 *
 * What is picked is listed in the right column. Arrow keys walk the matches from the field and
 * Enter ticks the one walked to. The button beside the field moves on: to treatment, or — when
 * nothing is picked — to checking a clean answer.
 *
 * It renders three parts the page places: the right column, the dock, and the opened card.
 */
export function DiagnosisStep({
  classes,
  cards,
  answer,
  onToggle,
  onNext,
}: {
  classes: readonly ProblemClass[];
  cards: readonly CandidateCard[];
  answer: Answer;
  onToggle: (card: string) => void;
  onNext: () => void;
}) {
  const t = useT();
  const [query, setQuery] = useState('');
  const [classFilter, setClassFilter] = useState<readonly string[]>([]);
  const [active, setActive] = useState(0);
  const [opened, setOpened] = useState<string | null>(null);
  const { raised, toggle, attach, reset, collapsed, toggleCollapsed } = useRisingDock();

  const terms = stems(query);
  const searchable = (card: CandidateCard) => ({ name: card.name, summary: card.summary, keywords: card.keywords.join(' ') });
  const matching = terms.length > 0 ? rank(cards, terms, searchable) : cards;
  const inFilter = (card: CandidateCard) => classFilter.length === 0 || classFilter.includes(card.class);
  const shown = terms.length > 0 || classFilter.length > 0 ? matching.filter(inFilter) : null;
  const count = shown === null ? '' : terms.length > 0 ? t('task.matches', { count: shown.length }) : t('task.cardCount', { count: shown.length });
  const current = Math.min(active, Math.max(0, (shown?.length ?? 0) - 1));
  const picked = (slug: string) => answer.some((pick) => pick.card === slug);
  const className = (id: string) => classes.find((each) => each.id === id)?.name ?? id;
  const openIndex = shown === null || opened === null ? -1 : shown.findIndex((card) => card.slug === opened);

  // The card walked to with the arrow keys stays in view as the dock scrolls.
  const walked = shown?.[current]?.slug;
  useEffect(() => {
    if (walked !== undefined && terms.length > 0) {
      const element = document.getElementById(cardId(walked));
      if (typeof element?.scrollIntoView === 'function') {
        element.scrollIntoView({ block: 'nearest' });
      }
    }
  }, [walked, terms.length]);

  const search = (nextQuery: string, nextFilter: readonly string[]) => {
    setQuery(nextQuery);
    setClassFilter(nextFilter);
    setActive(0);
    reset(stems(nextQuery).length === 0 && nextFilter.length === 0);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (shown === null || shown.length === 0) {
      return;
    }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      setActive(Math.max(0, Math.min(shown.length - 1, current + (event.key === 'ArrowDown' ? 1 : -1))));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const card = shown[current];
      if (card !== undefined) {
        onToggle(card.slug);
      }
    }
  };

  // Focus goes back to the card the open one came from, once the dock is no longer inert.
  const returnFocus = useRef<string | null>(null);
  useEffect(() => {
    if (opened === null && returnFocus.current !== null) {
      document.getElementById(`${cardId(returnFocus.current)}-open`)?.focus();
      returnFocus.current = null;
    }
  }, [opened]);

  const close = () => {
    returnFocus.current = opened;
    setOpened(null);
  };

  return (
    <>
      <aside className="panel workspace__aside picked" aria-labelledby="picked-title">
        <div className="aside__head">
          <h2 id="picked-title" className="aside__title">
            {t('task.pickedTitle')}
          </h2>
          <span className="aside__count">{answer.length}</span>
        </div>
        {answer.length === 0 ? (
          <p className="aside__empty">{t('task.nothingPickedYet')}</p>
        ) : (
          <ul className="picked__list" aria-label={t('task.pickedCards')}>
            {answer.map((pick) => {
              const card = cards.find((each) => each.slug === pick.card);
              const name = card?.name ?? pick.card;
              return (
                <li key={pick.card} className={`picked__row${classTint(card?.class)}`}>
                  <span className="dot" aria-hidden="true" />
                  <span className="picked__name">{name}</span>
                  <button
                    type="button"
                    className="picked__remove"
                    aria-label={t('task.remove', { name })}
                    onClick={() => {
                      onToggle(pick.card);
                    }}
                  >
                    <Icon name="close" />
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </aside>

      <section
        className={`dock dock--cards${shown !== null ? ' dock--results' : ''}${shown !== null && raised ? ' dock--raised' : ''}${collapsed ? ' dock--collapsed' : ''}`}
        aria-labelledby="dock-title"
        inert={openIndex >= 0}
      >
        <DockBar
          title={t('task.step1')}
          summary={[query.trim(), count].filter((part) => part.length > 0).join(' · ')}
          collapsed={collapsed}
          onCollapse={toggleCollapsed}
          grip={shown !== null ? { raised, onToggle: toggle } : undefined}
        />
        <div className="dock__controls" hidden={collapsed}>
          <h2 id="dock-title" className="visually-hidden">
            {t('task.step1')}
          </h2>
          <div className="search-row">
            <div className="search-field">
              <Icon name="search" />
              <input
                type="text"
                aria-label={t('task.search')}
                value={query}
                placeholder={t('task.searchPlaceholder')}
                autoComplete="off"
                onChange={(event) => {
                  search(event.target.value, classFilter);
                }}
                onKeyDown={onKeyDown}
              />
              {query.length > 0 && (
                <button
                  type="button"
                  className="search-field__clear"
                  aria-label={t('task.clearSearch')}
                  onClick={() => {
                    search('', classFilter);
                  }}
                >
                  <Icon name="close" />
                </button>
              )}
            </div>
            <button type="button" className="button button--primary" onClick={onNext}>
              {answer.length > 0 ? t('task.toStep2') : t('task.toStep2Clean')}
              <Icon name="arrow" />
            </button>
          </div>

          <div className="dock__filters">
            <FilterChips
              label={t('task.classFilter')}
              options={classes.map((each) => ({
                id: each.id,
                label: each.name,
                count: matching.filter((card) => card.class === each.id).length,
                problemClass: each.id,
              }))}
              selected={classFilter}
              onToggle={(id) => {
                setClassFilter((list) => toggled(list, id));
                setActive(0);
                reset(terms.length === 0 && toggled(classFilter, id).length === 0);
              }}
            />
            {shown !== null && (
              <span className="dock__count" role="status">
                {count}
              </span>
            )}
          </div>
        </div>

        {shown !== null && (
          <div ref={attach} className="dock__results" hidden={collapsed}>
            {shown.length === 0 ? (
              <p className="dock__empty">{t('task.noMatch')}</p>
            ) : (
              <div className="dock__cards">
                {shown.map((card, index) => (
                  <DockCard
                    key={card.slug}
                    card={card}
                    className={className(card.class)}
                    terms={terms}
                    picked={picked(card.slug)}
                    active={terms.length > 0 && index === current}
                    onToggle={() => {
                      onToggle(card.slug);
                    }}
                    onOpen={() => {
                      setOpened(card.slug);
                    }}
                  />
                ))}
              </div>
            )}
          </div>
        )}
      </section>

      {shown !== null && openIndex >= 0 && (
        <OpenedCard
          cards={shown}
          index={openIndex}
          classes={classes}
          picked={picked}
          onToggle={onToggle}
          onMove={(index) => {
            const card = shown[(index + shown.length) % shown.length];
            if (card !== undefined) {
              setOpened(card.slug);
            }
          }}
          onClose={close}
        />
      )}
    </>
  );
}

function cardId(slug: string): string {
  return `dock-card-${slug}`;
}

/**
 * A match in the dock: the catalogue's folded card (DESIGN.md › The problem catalogue) made small —
 * the class on its band, the name, the rule, the summary — with *Подробнее*, which opens it, and its
 * own *Отметить*, a real checkbox named by the card.
 */
function DockCard({
  card,
  className,
  terms,
  picked,
  active,
  onToggle,
  onOpen,
}: {
  card: CandidateCard;
  className: string;
  terms: readonly string[];
  picked: boolean;
  active: boolean;
  onToggle: () => void;
  onOpen: () => void;
}) {
  const t = useT();
  const id = cardId(card.slug);

  return (
    <article
      id={id}
      className={`rc-pcard rc-pcard--dock k-${card.class}${picked ? ' rc-pcard--picked' : ''}${active ? ' rc-pcard--active' : ''}`}
      aria-labelledby={`${id}-name`}
    >
      <div className="rc-pcard__band">
        <span>{className}</span>
        <span className="rc-pcard__seal" aria-hidden="true" />
      </div>
      <div className="rc-pcard__face">
        <h3 id={`${id}-name`} className="rc-pcard__name">
          <Highlighted text={card.name} query={terms} />
        </h3>
        <span className="rc-pcard__rule" aria-hidden="true" />
        <div className="rc-pcard__summary">
          <p>
            <Highlighted text={card.summary} query={terms} />
          </p>
        </div>
        <div className="rc-pcard__foot">
          <button id={`${id}-open`} type="button" className="rc-pcard__open" aria-label={t('task.openCard', { name: card.name })} onClick={onOpen}>
            {t('task.more')}
          </button>
          <label className="rc-pcard__pick">
            <input type="checkbox" className="rc-pcard__check" checked={picked} onChange={onToggle} aria-labelledby={`${id}-name ${id}-pick`} />
            <span id={`${id}-pick`}>{picked ? t('task.pickedShort') : t('task.pick')}</span>
          </label>
        </div>
      </div>
    </article>
  );
}

/**
 * The opened card, over the code only: the class, the full summary and the class's description
 * (SPEC §3.2 — no signs, no treatment), ‹ n из N › through the matches, and *Отметить*. A dialog:
 * focus moves into it and stays there, and ×, Esc or a click on the dimmed code closes it.
 */
function OpenedCard({
  cards,
  index,
  classes,
  picked,
  onToggle,
  onMove,
  onClose,
}: {
  cards: readonly CandidateCard[];
  index: number;
  classes: readonly ProblemClass[];
  picked: (slug: string) => boolean;
  onToggle: (slug: string) => void;
  onMove: (index: number) => void;
  onClose: () => void;
}) {
  const t = useT();
  const dialog = useRef<HTMLDivElement>(null);
  const closeButton = useRef<HTMLButtonElement>(null);
  const card = cards[index];
  const problemClass = classes.find((each) => each.id === card?.class);

  useLayoutEffect(() => {
    closeButton.current?.focus();
  }, []);

  if (card === undefined) {
    return null;
  }

  const on = picked(card.slug);

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }
    if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
      event.preventDefault();
      onMove(index + (event.key === 'ArrowRight' ? 1 : -1));
      return;
    }
    if (event.key !== 'Tab' || dialog.current === null) {
      return;
    }
    const focusable = [...dialog.current.querySelectorAll<HTMLElement>('button:not(:disabled)')];
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

  return (
    <div className="dock-open" onKeyDown={onKeyDown}>
      <button type="button" className="dock-open__dim" tabIndex={-1} aria-label={t('problems.close')} onClick={onClose} />
      <div ref={dialog} className={`rc-pcard rc-pcard--peek k-${card.class}`} role="dialog" aria-modal="true" aria-labelledby="dock-open-name">
        <div className="rc-pcard__band">
          <span>{problemClass?.name ?? card.class}</span>
          <span className="rc-pcard__seal" aria-hidden="true" />
        </div>
        <div className="rc-pcard__face">
          <div className="rc-pcard__open-head">
            <h3 id="dock-open-name" className="rc-pcard__name">
              {card.name}
            </h3>
            <button ref={closeButton} type="button" className="icon-button" aria-label={t('problems.close')} onClick={onClose}>
              <Icon name="close" />
            </button>
          </div>
          <span className="rc-pcard__rule" aria-hidden="true" />
          <div className="rc-pcard__summary">
            <Markdown source={card.summary} />
          </div>
          {problemClass?.description != null && (
            <p className="rc-pcard__class-note">
              <strong>{problemClass.name}</strong>
              {t('task.classNote', { description: problemClass.description })}
            </p>
          )}
          <div className="rc-pcard__walk">
            {cards.length > 1 && (
              <>
                <button
                  type="button"
                  className="nav-button"
                  aria-label={t('task.previousCard')}
                  onClick={() => {
                    onMove(index - 1);
                  }}
                >
                  <Icon name="left" />
                </button>
                <span className="rc-pcard__position">{t('task.cardPosition', { index: index + 1, count: cards.length })}</span>
                <button
                  type="button"
                  className="nav-button"
                  aria-label={t('task.nextCard')}
                  onClick={() => {
                    onMove(index + 1);
                  }}
                >
                  <Icon name="right" />
                </button>
              </>
            )}
            <button
              type="button"
              className={`button${on ? '' : ' button--primary'} rc-pcard__mark`}
              aria-pressed={on}
              onClick={() => {
                onToggle(card.slug);
              }}
            >
              {on ? t('task.pickedLong') : t('task.pickLong')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
