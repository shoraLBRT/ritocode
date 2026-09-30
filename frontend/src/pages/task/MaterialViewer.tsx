import { useState } from 'react';
import type { Material } from '../../api';
import { CodeView } from '../../components/CodeView';
import { useT } from '../../i18n';

/**
 * The material, read-only (docs/SPEC.md §4.4): the overview — lines, files, dependencies — enough
 * to see a project's shape before reading it; the file tree with a line count per file; and the
 * selected file. It opens on the first Python file.
 */
export function MaterialViewer({ material }: { material: Material }) {
  const t = useT();
  const { overview } = material;
  const [selected, setSelected] = useState(
    () => (material.files.find((file) => file.path.endsWith('.py')) ?? material.files[0])?.path,
  );
  const file = material.files.find((each) => each.path === selected);

  return (
    <div className="material">
      <p className="material__overview">
        <span>{t('task.overview', { files: overview.fileCount, lines: overview.totalLines })}</span>
        <span>
          {overview.dependencies.length === 0
            ? t('task.noDependencies')
            : t('task.dependencies', { list: overview.dependencies.join(', ') })}
        </span>
      </p>

      <ul className="material__files" aria-label={t('task.files')}>
        {overview.files.map((entry) => (
          <li key={entry.path}>
            <button
              type="button"
              className="material__file"
              aria-current={entry.path === selected ? 'true' : undefined}
              onClick={() => {
                setSelected(entry.path);
              }}
            >
              <span className="material__path">{entry.path}</span>
              <span className="material__lines">{t('task.fileLines', { count: entry.lines })}</span>
            </button>
          </li>
        ))}
      </ul>

      {file !== undefined && <CodeView path={file.path} content={file.content} />}
    </div>
  );
}
