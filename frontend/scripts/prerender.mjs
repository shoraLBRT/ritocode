// Writes the prerendered public pages into dist/ (docs/SPEC.md §4.1). Run by `npm run build:static`,
// after `vite build` has written the shell and `vite build --ssr` the renderer:
//
//   CONTENT_EXPORT=<file from `content export`> SITE_ORIGIN=https://ritocode.ru npm run build:static
//
// CONTENT_LASTMOD (YYYY-MM-DD), when the content last changed, gives the sitemap its `lastmod`;
// a release sets it from git (`git log -1 --format=%cs -- content`), and it is left out when unset.
// SOURCE_COMMIT, the commit built, goes into `version.json` for the admin system page (#297); null when unset.
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
const { checkPages, prerender, resolveSiteConfig } = await import(pathToFileURL(resolve('dist-ssr/prerender.js')).href);
const content = JSON.parse(await readFile(exportPath, 'utf8'));

const files = prerender({
  content,
  template: await readFile(join(dist, 'index.html'), 'utf8'),
  origin,
  config: resolveSiteConfig(process.env),
  lastmod: process.env.CONTENT_LASTMOD || undefined,
  commit: process.env.SOURCE_COMMIT || undefined,
});

for (const [path, text] of files) {
  const target = join(dist, path);
  await mkdir(dirname(target), { recursive: true });
  await writeFile(target, text, 'utf8');
}

// Every card and every task has its page, with its text, in the sitemap: the CI job *Prerender public
// pages* runs this build, so a card or task without a page fails it (#301).
const problems = checkPages(content, files);

if (problems.length > 0) {
  console.error(`The prerendered pages do not cover the content:\n${problems.join('\n')}`);
  process.exit(1);
}

const count = (prefix) => [...files.keys()].filter((path) => path.startsWith(prefix)).length;
console.log(`dist/: ${files.size} files; a page for each of ${count('problems/')} cards and ${count('tasks/')} tasks.`);
