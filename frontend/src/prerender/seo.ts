import type { ProblemCatalogue, TaskPreview } from '../api';
import type { Translate } from '../i18n';
import { plainText } from '../site';
import type { PageMeta } from '../site';
import { PREVIEW_HEIGHT, PREVIEW_WIDTH, classColour } from './preview';
import type { Preview } from './preview';

/**
 * What a public page tells a messenger, a social network and a search engine about itself besides its
 * title and description (#302): the Open Graph and Twitter tags of its link preview, with its preview
 * image, and its structured data as JSON-LD — `WebSite` and `Organization` on `/`, a `DefinedTermSet`
 * of the cards on `/problems` and a `DefinedTerm` on each card's page, a `LearningResource` for each
 * task.
 */
export interface PageSeo {
  /** The page's preview image under the build's output directory, such as `og/problems/secrets-in-repo.png`. */
  readonly image: string;
  readonly imageAlt: string;
  /** Each a JSON-LD document of schema.org. */
  readonly structuredData: readonly Record<string, unknown>[];
}

const SCHEMA = 'https://schema.org';

/** A task's time as ISO 8601, the upper end of what the tasks page says (`tasks.time`). */
const TASK_TIME = { easy: 'PT5M', medium: 'PT15M', hard: 'PT25M' } as const;

/** The site's name, which is also its owner's: Ritocode has no organisation behind it but itself. */
function organization(site: string, t: Translate) {
  return { '@type': 'Organization', '@id': `${site}/#organization`, name: t('app.name'), url: `${site}/` };
}

function webSite(site: string, t: Translate) {
  return { '@type': 'WebSite', '@id': `${site}/#website`, name: t('app.name'), url: `${site}/`, inLanguage: 'ru' };
}

function termSet(site: string, t: Translate) {
  return { '@type': 'DefinedTermSet', '@id': `${site}/problems#catalogue`, name: t('meta.problems.title'), url: `${site}/problems` };
}

/** The image, its alternative text and the structured data of every public page, by path. */
export function publicSeo(content: { problems: ProblemCatalogue; tasks: readonly TaskPreview[] }, site: string, t: Translate): Map<string, PageSeo> {
  const seo = new Map<string, PageSeo>();
  const alt = (title: string) => t('meta.preview.alt', { title });
  const { cards } = content.problems;

  seo.set('/', {
    image: 'og/home.png',
    imageAlt: alt(t('meta.preview.home.title')),
    structuredData: [
      {
        '@context': SCHEMA,
        '@graph': [
          { ...webSite(site, t), description: t('meta.home.description'), publisher: { '@id': `${site}/#organization` } },
          { ...organization(site, t), description: t('app.tagline') },
        ],
      },
    ],
  });

  seo.set('/problems', {
    image: 'og/problems.png',
    imageAlt: alt(t('meta.preview.problems.title')),
    structuredData: [
      {
        '@context': SCHEMA,
        ...termSet(site, t),
        description: t('meta.problems.description'),
        inLanguage: 'ru',
        hasDefinedTerm: cards.map((card) => ({
          '@type': 'DefinedTerm',
          name: card.name,
          termCode: card.slug,
          description: plainText(card.summary),
          url: `${site}/problems/${card.slug}`,
        })),
      },
    ],
  });

  seo.set('/tasks', {
    image: 'og/tasks.png',
    imageAlt: alt(t('meta.preview.tasks.title')),
    structuredData: [
      {
        '@context': SCHEMA,
        '@type': 'ItemList',
        name: t('meta.tasks.title'),
        url: `${site}/tasks`,
        numberOfItems: content.tasks.length,
        itemListElement: content.tasks.map((task, index) => ({
          '@type': 'ListItem',
          position: index + 1,
          url: `${site}/tasks/${task.slug}`,
          name: task.title,
        })),
      },
    ],
  });

  seo.set('/privacy', {
    image: 'og/home.png',
    imageAlt: alt(t('meta.preview.home.title')),
    structuredData: [
      {
        '@context': SCHEMA,
        '@type': 'WebPage',
        name: t('meta.privacy.title'),
        description: t('meta.privacy.description'),
        url: `${site}/privacy`,
        inLanguage: 'ru',
        isPartOf: { '@id': `${site}/#website` },
      },
    ],
  });

  for (const card of cards) {
    seo.set(`/problems/${card.slug}`, {
      image: `og/problems/${card.slug}.png`,
      imageAlt: alt(card.name),
      structuredData: [
        {
          '@context': SCHEMA,
          '@type': 'DefinedTerm',
          name: card.name,
          termCode: card.slug,
          description: plainText(card.summary),
          url: `${site}/problems/${card.slug}`,
          inLanguage: 'ru',
          inDefinedTermSet: termSet(site, t),
        },
      ],
    });
  }

  for (const task of content.tasks) {
    seo.set(`/tasks/${task.slug}`, {
      image: `og/tasks/${task.slug}.png`,
      imageAlt: alt(task.title),
      structuredData: [
        {
          '@context': SCHEMA,
          '@type': 'LearningResource',
          name: task.title,
          description: plainText(task.brief),
          url: `${site}/tasks/${task.slug}`,
          inLanguage: 'ru',
          learningResourceType: 'exercise',
          educationalLevel: t(`tasks.difficulty.${task.difficulty}`),
          timeRequired: TASK_TIME[task.difficulty],
          isAccessibleForFree: true,
          provider: organization(site, t),
        },
      ],
    });
  }

  return seo;
}

/** The preview image of every public page, as the SVG to rasterise, by its path under the output directory. */
export function previewImages(content: { problems: ProblemCatalogue; tasks: readonly TaskPreview[] }, t: Translate): Map<string, Preview> {
  const images = new Map<string, Preview>();
  const classes = new Map(content.problems.classes.map((each) => [each.id, each.name]));

  images.set('og/home.png', { kicker: t('meta.preview.home.kicker'), title: t('meta.preview.home.title'), text: t('meta.home.description') });
  images.set('og/problems.png', { kicker: t('meta.preview.problems.kicker'), title: t('meta.preview.problems.title'), text: t('meta.problems.description') });
  images.set('og/tasks.png', { kicker: t('meta.preview.tasks.kicker'), title: t('meta.preview.tasks.title'), text: t('meta.tasks.description') });

  for (const card of content.problems.cards) {
    images.set(`og/problems/${card.slug}.png`, {
      kicker: classes.get(card.class) ?? t('meta.preview.problems.kicker'),
      title: card.name,
      text: plainText(card.summary, 400),
      accent: classColour(card.class),
    });
  }

  for (const task of content.tasks) {
    images.set(`og/tasks/${task.slug}.png`, {
      kicker: t('meta.preview.task', { difficulty: t(`tasks.difficulty.${task.difficulty}`), time: t(`tasks.time.${task.difficulty}`) }),
      title: task.title,
      text: plainText(task.brief, 400),
    });
  }

  return images;
}

/** The tags of a page's link preview and its structured data, for its `<head>`. */
export function seoHead(url: string, meta: PageMeta, seo: PageSeo, site: string): string {
  const image = `${site}/${seo.image}`;
  const property = (name: string, value: string) => `<meta property="${name}" content="${escape(value)}" />`;
  const named = (name: string, value: string) => `<meta name="${name}" content="${escape(value)}" />`;

  return [
    property('og:type', 'website'),
    property('og:site_name', 'Ritocode'),
    property('og:locale', 'ru_RU'),
    property('og:url', url),
    property('og:title', meta.title),
    property('og:description', meta.description),
    property('og:image', image),
    property('og:image:type', 'image/png'),
    property('og:image:width', String(PREVIEW_WIDTH)),
    property('og:image:height', String(PREVIEW_HEIGHT)),
    property('og:image:alt', seo.imageAlt),
    named('twitter:card', 'summary_large_image'),
    named('twitter:title', meta.title),
    named('twitter:description', meta.description),
    named('twitter:image', image),
    ...seo.structuredData.map((data) => `<script type="application/ld+json">${jsonInHtml(data)}</script>`),
  ].join('\n    ');
}

/** JSON safe inside a `<script>`: no `</script>`, no comment opener, whatever a card's text holds. */
function jsonInHtml(data: unknown): string {
  return JSON.stringify(data).replaceAll('<', '\\u003c').replaceAll('>', '\\u003e').replaceAll('&', '\\u0026');
}

function escape(text: string): string {
  return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
}
