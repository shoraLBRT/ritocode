import { renderToString } from 'react-dom/server';
import { Route, Routes, StaticRouter } from 'react-router';
import { ApiClient, ApiClientProvider } from '../api';
import type { ProblemCatalogue } from '../api';
import { AppLayout } from '../components/AppLayout';
import { I18nProvider, ru, translate } from '../i18n';
import type { Translate } from '../i18n';
import { HomePage } from '../pages/HomePage';
import { PrivacyPage } from '../pages/PrivacyPage';
import { StaticProblemsPage } from '../pages/ProblemsPage';
import { SessionProvider } from '../session';
import { PRERENDERED_PATHS, SiteConfigContext, pageMeta } from '../site';
import type { SiteConfig } from '../site';

export { resolveSiteConfig } from '../site';

/** What `content export` writes (`src/Ritocode.ContentTool`): the catalogue as `GET /problems` serves it. */
export interface ContentExport {
  readonly problems: ProblemCatalogue;
}

export interface PrerenderInput {
  readonly content: ContentExport;
  /** The `index.html` Vite built, with an empty `#root` — the single-page application's shell. */
  readonly template: string;
  /** Where the site is served, such as `https://ritocode.ru`: the sitemap needs absolute addresses. */
  readonly origin: string;
  readonly config: SiteConfig;
}

const EMPTY_ROOT = '<div id="root"></div>';

/**
 * The public pages as static HTML, for search engines that do not run JavaScript — Yandex among them
 * (docs/SPEC.md §4.1): `/`, `/problems` with every card in full and its anchor, and `/privacy`, each with its title,
 * description and canonical address; `sitemap.xml` and `robots.txt`; and `spa.html`, the untouched
 * shell every other route is served. Once the bundle loads, the application renders over the static
 * markup and takes over.
 *
 * Returns the files to write, by path under the build's output directory.
 */
export function prerender({ content, template, origin, config }: PrerenderInput): Map<string, string> {
  if (!template.includes(EMPTY_ROOT)) {
    throw new Error(`The template has no empty ${EMPTY_ROOT}: prerender the index.html a fresh 'vite build' wrote.`);
  }

  const site = siteOrigin(origin);
  const t: Translate = (key, params) => translate(ru, 'ru', key, params);
  const files = new Map<string, string>();

  for (const path of PRERENDERED_PATHS) {
    const markup = renderToString(<Page path={path} content={content} config={config} />);
    const meta = pageMeta(path, t);
    // Replaced through functions: a replacement string would read `$&` in a card's text as a pattern.
    const html = template
      .replace(EMPTY_ROOT, () => `<div id="root">${markup}</div>`)
      .replace(/<title>[^<]*<\/title>/, () => `<title>${escape(meta.title)}</title>`)
      .replace(
        '</head>',
        () =>
          `  <meta name="description" content="${escape(meta.description)}" />\n`
          + `    <link rel="canonical" href="${escape(site + path)}" />\n  </head>`,
      );

    files.set(path === '/' ? 'index.html' : `${path.slice(1)}.html`, html);
  }

  files.set('spa.html', template);
  files.set('sitemap.xml', sitemap(site));
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
                  <Route index element={<HomePage />} />
                  <Route path="problems" element={<StaticProblemsPage catalogue={content.problems} />} />
                  <Route path="privacy" element={<PrivacyPage />} />
                </Route>
              </Routes>
            </StaticRouter>
          </SessionProvider>
        </ApiClientProvider>
      </SiteConfigContext>
    </I18nProvider>
  );
}

function sitemap(site: string): string {
  const urls = PRERENDERED_PATHS.map((path) => `  <url><loc>${escape(site + path)}</loc></url>`);
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
