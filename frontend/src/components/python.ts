/**
 * Splits Python source into highlighted tokens, line by line, for the read-only material viewer.
 *
 * A highlighter only has to colour what a reader scans for — keywords, strings, comments, numbers,
 * decorators, the names a `def` or `class` introduces — so this is a scanner of a few rules rather
 * than a parser or a library. A string that spans lines (a triple-quoted one) is cut at each line
 * break and keeps its kind on every line. Anything unrecognised is plain text, never lost.
 */

export type TokenKind = 'keyword' | 'builtin' | 'string' | 'comment' | 'number' | 'decorator' | 'definition' | 'plain';

export interface Token {
  readonly kind: TokenKind;
  readonly text: string;
}

const keywords = new Set([
  'False', 'None', 'True', 'and', 'as', 'assert', 'async', 'await', 'break', 'class', 'continue',
  'def', 'del', 'elif', 'else', 'except', 'finally', 'for', 'from', 'global', 'if', 'import', 'in',
  'is', 'lambda', 'match', 'case', 'nonlocal', 'not', 'or', 'pass', 'raise', 'return', 'try', 'while',
  'with', 'yield',
]);

const builtins = new Set([
  'abs', 'all', 'any', 'bool', 'bytes', 'dict', 'enumerate', 'Exception', 'filter', 'float', 'int',
  'isinstance', 'len', 'list', 'map', 'max', 'min', 'open', 'print', 'range', 'round', 'self', 'set',
  'sorted', 'str', 'sum', 'super', 'tuple', 'type', 'ValueError', 'zip',
]);

// Tried in this order at each position; the first that matches wins.
const rules: readonly (readonly [TokenKind | 'name', RegExp])[] = [
  ['comment', /#[^\n]*/y],
  ['string', /[rRbBuUfF]{0,2}(?:'''[\s\S]*?(?:'''|$)|"""[\s\S]*?(?:"""|$)|'(?:\\.|[^'\\\n])*'?|"(?:\\.|[^"\\\n])*"?)/y],
  ['number', /(?:0[xXoObB][\da-fA-F_]+|\d[\d_]*(?:\.\d[\d_]*)?(?:[eE][+-]?\d+)?[jJ]?)(?![\w])/y],
  ['decorator', /@[A-Za-z_][\w.]*/y],
  ['name', /[A-Za-z_]\w*/y],
];

/** The source as lines of tokens; joining every line's texts with `\n` gives the source back. */
export function highlightPython(source: string): Token[][] {
  const lines: Token[][] = [[]];
  let previousName = '';
  let position = 0;

  const push = (kind: TokenKind, text: string) => {
    text.split('\n').forEach((part, index) => {
      if (index > 0) {
        lines.push([]);
      }
      if (part.length > 0) {
        lines[lines.length - 1]?.push({ kind, text: part });
      }
    });
  };

  while (position < source.length) {
    let matched = false;

    for (const [kind, pattern] of rules) {
      pattern.lastIndex = position;
      const match = pattern.exec(source);

      if (match === null || match[0].length === 0) {
        continue;
      }

      const text = match[0];

      if (kind === 'name') {
        push(nameKind(text, previousName), text);
        previousName = text;
      } else {
        push(kind, text);
        previousName = '';
      }

      position += text.length;
      matched = true;
      break;
    }

    if (!matched) {
      const character = source.charAt(position);
      push('plain', character);

      if (!/\s/.test(character)) {
        previousName = '';
      }

      position += 1;
    }
  }

  return lines;
}

function nameKind(name: string, previousName: string): TokenKind {
  if (keywords.has(name)) {
    return 'keyword';
  }
  if (previousName === 'def' || previousName === 'class') {
    return 'definition';
  }
  return builtins.has(name) ? 'builtin' : 'plain';
}
