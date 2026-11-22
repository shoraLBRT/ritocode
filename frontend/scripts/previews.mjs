// Rasterises the pages' preview images (#302) from the SVG src/prerender/preview.ts draws, for
// scripts/prerender.mjs. resvg (@resvg/resvg-js, a dev dependency with prebuilt binaries) does the
// drawing, with IBM Plex Sans from the @fontsource package the site already ships and no system
// font, so the images come out the same on every machine.
//
// resvg reads TrueType, not the WOFF @fontsource carries, so each font is unpacked first (WOFF 1.0
// is the TrueType tables, each zlib-compressed) into dist-ssr/, which the build already treats as
// scratch — and renamed to the family of its own the SVG names it by (PREVIEW_FONTS), since resvg
// falls back for a missing glyph to any weight.
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { inflateSync } from 'node:zlib';
import { Resvg } from '@resvg/resvg-js';

const NAME = 0x6e616d65; // 'name'

/** WOFF 1.0 back to the TrueType font it packs (https://www.w3.org/TR/WOFF/), its name table replaced by one naming `family`. */
export function woffToTtf(woff, family) {
  if (woff.readUInt32BE(0) !== 0x774f4646) {
    throw new Error('Not a WOFF 1.0 font.');
  }

  const count = woff.readUInt16BE(12);
  const tables = [];

  for (let index = 0; index < count; index++) {
    const entry = 44 + index * 20;
    const [tag, offset, compressed, length, checksum] = [0, 4, 8, 12, 16].map((at) => woff.readUInt32BE(entry + at));
    const stored = woff.subarray(offset, offset + compressed);
    tables.push(tag === NAME ? { tag, checksum: 0, data: nameTable(family) } : { tag, checksum, data: compressed < length ? inflateSync(stored) : stored });
  }

  const power = 2 ** Math.floor(Math.log2(count));
  const header = Buffer.alloc(12 + count * 16);
  header.writeUInt32BE(woff.readUInt32BE(4), 0);
  header.writeUInt16BE(count, 4);
  header.writeUInt16BE(power * 16, 6);
  header.writeUInt16BE(Math.log2(power), 8);
  header.writeUInt16BE(count * 16 - power * 16, 10);

  const parts = [header];
  let offset = header.length;

  tables.forEach(({ tag, checksum, data }, index) => {
    const record = 12 + index * 16;
    header.writeUInt32BE(tag, record);
    header.writeUInt32BE(checksum, record + 4);
    header.writeUInt32BE(offset, record + 8);
    header.writeUInt32BE(data.length, record + 12);
    const padded = Buffer.alloc(Math.ceil(data.length / 4) * 4);
    data.copy(padded);
    parts.push(padded);
    offset += padded.length;
  });

  return Buffer.concat(parts);
}

/** A name table (format 0, Windows Unicode) giving the family, the subfamily, the full and the PostScript name. */
function nameTable(family) {
  const names = [
    [1, family],
    [2, 'Regular'],
    [4, family],
    [6, family.replaceAll(' ', '-')],
  ].map(([id, text]) => [id, Buffer.from(text, 'utf16le').swap16()]);

  const header = Buffer.alloc(6 + names.length * 12);
  header.writeUInt16BE(0, 0);
  header.writeUInt16BE(names.length, 2);
  header.writeUInt16BE(header.length, 4);

  let offset = 0;

  names.forEach(([id, text], index) => {
    const record = 6 + index * 12;
    [3, 1, 0x409, id, text.length, offset].forEach((value, at) => header.writeUInt16BE(value, record + at * 2));
    offset += text.length;
  });

  return Buffer.concat([header, ...names.map(([, text]) => text)]);
}

/** Writes each SVG of `svgs` (by its PNG's path) as PNG under `outDir`, with `fonts` (PREVIEW_FONTS); returns the bytes written. */
export async function rasterise(svgs, outDir, fonts) {
  const fontDir = resolve('dist-ssr/fonts');
  await mkdir(fontDir, { recursive: true });

  const fontFiles = await Promise.all(
    fonts.map(async ({ file, family }) => {
      const target = join(fontDir, `${file}.ttf`);
      await writeFile(target, woffToTtf(await readFile(resolve(`node_modules/@fontsource/ibm-plex-sans/files/${file}.woff`)), family));
      return target;
    }),
  );

  let bytes = 0;

  for (const [path, svg] of svgs) {
    const png = new Resvg(svg, { font: { fontFiles, loadSystemFonts: false } }).render().asPng();
    const target = join(outDir, path);
    await mkdir(dirname(target), { recursive: true });
    await writeFile(target, png);
    bytes += png.length;
  }

  return bytes;
}
