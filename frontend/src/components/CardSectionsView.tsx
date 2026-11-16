import type { CardSections } from '../api';
import { useT } from '../i18n';
import { Markdown } from './Markdown';

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
 * A card's long fields, each under its heading and rendered from Markdown; the ones a card leaves
 * empty are left out. The problem catalogue and the review both show a card this way; the review folds
 * each under its heading.
 */
export function CardSectionsView({ sections, folded = false }: { sections: CardSections; folded?: boolean }) {
  const t = useT();

  return (
    <>
      {sectionOrder.map((section) => {
        const text = sections[section];
        if (text === null) {
          return null;
        }

        // On the review the sections are reference, not the point (#258): each opens on a click.
        return folded ? (
          <details key={section} className="problem-card__section problem-card__section--folded">
            <summary>{t(`problems.sections.${section}`)}</summary>
            <Markdown source={text} />
          </details>
        ) : (
          <div key={section} className="problem-card__section">
            <h4>{t(`problems.sections.${section}`)}</h4>
            <Markdown source={text} />
          </div>
        );
      })}
    </>
  );
}
