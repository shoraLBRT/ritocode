// The surface levels and the text on them (docs/DESIGN.md › Tokens): every text colour is
// readable on every surface at WCAG AA (4.5:1), and the surfaces step apart, in both themes. The
// tokens are read from the stylesheet itself, so a changed value is checked as it ships. It is
// read from disk: the test config turns CSS off, which empties a `?raw` import.
/// <reference types="node" />
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const styles = readFileSync(resolve(import.meta.dirname, '../styles.css'), 'utf8');

type Tokens = Record<string, string>;

function block(selector: string): Tokens {
  const start = styles.indexOf(`${selector} {`);
  expect(start, `${selector} is in styles.css`).toBeGreaterThanOrEqual(0);
  const body = styles.slice(start, styles.indexOf('\n}', start));
  const tokens: Tokens = {};
  for (const match of body.matchAll(/--([\w-]+):\s*(#[0-9a-fA-F]{6})\s*;/g)) {
    tokens[match[1] ?? ''] = match[2] ?? '';
  }
  return tokens;
}

const light = block(':root');
const themes: [string, Tokens][] = [
  ['light', light],
  ['dark', { ...light, ...block(":root[data-theme='dark']") }],
];

function channel(hex: string, at: number): number {
  const c = parseInt(hex.slice(at, at + 2), 16) / 255;
  return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
}

function luminance(hex: string): number {
  return 0.2126 * channel(hex, 1) + 0.7152 * channel(hex, 3) + 0.0722 * channel(hex, 5);
}

function contrast(a: string, b: string): number {
  const [x, y] = [luminance(a), luminance(b)];
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
}

function token(tokens: Tokens, name: string): string {
  const value = tokens[name];
  expect(value, `--${name} is defined`).toBeDefined();
  return value ?? '';
}

const surfaces = ['page', 'surface', 'surface-inset', 'surface-pop', 'surface-sunken'];
const texts = ['text', 'text-muted', 'text-faint', 'accent', 'good', 'warn', 'danger'];
const pairs: [string, string][] = [
  ...surfaces.flatMap((surface) => texts.map((text): [string, string] => [text, surface])),
  ['accent-ink', 'accent'],
  ['good', 'good-surface'],
  ['warn', 'warn-surface'],
  ['danger', 'danger-surface'],
  ['spotlight-ink', 'spotlight'],
  ['spotlight-muted', 'spotlight'],
  ...['text', 'text-faint', 'code-keyword', 'code-builtin', 'code-definition', 'code-string', 'code-number'].map(
    (text): [string, string] => [text, 'code-surface'],
  ),
];

describe.each(themes)('the %s theme', (_, tokens) => {
  it.each(pairs)('--%s on --%s meets WCAG AA', (text, surface) => {
    expect(contrast(token(tokens, text), token(tokens, surface))).toBeGreaterThanOrEqual(4.5);
  });

  it('steps each surface apart from the one it sits on', () => {
    const apart = (a: string, b: string) => contrast(token(tokens, a), token(tokens, b));
    // A card on the page, a field or the code inside a card, a popup over a card.
    expect(apart('surface', 'page')).toBeGreaterThanOrEqual(1.15);
    expect(apart('surface-inset', 'surface')).toBeGreaterThanOrEqual(1.08);
    expect(apart('surface-sunken', 'surface')).toBeGreaterThanOrEqual(1.15);
  });
});
