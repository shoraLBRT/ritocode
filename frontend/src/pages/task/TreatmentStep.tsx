import type { CandidateCard, TreatmentTree } from '../../api';
import { useT } from '../../i18n';
import type { Answer } from './answer';

/**
 * Step 2 — what do you do with it here? (docs/SPEC.md §4.4) For every picked card, the whole
 * treatment tree: a branch opens to its leaves, and one or more leaves can be ticked in any
 * branches. Branches are native disclosure widgets and leaves checkboxes, so the step works from
 * the keyboard. A branch holding a ticked leaf stays open.
 */
export function TreatmentStep({
  cards,
  tree,
  answer,
  onToggleLeaf,
}: {
  cards: readonly CandidateCard[];
  tree: TreatmentTree;
  answer: Answer;
  onToggleLeaf: (card: string, leaf: string) => void;
}) {
  const t = useT();

  if (answer.length === 0) {
    return <p className="page__note">{t('task.nothingPicked')}</p>;
  }

  return (
    <div className="treatment">
      <p className="page__note">{t('task.step2Hint')}</p>

      {answer.map((pick) => {
        const name = cards.find((card) => card.slug === pick.card)?.name ?? pick.card;

        return (
          <fieldset key={pick.card} className="choice-group">
            <legend>{name}</legend>

            {tree.branches.map((branch) => {
              const ticked = branch.leaves.some((leaf) => pick.leaves.includes(leaf.id));

              return (
                <details key={branch.id} className="branch" open={ticked || undefined}>
                  <summary>{branch.name}</summary>
                  {branch.leaves.map((leaf) => (
                    <label key={leaf.id} className="choice choice--leaf">
                      <input
                        type="checkbox"
                        checked={pick.leaves.includes(leaf.id)}
                        onChange={() => {
                          onToggleLeaf(pick.card, leaf.id);
                        }}
                      />
                      <span>{leaf.label}</span>
                    </label>
                  ))}
                </details>
              );
            })}
          </fieldset>
        );
      })}
    </div>
  );
}
