// Writes the prerendered public pages into dist/ (docs/SPEC.md §4.1). Run by `npm run build:static`,
// after `vite build` has written the shell and `vite build --ssr` the renderer:
//
//   CONTENT_EXPORT=<file from `content export`> SITE_ORIGIN=https://ritocode.ru npm run build:static
//
// Everything that decides what a page says is in src/prerender/prerender.tsx, where it is tested;
// this file only reads and writes.
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const exportPath = process.env.CONTENT_EXPORT;
const origin = process.env.SITE_ORIGIN;

if (!exportPath || !origin) {
  console.error('Set CONTENT_EXPORT (the file `content export` wrote) and SITE_ORIGIN (such as https://ritocode.ru).');
  process.exit(2);
}

const dist = resolve('dist');
const { prerender, resolveSiteConfig } = await import(pathToFileURL(resolve('dist-ssr/prerender.js')).href);

const files = prerender({
  content: JSON.parse(await readFile(exportPath, 'utf8')),
  template: await readFile(join(dist, 'index.html'), 'utf8'),
  origin,
  config: resolveSiteConfig(process.env),
});

for (const [path, text] of files) {
  const target = join(dist, path);
  await mkdir(dirname(target), { recursive: true });
  await writeFile(target, text, 'utf8');
  console.log(`dist/${path}`);
}
