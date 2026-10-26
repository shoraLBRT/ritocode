import { useState } from 'react';
import type { KeyboardEvent } from 'react';
import type { CandidateCard, ProblemClass } from '../../api';
import { useT } from '../../i18n';
import type { Answer } from './answer';
import { BrowseGroup, Chip, FilterChips, Highlighted, Icon, Option } from './parts';
import { rank, stems } from './search';
import { toggled } from './tint';

/**
 * Step 1 — what do you see here? (docs/SPEC.md §4.4, docs/DESIGN.md) Search first: the learner
 * describes what they see in their own words, and the cards that share a word come up as they type,
 * the matched words marked — name and summary only, never more of a card. What they pick shows as
 * chips under the field, in the card's class colour. Class chips narrow the cards; with neither a
 * query nor a class, the catalogue is offered by class, folded — reachable, but not the way in.
 *
 * Arrow keys walk the matches from the field and Enter ticks the one walked to. The button beside
 * the field moves on: to treatment, or — when nothing is picked — to checking a clean answer.
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
  const [opened, setOpened] = useState<readonly string[]>([]);
  const [active, setActive] = useState(0);

  const terms = stems(query);
  const searchable = (card: CandidateCard) => ({ name: card.name, summary: card.summary, keywords: card.keywords.join(' ') });
  const matching = terms.length > 0 ? rank(cards, terms, searchable) : cards;
  const inFilter = (card: CandidateCard) => classFilter.length === 0 || classFilter.includes(card.class);
  const shown = terms.length > 0 || classFilter.length > 0 ? matching.filter(inFilter) : null;
  const current = Math.min(active, Math.max(0, (shown?.length ?? 0) - 1));
  const picked = (slug: string) => answer.some((pick) => pick.card === slug);
  const className = (id: string) => classes.find((each) => each.id === id)?.name ?? id;

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

  return (
    <div className="step">
      <div className="step__head">
        <h2 className="step__title">{t('task.step1')}</h2>
        <span className="step__count">{answer.length > 0 ? t('task.picked', { count: answer.length }) : t('task.nothingPickedYet')}</span>
      </div>

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
              setQuery(event.target.value);
              setActive(0);
            }}
            onKeyDown={onKeyDown}
          />
          {query.length > 0 && (
            <button
              type="button"
              className="search-field__clear"
              aria-label={t('task.clearSearch')}
              onClick={() => {
                setQuery('');
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

      {answer.length > 0 && (
        <ul className="chips" aria-label={t('task.pickedCards')}>
          {answer.map((pick) => {
            const card = cards.find((each) => each.slug === pick.card);
            const name = card?.name ?? pick.card;
            return (
              <Chip
                key={pick.card}
                label={name}
                removeLabel={t('task.remove', { name })}
                problemClass={card?.class}
                onRemove={() => {
                  onToggle(pick.card);
                }}
              />
            );
          })}
        </ul>
      )}

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
        }}
      />

      <div className="step__options">
        {shown !== null ? (
          <>
            <p className="step__label">{terms.length > 0 ? t('task.matches', { count: shown.length }) : t('task.filteredByClass')}</p>
            {shown.length === 0 && <p className="step__empty">{t('task.noMatch')}</p>}
            {shown.map((card, index) => (
              <Option
                key={card.slug}
                checked={picked(card.slug)}
                active={terms.length > 0 && index === current}
                onToggle={() => {
                  onToggle(card.slug);
                }}
                name={<Highlighted text={card.name} query={terms} />}
                summary={<Highlighted text={card.summary} query={terms} />}
                tag={className(card.class)}
                problemClass={card.class}
              />
            ))}
          </>
        ) : (
          <>
            <p className="step__browse">{t('task.browse')}</p>
            {classes.map((each) => {
              const group = cards.filter((card) => card.class === each.id);
              return (
                group.length > 0 && (
                  <BrowseGroup
                    key={each.id}
                    name={each.name}
                    description={each.description}
                    count={group.length}
                    problemClass={each.id}
                    open={opened.includes(each.id)}
                    onToggle={() => {
                      setOpened((list) => toggled(list, each.id));
                    }}
                  >
                    {group.map((card) => (
                      <Option
                        key={card.slug}
                        quiet
                        checked={picked(card.slug)}
                        onToggle={() => {
                          onToggle(card.slug);
                        }}
                        name={card.name}
                        summary={card.summary}
                      />
                    ))}
                  </BrowseGroup>
                )
              );
            })}
          </>
        )}
      </div>
    </div>
  );
}
