import type { ReactNode } from 'react';

/**
 * Renders the Markdown a problem card's sections are written in (docs/CONTENT_FORMAT.md §4) as
 * React elements: paragraphs, bulleted and numbered lists, `code`, **bold**, *emphasis* and
 * [links](https://…). That subset is what cards are written in. Headings `##` and `###` render as
 * `h2` and `h3`, for the privacy policy (#128); a card's sections are already split at `##`, so its
 * text never holds one.
 *
 * Its own rather than a library's: the subset is small, it never goes through `innerHTML` — so no
 * sanitiser is needed and nothing in a card can become markup — and it renders on a server as it
 * does in the browser, which the prerender of `/problems` (#132) needs. Anything outside the
 * subset reads as the text it is.
 */
export function Markdown({ source }: { source: string }) {
  return <>{blocks(source).map((block, index) => renderBlock(block, index))}</>;
}

type Block =
  | { readonly kind: 'heading'; readonly level: 2 | 3; readonly text: string }
  | { readonly kind: 'paragraph'; readonly text: string }
  | { readonly kind: 'list'; readonly ordered: boolean; readonly items: readonly string[] };

const bullet = /^\s*[-*]\s+/;
const numbered = /^\s*\d+[.)]\s+/;
const heading = /^(##|###)\s+(.*\S)\s*$/;

function blocks(source: string): Block[] {
  const result: Block[] = [];
  let paragraph: string[] = [];
  let list: { ordered: boolean; items: string[] } | null = null;

  const flush = () => {
    if (paragraph.length > 0) {
      result.push({ kind: 'paragraph', text: paragraph.join(' ') });
      paragraph = [];
    }
    if (list !== null) {
      result.push({ kind: 'list', ordered: list.ordered, items: list.items });
      list = null;
    }
  };

  for (const line of source.replace(/\r\n/g, '\n').split('\n')) {
    const marker = bullet.exec(line) ?? numbered.exec(line);
    const title = heading.exec(line);

    if (line.trim() === '') {
      flush();
    } else if (title !== null) {
      flush();
      result.push({ kind: 'heading', level: title[1] === '##' ? 2 : 3, text: title[2] ?? '' });
    } else if (marker !== null) {
      const ordered = numbered.test(line);

      if (paragraph.length > 0 || (list !== null && list.ordered !== ordered)) {
        flush();
      }

      list ??= { ordered, items: [] };
      list.items.push(line.slice(marker[0].length).trim());
    } else if (list !== null && /^\s+/.test(line)) {
      // An indented line continues the list item above it; a list always holds one by now.
      const last = list.items.length - 1;
      list.items[last] = `${list.items[last] ?? ''} ${line.trim()}`;
    } else {
      if (list !== null) {
        flush();
      }
      paragraph.push(line.trim());
    }
  }

  flush();
  return result;
}

function renderBlock(block: Block, key: number): ReactNode {
  if (block.kind === 'heading') {
    return block.level === 2 ? <h2 key={key}>{inline(block.text)}</h2> : <h3 key={key}>{inline(block.text)}</h3>;
  }

  if (block.kind === 'paragraph') {
    return <p key={key}>{inline(block.text)}</p>;
  }

  const items = block.items.map((item, index) => <li key={index}>{inline(item)}</li>);

  return block.ordered ? <ol key={key}>{items}</ol> : <ul key={key}>{items}</ul>;
}

// `code` first, so nothing inside it is read as emphasis; then links, bold and emphasis.
const inlinePattern = /`([^`]+)`|\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)|\*\*([^*]+)\*\*|\*([^*\s][^*]*)\*|_([^_\s][^_]*)_/g;

function inline(text: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  let last = 0;

  for (const match of text.matchAll(inlinePattern)) {
    const [whole, code, label, href, bold, em, underscored] = match;

    if (match.index > last) {
      nodes.push(text.slice(last, match.index));
    }

    const key = nodes.length;

    if (code !== undefined) {
      nodes.push(<code key={key}>{code}</code>);
    } else if (label !== undefined && href !== undefined) {
      nodes.push(
        <a key={key} href={href} rel="noreferrer noopener" target="_blank">
          {label}
        </a>,
      );
    } else if (bold !== undefined) {
      nodes.push(<strong key={key}>{bold}</strong>);
    } else {
      nodes.push(<em key={key}>{em ?? underscored}</em>);
    }

    last = match.index + whole.length;
  }

  if (last < text.length) {
    nodes.push(text.slice(last));
  }

  return nodes;
}
