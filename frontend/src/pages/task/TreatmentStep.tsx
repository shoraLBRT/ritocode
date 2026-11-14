import { useState } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';
import type { CandidateCard, TreatmentTree } from '../../api';
import { useT } from '../../i18n';
import type { MessageKey } from '../../i18n';
import type { Answer } from './answer';
import { useRisingDock } from './dock';
import { Chip, DockGrip, FilterChips, Highlighted, Icon, Option } from './parts';
import { rank, stems } from './search';
import { classTint, toggled } from './tint';

/** The branches the screen knows how to describe; the tree is fixed product structure (SPEC §3.3). */
const BRANCHES = ['brief', 'rule', 'auto', 'manual', 'accept'] as const;
type KnownBranch = (typeof BRANCHES)[number];

function known(branch: string): branch is KnownBranch {
  return (BRANCHES as readonly string[]).includes(branch);
}

/**
 * Step 2 — what do you do with it here? (docs/SPEC.md §4.4, docs/DESIGN.md › The task workspace)
 * The picked findings are compact rows in the right column, each with how many measures it has; the
 * current one shows its summary. The dock at the bottom of the code holds the step: the treatment
 * search, the branch filters and the leaves — every branch in columns, or the matches — rising on
 * scroll like step 1's. What is ticked for the current finding shows as chips under the field, and
 * every leaf says where its measure lives.
 *
 * The check button sits beside the field, handed in by the page, which knows whether checking is
 * possible; `notice` is what the page has to say under it — a hint, an error, the sign-in prompt.
 *
 * It renders two parts the page places: the right column and the dock.
 */
export function TreatmentStep({
  cards,
  tree,
  answer,
  onToggleLeaf,
  action,
  notice,
  onBack,
}: {
  cards: readonly CandidateCard[];
  tree: TreatmentTree;
  answer: Answer;
  onToggleLeaf: (card: string, leaf: string) => void;
  action: ReactNode;
  notice?: ReactNode;
  onBack?: () => void;
}) {
  const t = useT();
  const [current, setCurrent] = useState(0);
  const [query, setQuery] = useState('');
  const [branchFilter, setBranchFilter] = useState<readonly string[]>([]);
  const [active, setActive] = useState(0);
  const { raised, setRaised, attach, reset } = useRisingDock();

  const short = (branch: string) => (known(branch) ? t(`task.branchShort.${branch}` as MessageKey) : branch);
  const where = (branch: string) => (known(branch) ? t(`task.branchWhere.${branch}` as MessageKey) : null);
  const leaves = tree.branches.flatMap((branch) => branch.leaves.map((leaf) => ({ ...leaf, branch: branch.id, branchName: branch.name })));
  const leafLabel = (id: string) => {
    const leaf = leaves.find((each) => each.id === id);
    return leaf === undefined ? id : `${short(leaf.branch)} · ${leaf.label}`;
  };

  const index = Math.min(current, Math.max(0, answer.length - 1));
  const pick = answer[index];
  const card = (slug: string) => cards.find((each) => each.slug === slug);
  const treated = answer.filter((each) => each.leaves.length > 0).length;
  const terms = stems(query);
  const matching = terms.length > 0 ? rank(leaves, terms, (leaf) => ({ name: leaf.label, summary: leaf.branchName, keywords: where(leaf.branch) ?? '' })) : leaves;
  const inFilter = (leaf: (typeof leaves)[number]) => branchFilter.length === 0 || branchFilter.includes(leaf.branch);
  const shown = terms.length > 0 || branchFilter.length > 0 ? matching.filter(inFilter) : null;
  const walked = Math.min(active, Math.max(0, (shown?.length ?? 0) - 1));

  const ticked = (leaf: string) => pick?.leaves.includes(leaf) === true;
  const toggle = (leaf: string) => {
    if (pick !== undefined) {
      onToggleLeaf(pick.card, leaf);
    }
  };
  const search = (nextQuery: string, nextFilter: readonly string[]) => {
    setQuery(nextQuery);
    setBranchFilter(nextFilter);
    setActive(0);
    reset(false);
  };
  const goTo = (position: number) => {
    setCurrent(position);
    search('', branchFilter);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (shown === null || shown.length === 0) {
      return;
    }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      setActive(Math.max(0, Math.min(shown.length - 1, walked + (event.key === 'ArrowDown' ? 1 : -1))));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const leaf = shown[walked];
      if (leaf !== undefined) {
        toggle(leaf.id);
      }
    }
  };

  const back =
    onBack === undefined ? null : (
      <button type="button" className="link-button" onClick={onBack}>
        {t('task.backToStep1')}
      </button>
    );

  const aside = (
    <aside className="panel workspace__aside" aria-labelledby="findings-title">
      <div className="aside__head">
        <h2 id="findings-title" className="aside__title">
          {t('task.findings')}
        </h2>
        <span className="aside__count">{answer.length}</span>
      </div>
      {answer.length === 0 ? (
        <p className="aside__empty">{t('task.nothingPickedYet')}</p>
      ) : (
        <ol className="findings" aria-label={t('task.findings')}>
          {answer.map((each, position) => {
            const found = card(each.card);
            const count = each.leaves.length;
            const isCurrent = position === index;
            return (
              <li key={each.card} className={`finding${isCurrent ? ' finding--current' : ''}${count > 0 ? ' finding--done' : ''}${classTint(found?.class)}`}>
                <button
                  type="button"
                  className="finding__head"
                  aria-current={isCurrent ? 'step' : undefined}
                  onClick={() => {
                    goTo(position);
                  }}
                >
                  <span className="finding__mark" aria-hidden="true">
                    {count > 0 ? '✓' : position + 1}
                  </span>
                  <span className="finding__name">{found?.name ?? each.card}</span>
                  <span className="finding__status">{count > 0 ? t('task.leafCount', { count }) : t('task.needsLeafShort')}</span>
                </button>
                {isCurrent && found !== undefined && <p className="finding__summary">{found.summary}</p>}
              </li>
            );
          })}
        </ol>
      )}
      <div className="aside__foot">
        {index < answer.length - 1 && (
          <button
            type="button"
            className="link-button"
            onClick={() => {
              goTo(index + 1);
            }}
          >
            {t('task.nextFinding')}
          </button>
        )}
        {back}
      </div>
    </aside>
  );

  if (pick === undefined) {
    return (
      <>
        {aside}
        <section className="dock" aria-labelledby="dock-title">
          <div className="dock__controls">
            <div className="dock__head">
              <h2 id="dock-title" className="dock__title">
                {t('task.step2')}
              </h2>
            </div>
            <p className="step__note">{t('task.nothingPicked')}</p>
            <div className="search-row search-row--end">{action}</div>
            {notice}
          </div>
        </section>
      </>
    );
  }

  const pickedCard = card(pick.card);
  const pickedName = pickedCard?.name ?? pick.card;

  return (
    <>
      {aside}
      <section className={`dock dock--leaves dock--results${raised ? ' dock--raised' : ''}`} aria-labelledby="dock-title">
        <DockGrip
          raised={raised}
          onToggle={() => {
            setRaised(!raised);
          }}
        />
        <div className="dock__controls">
          <div className="dock__head">
            <h2 id="dock-title" className="dock__title">
              {t('task.step2')}
            </h2>
            <span className="dock__count">{t('task.treated', { done: treated, total: answer.length })}</span>
          </div>
          <div className={`dock__subject${classTint(pickedCard?.class)}`} aria-live="polite">
            <p className="dock__finding">{pickedName}</p>
            <p className="dock__ask">{t('task.step2Ask')}</p>
          </div>

          <div className="search-row">
            <div className="search-field">
              <Icon name="search" />
              <input
                type="text"
                aria-label={t('task.leafSearch')}
                value={query}
                placeholder={t('task.leafPlaceholder', { card: pickedName })}
                autoComplete="off"
                onChange={(event) => {
                  search(event.target.value, branchFilter);
                }}
                onKeyDown={onKeyDown}
              />
              {query.length > 0 && (
                <button
                  type="button"
                  className="search-field__clear"
                  aria-label={t('task.clearSearch')}
                  onClick={() => {
                    search('', branchFilter);
                  }}
                >
                  <Icon name="close" />
                </button>
              )}
            </div>
            {action}
          </div>

          {notice}

          {pick.leaves.length > 0 && (
            <ul className="chips" aria-label={t('task.pickedLeaves')}>
              {pick.leaves.map((leaf) => (
                <Chip
                  key={leaf}
                  label={leafLabel(leaf)}
                  removeLabel={t('task.remove', { name: leafLabel(leaf) })}
                  onRemove={() => {
                    toggle(leaf);
                  }}
                />
              ))}
            </ul>
          )}

          <div className="dock__filters">
            <FilterChips
              label={t('task.branchFilter')}
              options={tree.branches.map((branch) => ({ id: branch.id, label: short(branch.id), count: matching.filter((leaf) => leaf.branch === branch.id).length }))}
              selected={branchFilter}
              onToggle={(id) => {
                setBranchFilter((list) => toggled(list, id));
                setActive(0);
                reset(false);
              }}
            />
            {shown !== null && (
              <span className="dock__count" role="status">
                {t('task.leafMatches', { count: shown.length })}
              </span>
            )}
          </div>
        </div>

        <div ref={attach} className="dock__results">
          {shown === null ? (
            <div className="leaf-columns">
              {tree.branches.map((branch) => (
                <section key={branch.id} className="leaf-group" aria-labelledby={`branch-${branch.id}`}>
                  <h3 id={`branch-${branch.id}`} className="leaf-group__name">
                    {branch.name}
                  </h3>
                  {where(branch.id) !== null && <p className="leaf-group__where">{where(branch.id)}</p>}
                  {branch.leaves.map((leaf) => (
                    <Option
                      key={leaf.id}
                      checked={ticked(leaf.id)}
                      onToggle={() => {
                        toggle(leaf.id);
                      }}
                      name={leaf.label}
                    />
                  ))}
                </section>
              ))}
            </div>
          ) : shown.length === 0 ? (
            <p className="dock__empty">{t('task.noMatch')}</p>
          ) : (
            <div className="leaf-matches">
              {shown.map((leaf, position) => (
                <Option
                  key={leaf.id}
                  checked={ticked(leaf.id)}
                  active={terms.length > 0 && position === walked}
                  onToggle={() => {
                    toggle(leaf.id);
                  }}
                  name={<Highlighted text={leaf.label} query={terms} />}
                  summary={where(leaf.branch) ?? leaf.branchName}
                  tag={short(leaf.branch)}
                />
              ))}
            </div>
          )}
        </div>
      </section>
    </>
  );
}
