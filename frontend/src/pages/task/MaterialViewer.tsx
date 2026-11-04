import { useState } from 'react';
import type { Material } from '../../api';
import { CodeView } from '../../components/CodeView';
import { useT } from '../../i18n';
import { Icon } from './parts';

interface Row {
  readonly key: string;
  readonly name: string;
  readonly depth: number;
  readonly path: string | null;
  readonly lines: number;
}

/**
 * The material, read-only (docs/SPEC.md §4.4, docs/DESIGN.md). Above the code, the open file's path
 * as a button that opens the project's tree — folders, files, a line count each, and the project's
 * size — and buttons to the previous and the next file, named by those files. The code can be made
 * larger. It opens on the first Python file.
 */
export function MaterialViewer({ material }: { material: Material }) {
  const t = useT();
  const files = material.overview.files;
  const [selected, setSelected] = useState(() => (files.find((file) => file.path.endsWith('.py')) ?? files[0])?.path);
  const [open, setOpen] = useState(false);
  const [large, setLarge] = useState(false);

  const index = Math.max(0, files.findIndex((file) => file.path === selected));
  const current = files[index];
  const file = material.files.find((each) => each.path === current?.path);
  const previous = files[(index - 1 + files.length) % files.length];
  const next = files[(index + 1) % files.length];
  const name = (path: string) => path.split('/').at(-1) ?? path;
  const crumbs = current === undefined ? [] : current.path.split('/');

  return (
    <div className="material">
      <div className="material__bar">
        <button
          type="button"
          className="file-picker"
          aria-haspopup="true"
          aria-expanded={open}
          aria-label={t('task.chooseFile', { path: current?.path ?? '' })}
          onClick={() => {
            setOpen(!open);
          }}
        >
          <span className="file-picker__path">
            <Icon name="file" />
            {crumbs.map((crumb, position) => (
              <span key={position} className={position === crumbs.length - 1 ? 'file-picker__name' : 'file-picker__dir'}>
                {crumb}
                {position < crumbs.length - 1 && <span className="file-picker__slash">{'/'}</span>}
              </span>
            ))}
          </span>
          <span className="file-picker__open" aria-hidden="true">
            <Icon name="down" />
          </span>
        </button>
        {current !== undefined && <span className="material__lines">{t('task.fileLines', { count: current.lines })}</span>}

        {files.length > 1 && previous !== undefined && next !== undefined && (
          <div className="material__nav">
            <button
              type="button"
              className="nav-button"
              aria-label={t('task.previousFile', { path: previous.path })}
              onClick={() => {
                setSelected(previous.path);
              }}
            >
              <Icon name="left" />
              <span>{name(previous.path)}</span>
            </button>
            <span className="material__position">{t('task.filePosition', { index: index + 1, count: files.length })}</span>
            <button
              type="button"
              className="nav-button"
              aria-label={t('task.nextFile', { path: next.path })}
              onClick={() => {
                setSelected(next.path);
              }}
            >
              <span>{name(next.path)}</span>
              <Icon name="right" />
            </button>
          </div>
        )}
        <button
          type="button"
          className="icon-button icon-button--text"
          aria-label={large ? t('task.smallerCode') : t('task.largerCode')}
          aria-pressed={large}
          onClick={() => {
            setLarge(!large);
          }}
        >
          {large ? t('task.smallerCodeShort') : t('task.largerCodeShort')}
        </button>

        {open && (
          <div className="file-tree">
            <div className="file-tree__head">
              <span className="eyebrow">{t('task.files')}</span>
              <span>{t('task.overview', { files: material.overview.fileCount, lines: material.overview.totalLines })}</span>
            </div>
            <ul className="file-tree__rows">
              {treeRows(files).map((row) =>
                row.path === null ? (
                  <li key={row.key} className={`file-tree__row file-tree__row--dir file-tree__row--depth-${String(row.depth)}`}>
                    <Icon name="folder" />
                    {`${row.name}/`}
                  </li>
                ) : (
                  <li key={row.key}>
                    <button
                      type="button"
                      className={`file-tree__row file-tree__row--depth-${String(row.depth)}`}
                      aria-current={row.path === current?.path ? 'true' : undefined}
                      onClick={() => {
                        setSelected(row.path ?? undefined);
                        setOpen(false);
                      }}
                    >
                      <Icon name="file" />
                      {row.name}
                      <span className="file-tree__lines">{row.lines}</span>
                    </button>
                  </li>
                ),
              )}
            </ul>
          </div>
        )}
      </div>

      {open && (
        <button
          type="button"
          className="material__scrim"
          aria-label={t('task.closeFiles')}
          onClick={() => {
            setOpen(false);
          }}
        />
      )}

      {file !== undefined && (
        <div className={large ? 'material__code material__code--large' : 'material__code'}>
          <CodeView path={file.path} content={file.content} />
        </div>
      )}
    </div>
  );
}

/** The project's files as a tree, folders first in the order the files name them. */
function treeRows(files: readonly { readonly path: string; readonly lines: number }[]): Row[] {
  const rows: Row[] = [];
  const seen = new Set<string>();

  for (const file of files) {
    const parts = file.path.split('/');
    for (let depth = 1; depth < parts.length; depth++) {
      const dir = parts.slice(0, depth).join('/');
      if (!seen.has(dir)) {
        seen.add(dir);
        rows.push({ key: `${dir}/`, name: parts[depth - 1] ?? dir, depth: depth - 1, path: null, lines: 0 });
      }
    }
    rows.push({ key: file.path, name: parts.at(-1) ?? file.path, depth: parts.length - 1, path: file.path, lines: file.lines });
  }

  return rows;
}
