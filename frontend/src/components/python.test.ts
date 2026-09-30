import { describe, expect, it } from 'vitest';
import { highlightPython } from './python';
import type { Token } from './python';

function kinds(line: readonly Token[] | undefined): string[] {
  return (line ?? []).filter((token) => token.text.trim() !== '').map((token) => `${token.kind}:${token.text}`);
}

describe('highlightPython', () => {
  it('gives the source back, line for line', () => {
    const source = 'import sys\n\n\ndef main():\n    print("hi")  # say it\n';
    const lines = highlightPython(source);

    expect(lines.map((line) => line.map((token) => token.text).join('')).join('\n')).toBe(source);
  });

  it('marks keywords, builtins, definitions, numbers and comments', () => {
    const [line] = highlightPython('def total(items): return round(sum(items), 2)  # money');

    expect(kinds(line)).toEqual([
      'keyword:def',
      'definition:total',
      'plain:(',
      'plain:items',
      'plain:)',
      'plain::',
      'keyword:return',
      'builtin:round',
      'plain:(',
      'builtin:sum',
      'plain:(',
      'plain:items',
      'plain:)',
      'plain:,',
      'number:2',
      'plain:)',
      'comment:# money',
    ]);
  });

  it('reads prefixed strings, and a # inside a string is not a comment', () => {
    const [line] = highlightPython('url = f"postgresql://shop:{pw}@db#1"');

    expect(kinds(line)).toEqual(['plain:url', 'plain:=', 'string:f"postgresql://shop:{pw}@db#1"']);
  });

  it('keeps a triple-quoted string a string on every line it spans', () => {
    const lines = highlightPython('"""First line\nsecond line"""\nx = 1');

    expect(kinds(lines[0])).toEqual(['string:"""First line']);
    expect(kinds(lines[1])).toEqual(['string:second line"""']);
    expect(kinds(lines[2])).toEqual(['plain:x', 'plain:=', 'number:1']);
  });

  it('marks decorators', () => {
    expect(kinds(highlightPython('@app.route("/")')[0])).toEqual(['decorator:@app.route', 'plain:(', 'string:"/"', 'plain:)']);
  });
});
