// The Lighthouse budget of the prerendered pages (#300): `npm run lighthouse`, after `npm run build:static`.
//
// Serves dist/ the way the web image does (deploy/web.Caddyfile: the path, the path as a page of the
// build, else 404.html with 404; compressed, as Caddy's `encode` does), runs Lighthouse in headless Chrome on each public page — mobile,
// Lighthouse's default — and fails when a category scores under its budget. Performance is the median
// of RUNS runs, since it moves from run to run; the other categories do not. Behind the server is the
// API as a guest finds it, answered from CONTENT_EXPORT (`guestApi`, #310); without it, 503, as in an
// outage. Measure the build the web image makes — the API on the same origin:
//
//   VITE_API_BASE_URL=/api/v1 CONTENT_EXPORT=… SITE_ORIGIN=… npm run build:static
//   CONTENT_EXPORT=… npm run lighthouse
//
// (In Git Bash, MSYS_NO_PATHCONV=1 keeps `/api/v1` from becoming a Windows path.) A build left at
// the development API address asks a port nobody answers, and the outage banner shifts every page.
//
// LIGHTHOUSE_REPORTS=<directory> keeps the first run's report of each page there, as JSON.
//
// Needs Chrome (CHROME_PATH when it is not where chrome-launcher looks).
import { createServer } from 'node:http';
import { mkdir, readdir, readFile, stat, writeFile } from 'node:fs/promises';
import { extname, join, normalize, resolve } from 'node:path';
import { gzipSync } from 'node:zlib';
import * as chromeLauncher from 'chrome-launcher';
import lighthouse from 'lighthouse';

/**
 * The lowest score each category may have, out of 100. Since #310 the pages hydrate their markup with
 * the data they were built from and load the task screen, profile and admin as chunks of their own:
 * performance 99–100 on `/`, `/tasks`, `/privacy` and a card's page when the budget was raised. Best
 * practices are left out: served over plain HTTP, the page fails them for what the test lacks, not
 * for what it does.
 */
const BUDGET = { performance: 90, accessibility: 90, seo: 90 };

/**
 * Pages held lower, by path or by a path prefix ending in `/`, each with the reason (#310):
 * - `/problems`, 72 when set: its first paint waits for the whole document, every card in full and the
 *   catalogue again as hydration data (docs/PROJECT_STATE.md › Open questions);
 * - a task's page, 80–100 from run to run: the task screen replaces its prerendered preview, and the
 *   screen's own paint is the largest.
 */
const EXCEPTIONS = { '/problems': { performance: 65 }, '/tasks/': { performance: 75 } };
const exceptionOf = (page) => EXCEPTIONS[page] ?? Object.entries(EXCEPTIONS).find(([prefix]) => prefix.endsWith('/') && page.startsWith(prefix))?.[1] ?? {};
// The fixed public pages, and the first page of a card and of a task the build wrote (#301): they
// share one layout each, so one of each stands for the rest.
const firstPage = async (directory) => `/${directory}/${(await readdir(join('dist', directory))).filter((name) => name.endsWith('.html')).sort()[0].slice(0, -'.html'.length)}`;
const PAGES = ['/', '/problems', '/tasks', '/privacy', await firstPage('problems'), await firstPage('tasks')];
const RUNS = Number(process.env.LIGHTHOUSE_RUNS ?? 3);
const reports = process.env.LIGHTHOUSE_REPORTS;

const dist = resolve('dist');
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.woff2': 'font/woff2', '.svg': 'image/svg+xml', '.xml': 'application/xml', '.txt': 'text/plain', '.ico': 'image/x-icon', '.png': 'image/png' };

// The API as it answers a guest, from the content export the pages were built from (#310): the task
// list and the catalogue, and 401 for everything that needs a learner. A page is measured as a visitor
// finds it while the site is up; without CONTENT_EXPORT every request gets 503, as in an outage, and
// the outage banner then shifts each page down.
const content = process.env.CONTENT_EXPORT ? JSON.parse(await readFile(process.env.CONTENT_EXPORT, 'utf8')) : null;

function guestApi(pathname) {
  if (content === null) {
    return { status: 503, body: {} };
  }

  if (pathname === '/api/v1/tasks') {
    const items = content.tasks.map(({ slug, title, difficulty }) => ({ slug, title, difficulty, status: null }));
    return { status: 200, body: { items, pageNumber: 1, pageSize: 100, totalItems: items.length, totalPages: 1, hasNextPage: false, hasPreviousPage: false } };
  }

  if (pathname === '/api/v1/problems') {
    return { status: 200, body: content.problems };
  }

  return { status: 401, body: { type: 'unauthenticated', title: 'Sign in first.', status: 401 } };
}

async function isFile(path) {
  try {
    return (await stat(path)).isFile();
  } catch {
    return false;
  }
}

/** The file the web image serves for `pathname`, and its status. */
async function lookup(pathname) {
  const path = pathname === '/' ? '/index.html' : pathname.replace(/^(\/tasks\/[a-z0-9-]+)\/attempts\/[^/]+$/, '$1.html');
  const file = join(dist, normalize(decodeURIComponent(path)));

  if (file.startsWith(dist)) {
    for (const candidate of [file, `${file}.html`]) {
      if (await isFile(candidate)) {
        return { file: candidate, status: 200 };
      }
    }
  }

  return { file: join(dist, '404.html'), status: 404 };
}

const server = createServer(async (request, response) => {
  const { pathname } = new URL(request.url ?? '/', 'http://localhost');

  if (pathname.startsWith('/api/')) {
    const answer = guestApi(pathname);
    response.writeHead(answer.status, { 'Content-Type': 'application/json' }).end(JSON.stringify(answer.body));
    return;
  }

  const { file, status } = await lookup(pathname);
  const body = await readFile(file);
  const headers = { 'Content-Type': types[extname(file)] ?? 'application/octet-stream' };

  if (/gzip/.test(request.headers['accept-encoding'] ?? '') && !file.endsWith('.woff2')) {
    response.writeHead(status, { ...headers, 'Content-Encoding': 'gzip' }).end(gzipSync(body));
  } else {
    response.writeHead(status, headers).end(body);
  }
});

await new Promise((done) => server.listen(0, '127.0.0.1', done));
const origin = `http://127.0.0.1:${server.address().port}`;
const chrome = await chromeLauncher.launch({ chromeFlags: ['--headless=new', '--no-sandbox', '--disable-gpu'] });
const failures = [];

try {
  for (const page of PAGES) {
    const runs = [];

    for (let run = 0; run < RUNS; run += 1) {
      const result = await lighthouse(`${origin}${page}`, { port: chrome.port, output: 'json', logLevel: 'error', onlyCategories: Object.keys(BUDGET) });
      runs.push(result.lhr);
    }

    if (reports) {
      await mkdir(reports, { recursive: true });
      await writeFile(join(reports, `${page === '/' ? 'home' : page.slice(1).replaceAll('/', '-')}.json`), JSON.stringify(runs[0]));
    }

    const scores = Object.fromEntries(Object.keys(BUDGET).map((category) => {
      const values = runs.map((lhr) => Math.round((lhr.categories[category]?.score ?? 0) * 100)).sort((a, b) => a - b);
      return [category, values[Math.floor(values.length / 2)]];
    }));

    console.log(`${page.padEnd(10)} ${Object.entries(scores).map(([category, score]) => `${category} ${score}`).join(', ')}`);

    for (const [category, score] of Object.entries(scores)) {
      const budget = { ...BUDGET, ...exceptionOf(page) }[category];

      if (score < budget) {
        const audits = Object.values(runs[0].audits)
          .filter((audit) => audit.score !== null && audit.score < 0.9 && runs[0].categories[category].auditRefs.some((ref) => ref.id === audit.id && ref.weight > 0))
          .map((audit) => audit.id);
        failures.push(`${page}: ${category} ${score} < ${budget} (${audits.join(', ')})`);
      }
    }
  }
} finally {
  chrome.kill();
  server.close();
}

if (failures.length > 0) {
  console.error(`Under the budget:\n  ${failures.join('\n  ')}`);
  process.exit(1);
}

const within = (budget) => Object.entries(budget).map(([category, score]) => `${category} ≥ ${score}`).join(', ');
console.log(`Every page within the budget: ${within(BUDGET)}${Object.entries(EXCEPTIONS).map(([page, budget]) => `; ${page}: ${within(budget)}`).join('')}.`);
