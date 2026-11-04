import { useEffect, useId, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { useNavigate } from 'react-router';
import { getProblemCatalogue, listTasks, useApiClient } from '../api';
import type { ProblemCard, TaskSummary } from '../api';
import { useT } from '../i18n';
import { headerResults, NO_RESULTS } from './headerResults';
import type { HeaderResult } from './headerResults';

type Lists =
  | { readonly status: 'idle' | 'loading' | 'failed' }
  | { readonly status: 'loaded'; readonly tasks: readonly TaskSummary[]; readonly cards: readonly ProblemCard[] };

/**
 * The header's search over tasks and problems (docs/DESIGN.md › The arena): a combobox whose list
 * shows the tasks and the problems that share a word with what is typed. The two lists are read on
 * the first focus, not with every page, and kept for the visit; a failed read is tried again on the
 * next focus. Arrow keys move through the list, Enter opens the highlighted entry — the first one
 * when none is — Escape closes it, and Ctrl K (⌘K) reaches the field from anywhere.
 */
export function HeaderSearch() {
  const client = useApiClient();
  const t = useT();
  const navigate = useNavigate();
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [lists, setLists] = useState<Lists>({ status: 'idle' });
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);

  useEffect(() => {
    const onKey = (event: globalThis.KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        input.current?.focus();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('keydown', onKey);
    };
  }, []);

  const load = () => {
    if (lists.status === 'loading' || lists.status === 'loaded') {
      return;
    }
    setLists({ status: 'loading' });
    Promise.all([listTasks(client, { pageSize: 100 }), getProblemCatalogue(client)]).then(
      ([tasks, catalogue]) => {
        setLists({ status: 'loaded', tasks: tasks.items, cards: catalogue.cards });
      },
      () => {
        setLists({ status: 'failed' });
      },
    );
  };

  const results = lists.status === 'loaded' ? headerResults(lists.tasks, lists.cards, query) : NO_RESULTS;
  const options = [...results.tasks, ...results.problems];
  const expanded = open && query.trim() !== '';
  const optionId = (index: number) => `${id}-option-${String(index)}`;
  const note =
    lists.status === 'failed'
      ? t('header.search.failed')
      : lists.status !== 'loaded'
        ? t('header.search.loading')
        : options.length === 0
          ? t('header.search.empty')
          : null;

  const choose = (result: HeaderResult) => {
    void navigate(result.to);
    setQuery('');
    setOpen(false);
    setActive(-1);
    input.current?.blur();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowUp': {
        if (options.length === 0) {
          return;
        }
        event.preventDefault();
        setOpen(true);
        const last = options.length - 1;
        // From nothing highlighted, down goes to the first entry and up to the last; both wrap.
        setActive((current) =>
          event.key === 'ArrowDown' ? (current >= last ? 0 : current + 1) : current <= 0 ? last : current - 1,
        );
        return;
      }
      case 'Enter': {
        const chosen = options[active] ?? options[0];
        if (expanded && chosen !== undefined) {
          event.preventDefault();
          choose(chosen);
        }
        return;
      }
      case 'Escape':
        if (expanded) {
          event.preventDefault();
          setOpen(false);
          setActive(-1);
        }
        return;
    }
  };

  const group = (kind: 'tasks' | 'problems', items: readonly HeaderResult[], offset: number) =>
    items.length > 0 && (
      <li role="presentation">
        <div className="app__search-group" id={`${id}-${kind}`} aria-hidden="true">
          {t(`header.search.${kind}`)}
        </div>
        <ul role="group" aria-labelledby={`${id}-${kind}`}>
          {items.map((item, index) => (
            <li
              key={item.slug}
              id={optionId(offset + index)}
              role="option"
              aria-selected={active === offset + index}
              className="app__search-option"
              // Keeps the focus in the field, so choosing does not close the list first.
              onMouseDown={(event) => {
                event.preventDefault();
              }}
              onClick={() => {
                choose(item);
              }}
            >
              {item.title}
            </li>
          ))}
        </ul>
      </li>
    );

  return (
    <div className="app__search">
      <label className="rc-search">
        <svg viewBox="0 0 24 24" aria-hidden="true" className="icon">
          <circle cx="11" cy="11" r="7" />
          <path d="m20 20-3.5-3.5" />
        </svg>
        <span className="visually-hidden">{t('header.search.label')}</span>
        <input
          ref={input}
          type="search"
          role="combobox"
          aria-expanded={expanded}
          aria-controls={`${id}-list`}
          aria-autocomplete="list"
          {...(expanded && active >= 0 ? { 'aria-activedescendant': optionId(active) } : {})}
          placeholder={t('header.search.placeholder')}
          value={query}
          onFocus={() => {
            load();
            setOpen(true);
          }}
          onBlur={() => {
            setOpen(false);
            setActive(-1);
          }}
          onChange={(event) => {
            setQuery(event.target.value);
            setOpen(true);
            setActive(-1);
          }}
          onKeyDown={onKeyDown}
        />
        <kbd className="rc-kbd" aria-hidden="true">
          {t('header.search.shortcut')}
        </kbd>
      </label>

      <div className="app__search-popup" hidden={!expanded}>
        <ul id={`${id}-list`} role="listbox" aria-label={t('header.search.results')}>
          {group('tasks', results.tasks, 0)}
          {group('problems', results.problems, results.tasks.length)}
        </ul>
        {expanded && note !== null && (
          <p className="app__search-note" role="status">
            {note}
          </p>
        )}
      </div>
    </div>
  );
}
