import { useMemo } from 'react';
import { highlightPython } from './python';

/**
 * One file of a material, read-only, with line numbers and — for Python — highlighting. Long lines
 * scroll inside the block rather than widening the page; materials keep to 79 columns, so on a
 * phone that is a short scroll.
 */
export function CodeView({ path, content }: { path: string; content: string }) {
  const lines = useMemo(
    () =>
      path.endsWith('.py')
        ? highlightPython(content)
        : content.split('\n').map((text) => (text.length > 0 ? [{ kind: 'plain' as const, text }] : [])),
    [path, content],
  );

  // A file ending in a newline has no line after it.
  const shown = lines.length > 1 && lines[lines.length - 1]?.length === 0 ? lines.slice(0, -1) : lines;

  return (
    <pre className="code" tabIndex={0}>
      <code>
        {shown.map((tokens, index) => (
          <span key={index} className="code__line">
            <span className="code__number" aria-hidden="true">
              {index + 1}
            </span>
            <span className="code__text">
              {tokens.map((token, position) => (
                <span key={position} className={`code__token code__token--${token.kind}`}>
                  {token.text}
                </span>
              ))}
            </span>
          </span>
        ))}
      </code>
    </pre>
  );
}
