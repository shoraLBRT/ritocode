import { useState } from 'react';
import type { CandidateCard, ProblemClass } from '../../api';
import { EmptyState } from '../../components/EmptyState';
import { useT } from '../../i18n';
import type { Answer } from './answer';

/**
 * Step 1 — what do you see here? (docs/SPEC.md §4.4) The cards the task offers — the shortlist for
 * an easy task — by name and summary only, grouped by class, with a search over name, summary and
 * keywords. Each card is a checkbox, so the step works from the keyboard as it does by hand.
 */
export function DiagnosisStep({
  classes,
  cards,
  answer,
  onToggle,
}: {
  classes: readonly ProblemClass[];
  cards: readonly CandidateCard[];
  answer: Answer;
  onToggle: (card: string) => void;
}) {
  const t = useT();
  const [query, setQuery] = useState('');

  const terms = normalise(query).split(/\s+/).filter((term) => term.length > 0);
  const shown = cards.filter((card) => {
    const haystack = normalise([card.name, card.summary, ...card.keywords].join('\n'));
    return terms.every((term) => haystack.includes(term));
  });

  return (
    <div className="diagnosis">
      <p className="page__note">{t('task.step1Hint')}</p>

      <label className="filters__field">
        <span>{t('task.search')}</span>
        <input
          type="search"
          value={query}
          placeholder={t('task.searchPlaceholder')}
          onChange={(event) => {
            setQuery(event.target.value);
          }}
        />
      </label>

      {shown.length === 0 && <EmptyState>{t('task.noMatch')}</EmptyState>}

      {classes.map((problemClass) => {
        const group = shown.filter((card) => card.class === problemClass.id);

        return (
          group.length > 0 && (
            <fieldset key={problemClass.id} className="choice-group">
              <legend>{problemClass.name}</legend>
              {group.map((card) => (
                <label key={card.slug} className="choice">
                  <input
                    type="checkbox"
                    checked={answer.some((pick) => pick.card === card.slug)}
                    onChange={() => {
                      onToggle(card.slug);
                    }}
                  />
                  <span>
                    <span className="choice__name">{card.name}</span>
                    <span className="choice__summary">{card.summary}</span>
                  </span>
                </label>
              ))}
            </fieldset>
          )
        );
      })}
    </div>
  );
}

function normalise(text: string): string {
  return text.toLocaleLowerCase('ru').replaceAll('ё', 'е');
}
