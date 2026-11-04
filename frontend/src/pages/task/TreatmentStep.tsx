import { useState } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';
import type { CandidateCard, TreatmentTree } from '../../api';
import { useT } from '../../i18n';
import type { MessageKey } from '../../i18n';
import type { Answer } from './answer';
import { BrowseGroup, Chip, FilterChips, Highlighted, Icon, Option } from './parts';
import { rank, stems } from './search';
import { classTint, toggled } from './tint';

/** The branches the screen knows how to describe; the tree is fixed product structure (SPEC §3.3). */
const BRANCHES = ['brief', 'rule', 'auto', 'manual', 'accept'] as const;
type KnownBranch = (typeof BRANCHES)[number];

function known(branch: string): branch is KnownBranch {
  return (BRANCHES as readonly string[]).includes(branch);
}

/**
 * Step 2 — what do you do with it here? (docs/SPEC.md §4.4, docs/DESIGN.md) The picked findings
 * as a list, each with how many measures it has; the current one shows its summary. For it, the
 * learner searches the treatment leaves the way they searched the cards — or opens a branch —
 * and what they tick shows as chips. Every leaf says where its measure lives.
 *
 * The check button sits beside the field, handed in by the page, which knows whether checking is
 * possible; `notice` is what the page has to say under it — a hint, an error, the sign-in prompt.
 */
export function TreatmentStep({
  cards,
  tree,
  answer,
  onToggleLeaf,
  action,
  notice,
}: {
  cards: readonly CandidateCard[];
  tree: TreatmentTree;
  answer: Answer;
  onToggleLeaf: (card: string, leaf: string) => void;
  action: ReactNode;
  notice?: ReactNode;
}) {
  const t = useT();
  const [current, setCurrent] = useState(0);
  const [query, setQuery] = useState('');
  const [branchFilter, setBranchFilter] = useState<readonly string[]>([]);
  const [opened, setOpened] = useState<readonly string[]>([]);
  const [active, setActive] = useState(0);

  const short = (branch: string) => (known(branch) ? t(`task.branchShort.${branch}` as MessageKey) : branch);
  const where = (branch: string) => (known(branch) ? t(`task.branchWhere.${branch}` as MessageKey) : null);
  const leaves = tree.branches.flatMap((branch) => branch.leaves.map((leaf) => ({ ...leaf, branch: branch.id, branchName: branch.name })));
  const leafLabel = (id: string) => {
    const leaf = leaves.find((each) => each.id === id);
    return leaf === undefined ? id : `${short(leaf.branch)} · ${leaf.label}`;
  };

  if (answer.length === 0) {
    return (
      <div className="step">
        <div className="step__head">
          <h2 className="step__title">{t('task.step2')}</h2>
        </div>
        <p className="step__note">{t('task.nothingPicked')}</p>
        <div className="search-row search-row--end">{action}</div>
        {notice}
      </div>
    );
  }

  const index = Math.min(current, answer.length - 1);
  const pick = answer[index] ?? answer[0];
  const card = (slug: string) => cards.find((each) => each.slug === slug);
  const treated = answer.filter((each) => each.leaves.length > 0).length;
  const terms = stems(query);
  const matching = terms.length > 0 ? rank(leaves, terms, (leaf) => ({ name: leaf.label, summary: leaf.branchName, keywords: where(leaf.branch) ?? '' })) : leaves;
  const inFilter = (leaf: (typeof leaves)[number]) => branchFilter.length === 0 || branchFilter.includes(leaf.branch);
  const shown = terms.length > 0 || branchFilter.length > 0 ? matching.filter(inFilter) : null;
  const walked = Math.min(active, Math.max(0, (shown?.length ?? 0) - 1));

  if (pick === undefined) {
    return null;
  }

  const ticked = (leaf: string) => pick.leaves.includes(leaf);
  const toggle = (leaf: string) => {
    onToggleLeaf(pick.card, leaf);
  };
  const goTo = (position: number) => {
    setCurrent(position);
    setQuery('');
    setActive(0);
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

  const pickedCard = card(pick.card);
  const pickedName = pickedCard?.name ?? pick.card;

  return (
    <div className="step">
      <div className="step__head">
        <h2 className="step__title">{t('task.step2')}</h2>
        <span className="step__count">{t('task.treated', { done: treated, total: answer.length })}</span>
      </div>

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

      <FilterChips
        label={t('task.branchFilter')}
        options={tree.branches.map((branch) => ({ id: branch.id, label: short(branch.id), count: matching.filter((leaf) => leaf.branch === branch.id).length }))}
        selected={branchFilter}
        onToggle={(id) => {
          setBranchFilter((list) => toggled(list, id));
          setActive(0);
        }}
      />

      <div className="step__options">
        {shown !== null ? (
          <>
            <p className="step__label">{terms.length > 0 ? t('task.leafMatches', { count: shown.length }) : t('task.filteredByBranch')}</p>
            {shown.length === 0 && <p className="step__empty">{t('task.noMatch')}</p>}
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
          </>
        ) : (
          <>
            <p className="step__browse">{t('task.browseBranches')}</p>
            {tree.branches.map((branch) => (
              <BrowseGroup
                key={branch.id}
                name={branch.name}
                description={where(branch.id)}
                count={branch.leaves.length}
                open={opened.includes(branch.id)}
                onToggle={() => {
                  setOpened((list) => toggled(list, branch.id));
                }}
              >
                {branch.leaves.map((leaf) => (
                  <Option
                    key={leaf.id}
                    quiet
                    checked={ticked(leaf.id)}
                    onToggle={() => {
                      toggle(leaf.id);
                    }}
                    name={leaf.label}
                  />
                ))}
              </BrowseGroup>
            ))}
          </>
        )}
      </div>

      {index < answer.length - 1 && (
        <div className="step__foot">
          <button
            type="button"
            className="link-button"
            onClick={() => {
              goTo(index + 1);
            }}
          >
            {t('task.nextFinding')}
          </button>
        </div>
      )}
    </div>
  );
}
