/**
 * The preview image a link to a public page shows in a messenger or a social network (#302), as SVG:
 * the build (`scripts/prerender.mjs`) rasterises it to PNG, since Telegram, VK and the rest take no
 * SVG. It is drawn from the page's own text — nobody draws an image for a card or a task — in the
 * site's look (docs/DESIGN.md): the spotlight indigo, IBM Plex Sans, a card's class in its colour.
 */
export interface Preview {
  /** The small line above the title: a card's class, a task's difficulty, the page's kind. */
  readonly kicker: string;
  readonly title: string;
  /** One or two lines under the title, cut to fit. */
  readonly text?: string;
  /** The colour of the kicker and the bar on the left; the site's accent when absent. */
  readonly accent?: string;
}

export const PREVIEW_WIDTH = 1200;
export const PREVIEW_HEIGHT = 630;

type Weight = 400 | 600 | 700;
type Subset = 'latin' | 'cyrillic';

/**
 * The fonts the rasteriser is given (`scripts/previews.mjs`), and no system font: IBM Plex Sans from
 * @fontsource, which comes split by script into one file per subset and weight. The rasteriser
 * falls back for a missing glyph to the first font that has it, whatever its weight, so each file is
 * loaded under a family of its own and every run of text names the one it is drawn with.
 */
export const PREVIEW_FONTS = (['latin', 'cyrillic'] as const).flatMap((subset) =>
  ([400, 600, 700] as const).map((weight) => ({ file: `ibm-plex-sans-${subset}-${String(weight)}-normal`, family: family(subset, weight) })),
);

function family(subset: Subset, weight: Weight): string {
  return `Preview ${subset} ${String(weight)}`;
}

/** Cyrillic letters, the combining stress mark and №: what @fontsource's cyrillic subset holds and its latin one lacks. */
const CYRILLIC = /[Ѐ-ӿ№]|́/;

/** Text as runs of one script each, every run in the font that holds it. */
function runs(text: string, weight: Weight): string {
  const parts: { subset: Subset; text: string }[] = [];

  for (const char of text) {
    const subset: Subset = CYRILLIC.test(char) ? 'cyrillic' : 'latin';
    const last = parts.at(-1);

    if (last?.subset === subset) {
      last.text += char;
    } else {
      parts.push({ subset, text: char });
    }
  }

  return parts.map((part) => `<tspan font-family="${family(part.subset, weight)}">${escape(part.text)}</tspan>`).join('');
}

/** The dark theme's accent and class colours (docs/DESIGN.md), which read on the spotlight indigo. */
const ACCENT = '#9ea3f5';
const CLASS_COLOURS: Readonly<Record<string, string>> = {
  disproportion: '#deaa5c',
  'project-knowledge': '#8fadea',
  hygiene: '#ec8f7e',
  growth: '#b9a0e6',
  'false-confidence': '#e693bd',
  domain: '#77c9a0',
};

/** The colour of a card's class on its preview; the site's accent for a class it does not know. */
export function classColour(id: string): string {
  return CLASS_COLOURS[id] ?? ACCENT;
}

const BACKGROUND = '#25275e';
const INK = '#ffffff';
const MUTED = '#c9cbf5';

const LEFT = 96;
const MEASURE = PREVIEW_WIDTH - 2 * LEFT;
/** Where the title and its text must end, above the footer. */
const BOTTOM = 520;

/** The preview at `host`, such as `ritocode.ru`, which its footer names. */
export function previewSvg(preview: Preview, host: string): string {
  const accent = preview.accent ?? ACCENT;

  // The longest titles take a smaller size rather than lose words.
  let size = 68;
  let title = wrap(preview.title, size, 600, 3);

  if (title.cut) {
    size = 56;
    title = wrap(preview.title, size, 600, 3);
  }

  const lineHeight = Math.round(size * 1.18);
  const titleTop = 300;
  const titleEnd = titleTop + (title.lines.length - 1) * lineHeight;

  const textSize = 30;
  const textHeight = 42;
  const textTop = titleEnd + 30 + textHeight;
  const textRoom = Math.max(0, Math.min(2, Math.floor((BOTTOM - textTop) / textHeight) + 1));
  const text = preview.text === undefined || textRoom === 0 ? [] : wrap(preview.text, textSize, 400, textRoom).lines;

  const lines = (items: readonly string[], top: number, step: number, weight: Weight) =>
    items.map((line, index) => `<tspan x="${String(LEFT)}" y="${String(top + index * step)}">${runs(line, weight)}</tspan>`).join('');

  const width = String(PREVIEW_WIDTH);
  const height = String(PREVIEW_HEIGHT);
  const left = String(LEFT);

  return [
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">`,
    `<rect width="${width}" height="${height}" fill="${BACKGROUND}"/>`,
    `<circle cx="${String(PREVIEW_WIDTH - 40)}" cy="40" r="260" fill="${accent}" fill-opacity="0.08"/>`,
    `<rect width="16" height="${height}" fill="${accent}"/>`,
    `<text x="${left}" y="128" font-size="40" fill="${INK}">${runs('Ritocode', 700)}</text>`,
    `<text x="${left}" y="212" font-size="30" fill="${accent}">${runs(wrap(preview.kicker, 30, 600, 1).lines[0] ?? '', 600)}</text>`,
    `<text font-size="${String(size)}" fill="${INK}">${lines(title.lines, titleTop, lineHeight, 600)}</text>`,
    text.length === 0 ? '' : `<text font-size="${String(textSize)}" fill="${MUTED}">${lines(text, textTop, textHeight, 400)}</text>`,
    `<text x="${left}" y="582" font-size="28" fill="${MUTED}">${runs(host, 400)}</text>`,
    '</svg>',
  ]
    .filter((part) => part !== '')
    .join('\n');
}

/**
 * Text broken into at most `max` lines of the measure at `size`, by words, with an ellipsis where it
 * was cut. SVG does not wrap, so the width is estimated from IBM Plex Sans's advances — generously, so
 * that a line falls short of the edge rather than past it.
 */
export function wrap(text: string, size: number, weight: number, max: number): { lines: string[]; cut: boolean } {
  const room = MEASURE / size / (weight >= 600 ? 1.04 : 1);
  const words = text.replace(/\s+/g, ' ').trim().split(' ').filter((word) => word !== '');
  const lines: string[] = [];
  let line = '';

  for (const word of words) {
    const next = line === '' ? word : `${line} ${word}`;

    if (width(next) <= room) {
      line = next;
      continue;
    }

    if (line !== '') {
      lines.push(line);
    }

    line = word;

    if (lines.length === max) {
      break;
    }
  }

  if (lines.length < max && line !== '') {
    lines.push(line);
    line = '';
  }

  const cut = lines.length === max && (line !== '' || lines.join(' ').length < words.join(' ').length);

  if (cut) {
    let last = lines[max - 1] ?? '';

    while (last.includes(' ') && width(`${last}…`) > room) {
      last = last.slice(0, last.lastIndexOf(' '));
    }

    while (width(`${last}…`) > room && last.length > 1) {
      last = last.slice(0, -1);
    }

    lines[max - 1] = `${last.replace(/[\s,.;:—-]+$/, '')}…`;
  }

  return { lines, cut };
}

/** The advance of a character in ems of IBM Plex Sans, rounded up. */
function advance(char: string): number {
  if (char === ' ') return 0.27;
  if ('iljtfrI.,:;!|\'"()[]«»ійїґ'.includes(char)) return 0.34;
  if ('mwMWШЩЖМЮшщжмюЫы—'.includes(char)) return 0.86;
  if (char >= '0' && char <= '9') return 0.6;
  if (char !== char.toLowerCase()) return 0.7;
  return 0.6;
}

function width(text: string): number {
  return Array.from(text).reduce((sum, char) => sum + advance(char), 0);
}

function escape(text: string): string {
  return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
}
