import { renderToString } from 'react-dom/server';
import { Route, Routes, StaticRouter } from 'react-router';
import { ApiClient, ApiClientProvider } from '../api';
import type { ProblemCatalogue } from '../api';
import { AppLayout } from '../components/AppLayout';
import { I18nProvider, ru, translate } from '../i18n';
import type { Translate } from '../i18n';
import { StaticHomePage } from '../pages/HomePage';
import { NotFoundPage } from '../pages/NotFoundPage';
import { PrivacyPage } from '../pages/PrivacyPage';
import { StaticProblemsPage } from '../pages/ProblemsPage';
import { SessionProvider } from '../session';
import { NOT_FOUND_PATH, PRERENDERED_PATHS, SHELL_PATHS, SiteConfigContext, pageMeta } from '../site';
import type { SiteConfig } from '../site';

export { resolveSiteConfig } from '../site';

/** What `content export` writes (`src/Ritocode.ContentTool`): the catalogue as `GET /problems` serves it, and the tasks. */
export interface ContentExport {
  readonly problems: ProblemCatalogue;
  /** The slugs of the tasks: `/tasks/{slug}` is an address of the site for these and no other. */
  readonly tasks: readonly string[];
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
 * (docs/SPEC.md §4.1): `/`, `/problems` with every card in full and its anchor, and `/privacy`, each with its title,
 * description and canonical address; `404.html`, the page the web image serves with 404 for an address
 * the site does not have; `sitemap.xml` and `robots.txt`; `version.json`, the commit built, for the admin system page; and the untouched shell under every other
 * address of the site — the fixed ones (`SHELL_PATHS`) and `tasks/{slug}.html` for each task of the
 * export. The files written are the site's addresses, so the web image needs no list of its own
 * (`deploy/web.Caddyfile`, #300). Once the bundle loads, the application renders over the static
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

  const site = siteOrigin(origin);
  const t: Translate = (key, params) => translate(ru, 'ru', key, params);
  const files = new Map<string, string>();

  /** The page at `path` rendered into the shell, with its title, its description and `head`. */
  const render = (path: string, head: string): string => {
    const markup = renderToString(<Page path={path} content={content} config={config} />);
    const meta = pageMeta(path, t);

    // Replaced through functions: a replacement string would read `$&` in a card's text as a pattern.
    return template
      .replace(EMPTY_ROOT, () => `<div id="root">${markup}</div>`)
      .replace(/<title>[^<]*<\/title>/, () => `<title>${escape(meta.title)}</title>`)
      .replace('</head>', () => `  <meta name="description" content="${escape(meta.description)}" />\n    ${head}\n  </head>`);
  };

  for (const path of PRERENDERED_PATHS) {
    files.set(path === '/' ? 'index.html' : `${path.slice(1)}.html`, render(path, `<link rel="canonical" href="${escape(site + path)}" />`));
  }

  // Served under any address the site lacks: not to be indexed, and no canonical address of its own.
  files.set('404.html', render(NOT_FOUND_PATH, '<meta name="robots" content="noindex" />'));

  for (const path of SHELL_PATHS) {
    files.set(`${path.slice(1)}.html`, template);
  }

  for (const slug of content.tasks) {
    if (!SLUG.test(slug)) {
      throw new Error(`The task slug '${slug}' is not lowercase words joined by hyphens, so it cannot name a file.`);
    }

    files.set(`tasks/${slug}.html`, template);
  }

  files.set('sitemap.xml', sitemap(site, lastmod));
  files.set('version.json', `${JSON.stringify({ commit: commit ?? null })}\n`);
  files.set('robots.txt', `User-agent: *\nAllow: /\n\nSitemap: ${site}/sitemap.xml\n`);

  return files;
}

/**
 * One page inside the providers the application has. Nothing here reaches the network: effects do
 * not run when rendering to a string, so the session is still loading — the header names nobody —
 * and the catalogue comes from the export rather than from the API.
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

function sitemap(site: string, lastmod: string | undefined): string {
  const date = lastmod === undefined ? '' : `<lastmod>${lastmod}</lastmod>`;
  const urls = PRERENDERED_PATHS.map((path) => `  <url><loc>${escape(site + path)}</loc>${date}</url>`);
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
