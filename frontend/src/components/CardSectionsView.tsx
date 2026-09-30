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
 * empty are left out. The problem catalogue and the review both show a card this way.
 */
export function CardSectionsView({ sections }: { sections: CardSections }) {
  const t = useT();

  return (
    <>
      {sectionOrder.map((section) => {
        const text = sections[section];

        return (
          text !== null && (
            <div key={section} className="problem-card__section">
              <h4>{t(`problems.sections.${section}`)}</h4>
              <Markdown source={text} />
            </div>
          )
        );
      })}
    </>
  );
}
