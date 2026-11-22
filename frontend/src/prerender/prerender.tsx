import { renderToString } from 'react-dom/server';
import { Route, Routes, StaticRouter, useParams } from 'react-router';
import { ApiClient, ApiClientProvider } from '../api';
import type { ProblemCatalogue, TaskPreview } from '../api';
import { AppLayout } from '../components/AppLayout';
import { I18nProvider, ru, translate } from '../i18n';
import type { Translate } from '../i18n';
import { StaticHomePage } from '../pages/HomePage';
import { NotFoundPage } from '../pages/NotFoundPage';
import { PrivacyPage } from '../pages/PrivacyPage';
import { StaticProblemPage } from '../pages/ProblemPage';
import { StaticProblemsPage } from '../pages/ProblemsPage';
import { StaticTasksPage } from '../pages/TasksPage';
import { TaskPreviewPage } from '../pages/task/TaskPreviewPage';
import { SessionProvider } from '../session';
import { NOT_FOUND_PATH, PRERENDERED_PATHS, SHELL_PATHS, SiteConfigContext, pageMeta, problemMeta, taskMeta } from '../site';
import type { PageMeta, SiteConfig } from '../site';
import { previewSvg } from './preview';
import { previewImages, publicSeo, seoHead } from './seo';

export { resolveSiteConfig } from '../site';
export { PREVIEW_FONTS } from './preview';

/** What `content export` writes (`src/Ritocode.ContentTool`): the catalogue as `GET /problems` serves it, and the tasks. */
export interface ContentExport {
  readonly problems: ProblemCatalogue;
  /**
   * The tasks in the task catalogue's order, each with what its public page shows and nothing more:
   * `/tasks/{slug}` is an address of the site for these and no other.
   */
  readonly tasks: readonly TaskPreview[];
}

export interface PrerenderInput {
  readonly content: ContentExport;
  /** The `index.html` Vite built, with an empty `#root` — the single-page application's shell. */
  readonly template: string;
  /** Where the site is served, such as `https://ritocode.ru`: the sitemap needs absolute addresses. */
  readonly origin: string;
  readonly config: SiteConfig;
  /** When the content last changed, as `YYYY-MM-DD`, for the sitemap's `lastmod`; left out when absent. */
  readonly lastmod?: string;
  /** The commit the build is made from, for `version.json` and the admin system page (#297); `null` there when absent. */
  readonly commit?: string;
}

const EMPTY_ROOT = '<div id="root"></div>';

/** A slug as the content format allows it — and so a safe file name. */
const SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/**
 * The public pages as static HTML, for search engines that do not run JavaScript — Yandex among them
 * (docs/SPEC.md §4.1): `/`, `/problems` with every card in full and its anchor, `/problems/{slug}`
 * for every card, `/tasks` and `/tasks/{slug}` for every task — its title, difficulty, brief and
 * context, never more — and `/privacy`, each with its title, description and canonical address,
 * its link preview (Open Graph and Twitter tags, with the image `previews` draws) and its structured
 * data (JSON-LD, `seo.ts`; #302);
 * `404.html`, the page the web image serves with 404 for an address the site does not have;
 * `sitemap.xml` listing every public page and `robots.txt`; `version.json`, the commit built, for the
 * admin system page; and the untouched shell under every other fixed address of the site
 * (`SHELL_PATHS`). The files written are the site's addresses, so the web image needs no list of its
 * own (`deploy/web.Caddyfile`, #300). Once the bundle loads, the application renders over the static
 * markup and takes over.
 *
 * Returns the files to write, by path under the build's output directory.
 */
export function prerender({ content, template, origin, config, lastmod, commit }: PrerenderInput): Map<string, string> {
  if (!template.includes(EMPTY_ROOT)) {
    throw new Error(`The template has no empty ${EMPTY_ROOT}: prerender the index.html a fresh 'vite build' wrote.`);
  }

  if (lastmod !== undefined && !/^\d{4}-\d{2}-\d{2}$/.test(lastmod)) {
    throw new Error(`The content date '${lastmod}' is not YYYY-MM-DD; set CONTENT_LASTMOD, such as 2026-11-19, or leave it unset.`);
  }

  if (commit !== undefined && !/^[0-9a-f]{7,40}$/.test(commit)) {
    throw new Error(`The commit '${commit}' is not a git commit; set SOURCE_COMMIT to one, or leave it unset.`);
  }

  for (const slug of [...content.problems.cards.map((card) => card.slug), ...content.tasks.map((task) => task.slug)]) {
    if (!SLUG.test(slug)) {
      throw new Error(`The slug '${slug}' is not lowercase words joined by hyphens, so it cannot name a file.`);
    }
  }

  const site = siteOrigin(origin);
  const t: Translate = (key, params) => translate(ru, 'ru', key, params);
  const files = new Map<string, string>();

  /** The page at `path` rendered into the shell, with its title, its description and `head`. */
  const render = (path: string, meta: PageMeta, head: string): string => {
    const markup = renderToString(<Page path={path} content={content} config={config} />);

    // Replaced through functions: a replacement string would read `$&` in a card's text as a pattern.
    return template
      .replace(EMPTY_ROOT, () => `<div id="root">${markup}</div>`)
      .replace(/<title>[^<]*<\/title>/, () => `<title>${escape(meta.title)}</title>`)
      .replace('</head>', () => `  <meta name="description" content="${escape(meta.description)}" />\n    ${head}\n  </head>`);
  };

  const pages = publicPages(content, t);
  const seo = publicSeo(content, site, t);

  for (const { path, meta } of pages) {
    const url = site + path;
    const page = seo.get(path);

    if (page === undefined) {
      throw new Error(`The public page ${path} has no link preview or structured data.`);
    }

    files.set(fileOf(path), render(path, meta, `<link rel="canonical" href="${escape(url)}" />\n    ${seoHead(url, meta, page, site)}`));
  }

  // Served under any address the site lacks: not to be indexed, and no canonical address of its own.
  files.set('404.html', render(NOT_FOUND_PATH, pageMeta(NOT_FOUND_PATH, t), '<meta name="robots" content="noindex" />'));

  for (const path of SHELL_PATHS) {
    files.set(fileOf(path), template);
  }

  files.set('sitemap.xml', sitemap(site, pages.map((each) => each.path), lastmod));
  files.set('version.json', `${JSON.stringify({ commit: commit ?? null })}\n`);
  files.set('robots.txt', `User-agent: *\nAllow: /\n\nSitemap: ${site}/sitemap.xml\n`);

  return files;
}

/**
 * The preview image of every public page (#302) as SVG, by the path of the PNG the build rasterises it
 * to under its output directory (`scripts/prerender.mjs`): one for `/`, `/problems` and `/tasks`, and
 * one per card and per task, drawn from its text. The pages' `og:image` names these paths.
 */
export function previews(content: ContentExport, origin: string): Map<string, string> {
  const t: Translate = (key, params) => translate(ru, 'ru', key, params);
  const host = new URL(siteOrigin(origin)).host;
  return new Map([...previewImages(content, t)].map(([path, preview]) => [path, previewSvg(preview, host)]));
}

/**
 * What is wrong with the files `prerender` wrote, as one line each — empty when nothing is: every
 * card and every task of the export has its page, holding its name or title as text, and is in the
 * sitemap; and every public page has its link preview, with an image among `images`, and JSON-LD
 * that parses. The build (`scripts/prerender.mjs`) fails on any, so the CI job *Prerender public
 * pages* holds the site to the content (#301, #302).
 */
export function checkPages(content: ContentExport, files: ReadonlyMap<string, string>, images: ReadonlySet<string>): string[] {
  const sitemap = files.get('sitemap.xml') ?? '';
  const problems: string[] = [];

  const check = (path: string, text: string) => {
    const html = files.get(fileOf(path));

    if (html === undefined) {
      problems.push(`${path}: no page was written.`);
    } else if (!html.includes(`>${htmlText(text)}</h1>`)) {
      problems.push(`${path}: the page does not hold '${text}' as its heading.`);
    }

    if (!sitemap.includes(`${escape(path)}</loc>`)) {
      problems.push(`${path}: not in sitemap.xml.`);
    }
  };

  if (content.problems.cards.length === 0 || content.tasks.length === 0) {
    problems.push('The export has no cards or no tasks: the content was not read.');
  }

  for (const card of content.problems.cards) {
    check(`/problems/${card.slug}`, card.name);
  }

  for (const task of content.tasks) {
    check(`/tasks/${task.slug}`, task.title);
  }

  // Every public page has its link preview, with an image the build made, and structured data (#302).
  for (const { path } of publicPages(content, (key) => key)) {
    const html = files.get(fileOf(path));

    if (html === undefined) {
      continue;
    }

    const image = /<meta property="og:image" content="[^"]*?\/(og\/[^"]+)" \/>/.exec(html)?.[1];

    if (image === undefined || !html.includes('<meta property="og:title"') || !html.includes('<meta name="twitter:card"')) {
      problems.push(`${path}: the page has no link preview.`);
    } else if (!images.has(image)) {
      problems.push(`${path}: its preview image ${image} was not made.`);
    }

    const data = structuredData(html);

    if (data.length === 0 || data.some((each) => typeof each !== 'object' || each === null || (each as Record<string, unknown>)['@context'] !== 'https://schema.org')) {
      problems.push(`${path}: the page has no valid structured data.`);
    }
  }

  return problems;
}

/** The JSON-LD documents of a page, parsed; a block that is not JSON is read as `null`. */
export function structuredData(html: string): unknown[] {
  return [...html.matchAll(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/g)].map(([, json]) => {
    try {
      return JSON.parse(json ?? '') as unknown;
    } catch {
      return null;
    }
  });
}

/** The public pages, each prerendered with its canonical address and listed in the sitemap: the fixed ones, then a page per card and per task. */
function publicPages(content: ContentExport, t: Translate): { path: string; meta: PageMeta }[] {
  return [
    ...PRERENDERED_PATHS.map((path) => ({ path, meta: pageMeta(path, t) })),
    ...content.problems.cards.map((card) => ({ path: `/problems/${card.slug}`, meta: problemMeta(card, t) })),
    ...content.tasks.map((task) => ({ path: `/tasks/${task.slug}`, meta: taskMeta(task, t) })),
  ];
}

/** The file the web image serves for an address: `path.html`, and `index.html` for `/`. */
function fileOf(path: string): string {
  return path === '/' ? 'index.html' : `${path.slice(1)}.html`;
}

/**
 * One page inside the providers the application has. Nothing here reaches the network: effects do
 * not run when rendering to a string, so the session is still loading — the header names nobody —
 * and the catalogue and the tasks come from the export rather than from the API.
 */
function Page({ path, content, config }: { path: string; content: ContentExport; config: SiteConfig }) {
  const offline = new ApiClient({
    baseUrl: 'http://prerender.invalid/api/v1',
    fetch: () => Promise.reject(new Error('Nothing is fetched while prerendering.')),
  });

  return (
    <I18nProvider>
      <SiteConfigContext value={config}>
        <ApiClientProvider client={offline}>
          <SessionProvider>
            <StaticRouter location={path}>
              <Routes>
                <Route path="/" element={<AppLayout />}>
                  <Route index element={<StaticHomePage catalogue={content.problems} />} />
                  <Route path="problems" element={<StaticProblemsPage catalogue={content.problems} />} />
                  <Route path="problems/:slug" element={<CardRoute catalogue={content.problems} />} />
                  <Route path="tasks" element={<StaticTasksPage tasks={content.tasks} />} />
                  <Route path="tasks/:slug" element={<TaskRoute tasks={content.tasks} />} />
                  <Route path="privacy" element={<PrivacyPage />} />
                  <Route path="*" element={<NotFoundPage />} />
                </Route>
              </Routes>
            </StaticRouter>
          </SessionProvider>
        </ApiClientProvider>
      </SiteConfigContext>
    </I18nProvider>
  );
}

function CardRoute({ catalogue }: { catalogue: ProblemCatalogue }) {
  const { slug = '' } = useParams();
  return <StaticProblemPage catalogue={catalogue} slug={slug} />;
}

function TaskRoute({ tasks }: { tasks: readonly TaskPreview[] }) {
  const { slug = '' } = useParams();
  const task = tasks.find((each) => each.slug === slug);

  // Only the fields the page shows are handed on: whatever else the export carried stays out of the page.
  return task === undefined ? (
    <NotFoundPage />
  ) : (
    <TaskPreviewPage task={{ slug: task.slug, title: task.title, difficulty: task.difficulty, context: task.context, brief: task.brief }} />
  );
}

function sitemap(site: string, paths: readonly string[], lastmod: string | undefined): string {
  const date = lastmod === undefined ? '' : `<lastmod>${lastmod}</lastmod>`;
  const urls = paths.map((path) => `  <url><loc>${escape(site + path)}</loc>${date}</url>`);
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls.join('\n')}\n</urlset>\n`;
}

/** The origin alone — scheme, host, port — without a trailing slash, or an error for anything else. */
function siteOrigin(origin: string): string {
  let url: URL;

  try {
    url = new URL(origin);
  } catch {
    throw new Error(`The site origin '${origin}' is not an address; set SITE_ORIGIN, such as https://ritocode.ru.`);
  }

  if (url.protocol !== 'https:' && url.protocol !== 'http:') {
    throw new Error(`The site origin '${origin}' is not http or https.`);
  }

  return url.origin;
}

function escape(text: string): string {
  return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
}

/** Text as React writes it into markup. */
function htmlText(text: string): string {
  return escape(text).replaceAll("'", '&#x27;');
}
