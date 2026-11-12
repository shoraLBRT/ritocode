import type { ReactNode } from 'react';
import { useT } from '../../i18n';
import { highlight } from './search';
import { classTint } from './tint';

/**
 * The small pieces both steps and the review are built from (docs/DESIGN.md): an option a learner
 * ticks, a chip for what they picked, a row of filter chips, and the grip of the step's dock.
 *
 * A problem class travels as `k-{class}` on the element, which the stylesheet turns into that
 * class's colour; nothing here knows a colour.
 */

export function Icon({ name }: { name: 'search' | 'close' | 'check' | 'arrow' | 'chevron' | 'down' | 'left' | 'right' | 'file' | 'folder' | 'eye' }) {
  const paths: Record<typeof name, ReactNode> = {
    search: (
      <>
        <circle cx="11" cy="11" r="6.5" />
        <path d="M20 20l-4.2-4.2" />
      </>
    ),
    close: <path d="M6 6l12 12M18 6L6 18" />,
    check: <path d="M5 12l5 5 9-10" />,
    arrow: <path d="M5 12h14M13 6l6 6-6 6" />,
    chevron: <path d="M9 6l6 6-6 6" />,
    down: <path d="M6 9l6 6 6-6" />,
    left: <path d="M15 6l-6 6 6 6" />,
    right: <path d="M9 6l6 6-6 6" />,
    file: (
      <>
        <path d="M7 3h7l5 5v13H7z" />
        <path d="M14 3v5h5" />
      </>
    ),
    folder: <path d="M3 7h6l2 2h10v10H3z" />,
    eye: (
      <>
        <path d="M2.5 12s3.5-6.5 9.5-6.5 9.5 6.5 9.5 6.5-3.5 6.5-9.5 6.5S2.5 12 2.5 12z" />
        <circle cx="12" cy="12" r="2.6" />
      </>
    ),
  };

  return (
    <svg className="icon" viewBox="0 0 24 24" aria-hidden="true">
      {paths[name]}
    </svg>
  );
}

/** A text with the stretches the query matched marked. */
export function Highlighted({ text, query }: { text: string; query: readonly string[] }) {
  return (
    <>
      {highlight(text, query).map((fragment, index) =>
        fragment.match ? (
          <mark key={index} className="hit">
            {fragment.text}
          </mark>
        ) : (
          fragment.text
        ),
      )}
    </>
  );
}

/**
 * One option: a real checkbox under a drawn box, so it is ticked from the keyboard and named by
 * its text. A quiet option is one met by browsing rather than by searching.
 */
export function Option({
  checked,
  onToggle,
  name,
  summary,
  tag,
  problemClass,
  quiet = false,
  active = false,
}: {
  checked: boolean;
  onToggle: () => void;
  name: ReactNode;
  summary?: ReactNode;
  tag?: string;
  problemClass?: string | undefined;
  quiet?: boolean;
  active?: boolean;
}) {
  const kind = `option${quiet ? ' option--quiet' : ''}${active ? ' option--active' : ''}${checked ? ' option--on' : ''}${classTint(problemClass)}`;

  return (
    <label className={kind}>
      <input type="checkbox" className="option__input" checked={checked} onChange={onToggle} />
      <span className="option__box" aria-hidden="true">
        <Icon name="check" />
      </span>
      <span className="option__body">
        <span className="option__name">{name}</span>
        {summary !== undefined && <span className="option__summary">{summary}</span>}
      </span>
      {tag !== undefined && (
        <span className="option__tag">
          <span className="dot" aria-hidden="true" />
          {tag}
        </span>
      )}
    </label>
  );
}

/** Something the learner picked, in its class's colour, with a way to take it back. */
export function Chip({ label, removeLabel, onRemove, problemClass }: { label: string; removeLabel: string; onRemove: () => void; problemClass?: string | undefined }) {
  return (
    <li className={`chip${classTint(problemClass)}`}>
      <span className="chip__text">{label}</span>
      <button type="button" className="chip__remove" aria-label={removeLabel} onClick={onRemove}>
        <Icon name="close" />
      </button>
    </li>
  );
}

export interface FilterOption {
  readonly id: string;
  readonly label: string;
  readonly count: number;
  readonly problemClass?: string | undefined;
}

/** A row of toggles that narrow the options; any number may be on. */
export function FilterChips({ label, options, selected, onToggle }: { label: string; options: readonly FilterOption[]; selected: readonly string[]; onToggle: (id: string) => void }) {
  return (
    <div className="filter-chips" role="group" aria-label={label}>
      {options.map((option) => (
        <button
          key={option.id}
          type="button"
          className={`filter-chip${classTint(option.problemClass)}`}
          aria-pressed={selected.includes(option.id)}
          onClick={() => {
            onToggle(option.id);
          }}
        >
          {option.problemClass !== undefined && <span className="dot" aria-hidden="true" />}
          {option.label}
          <span className="filter-chip__count">{option.count}</span>
        </button>
      ))}
    </div>
  );
}

/** The grip on top of a dock that has results: raises or lowers it by a click (`dock.ts`). */
export function DockGrip({ raised, onToggle }: { raised: boolean; onToggle: () => void }) {
  const t = useT();

  return (
    <button type="button" className="dock__grip" aria-expanded={raised} aria-label={raised ? t('task.lowerDock') : t('task.raiseDock')} onClick={onToggle}>
      <span aria-hidden="true" />
    </button>
  );
}
