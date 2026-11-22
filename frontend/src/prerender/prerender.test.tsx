// @vitest-environment node
// The build runs this in Node, with no DOM: so does the test.
import { describe, expect, it } from 'vitest';
import { ru } from '../i18n';
import { checkPages, prerender, previews, structuredData } from './prerender';
import { wrap } from './preview';
import type { ContentExport } from './prerender';

const template = `<!doctype html>
<html lang="ru">
  <head>
    <meta charset="UTF-8" />
    <title>Ritocode</title>
    <script type="module" crossorigin src="/assets/index-abc.js"></script>
  </head>
  <body>
    <div id="root"></div>
  </body>
</html>
`;

const sections = {
  signs: '- Пароль строкой в `settings.py`.',
  whyAiDoesIt: null,
  cost: 'Стоит $5 000 и больше, если ключ утёк.',
  acceptableWhen: null,
  detection: null,
  treatment: null,
  sources: null,
  counterArguments: null,
};

const content: ContentExport = {
  problems: {
    classes: [
      { id: 'hygiene', name: 'Гигиена и безопасность', description: 'То, что должно быть закрыто оградой.' },
      { id: 'domain', name: 'Предметная область', description: null },
    ],
    cards: [
      { slug: 'secrets-in-repo', class: 'hygiene', name: 'Секреты в репозитории', summary: 'Пароли и ключи в коде.', keywords: ['пароль'], sections },
      { slug: 'money-in-float', class: 'domain', name: 'Деньги во float', summary: 'Копейки теряются.', keywords: [], sections: { ...sections, cost: 'Расхождение с бухгалтерией.' } },
    ],
  },
  tasks: [
    {
      slug: 'orders-report-small-shop',
      title: 'Выгрузка заказов для магазина чая',
      difficulty: 'easy',
      context: 'Интернет-магазин чая: около **двух тысяч** заказов в год.',
      brief: 'Сделай выгрузку заказов в CSV для менеджеров.',
    },
    {
      slug: 'flower-shop-daily-revenue',
      title: 'Выручка цветочной лавки за день',
      difficulty: 'hard',
      context: 'Лавка у метро, одна касса.',
      brief: 'Посчитай выручку за день по чекам.',
    },
  ],
};

/**
 * What a task page must never hold (#301): the export carries none of it — the content tool's test
 * holds it to five fields — but were it ever to, the page still must not show it.
 */
const secrets = {
  findings: [{ card: 'money-in-float', weight: 3 }],
  notes: { 'money-in-float': 'СЕКРЕТНАЯ-ЗАМЕТКА: сумма в float на строке 12.' },
  lesson: 'СЕКРЕТНЫЙ-УРОК',
  cards: ['secrets-in-repo', 'money-in-float'],
};
const leaky: ContentExport = { ...content, tasks: content.tasks.map((task) => ({ ...task, ...secrets })) };

const files = prerender({ content, template, origin: 'https://ritocode.example/', config: { demoTask: 'flower-shop-daily-revenue' }, lastmod: '2026-11-19' });
const svgs = previews(content, 'https://ritocode.example/');
const images = new Set(svgs.keys());

function page(path: string): string {
  const html = files.get(path);

  if (html === undefined) {
    throw new Error(`No ${path} was written.`);
  }

  return html;
}

describe('the prerendered landing page', () => {
  const html = page('index.html');

  it('holds the page itself, in Russian, with the bundle that takes over', () => {
    expect(html).toContain('<html lang="ru">');
    expect(html).toContain(ru.home.lead.slice(0, 40));
    expect(html).toContain('href="/tasks/flower-shop-daily-revenue"');
    expect(html).toContain('href="/problems"');
    expect(html).toContain('src="/assets/index-abc.js"');
  });

  it('carries its title, description and canonical address', () => {
    expect(html).toContain(`<title>${ru.meta.home.title}</title>`);
    expect(html).toContain(`<meta name="description" content="${ru.meta.home.description}" />`);
    expect(html).toContain('<link rel="canonical" href="https://ritocode.example/" />');
  });
});

describe('the prerendered problem catalogue', () => {
  const html = page('problems.html');

  it('shows every card in full, grouped by class', () => {
    expect(html).toContain('Гигиена и безопасность');
    expect(html).toContain('Предметная область');

    for (const card of content.problems.cards) {
      expect(html).toContain(card.name);
      expect(html).toContain(card.summary);
    }

    expect(html).toContain('<code>settings.py</code>');
    expect(html).toContain('Расхождение с бухгалтерией.');
    // A dollar sign in a card is text, not a replacement pattern.
    expect(html).toContain('Стоит $5 000 и больше');
  });

  it('gives every card its anchor, which works without JavaScript', () => {
    expect(html).toContain('id="secrets-in-repo"');
    expect(html).toContain('href="#secrets-in-repo"');
    expect(html).toContain('id="money-in-float"');
  });

  it('carries its own title and description', () => {
    expect(html).toContain(`<title>${ru.meta.problems.title}</title>`);
    expect(html).toContain(`content="${ru.meta.problems.description}"`);
    expect(html).toContain('<link rel="canonical" href="https://ritocode.example/problems" />');
  });
});

describe('the prerendered privacy policy', () => {
  const html = page('privacy.html');

  it('holds the policy file, its title, description and canonical address', () => {
    expect(html).toContain(`<h1>${ru.privacy.title}</h1>`);
    expect(html).toContain('<h2>Что сайт получает при входе</h2>');
    expect(html).toContain(`<title>${ru.meta.privacy.title}</title>`);
    expect(html).toContain(`content="${ru.meta.privacy.description}"`);
    expect(html).toContain('<link rel="canonical" href="https://ritocode.example/privacy" />');
  });
});

describe('the prerendered page of a problem card', () => {
  const html = page('problems/secrets-in-repo.html');

  it('holds the card in full, its class and the way back to the catalogue', () => {
    expect(html).toContain('>Секреты в репозитории</h1>');
    expect(html).toContain('Пароли и ключи в коде.');
    expect(html).toContain('<code>settings.py</code>');
    expect(html).toContain('Стоит $5 000 и больше');
    expect(html).toContain('Гигиена и безопасность');
    expect(html).toContain('href="/problems"');
  });

  it('carries the card as its title and description, and its own canonical address', () => {
    expect(html).toContain('<title>Секреты в репозитории — проблема кода от ИИ · Ritocode</title>');
    expect(html).toContain('<meta name="description" content="Пароли и ключи в коде." />');
    expect(html).toContain('<link rel="canonical" href="https://ritocode.example/problems/secrets-in-repo" />');
  });

  it('is linked from the catalogue, for a reader without JavaScript', () => {
    expect(page('problems.html')).toContain('href="/problems/money-in-float"');
  });
});

describe('the prerendered tasks', () => {
  it('lists every task on /tasks, with its difficulty and link', () => {
    const html = page('tasks.html');

    expect(html).toContain(`<title>${ru.meta.tasks.title}</title>`);
    expect(html).toContain('<link rel="canonical" href="https://ritocode.example/tasks" />');

    for (const task of content.tasks) {
      expect(html).toContain(task.title);
      expect(html).toContain(`href="/tasks/${task.slug}"`);
    }
  });

  it('gives each task a page with its title, difficulty, brief and context', () => {
    const html = page('tasks/orders-report-small-shop.html');

    expect(html).toContain('>Выгрузка заказов для магазина чая</h1>');
    expect(html).toContain(ru.tasks.difficulty.easy);
    expect(html).toContain('Сделай выгрузку заказов в CSV для менеджеров.');
    expect(html).toContain('<strong>двух тысяч</strong>');
    expect(html).toContain('<title>Выгрузка заказов для магазина чая — задача на ревью кода от ИИ · Ritocode</title>');
    expect(html).toContain('<meta name="description" content="Сделай выгрузку заказов в CSV для менеджеров." />');
    expect(html).toContain('<link rel="canonical" href="https://ritocode.example/tasks/orders-report-small-shop" />');
  });

  it('never holds the answer key, the notes, a weight or the cards to pick from, in any file', () => {
    const leaked = prerender({ content: leaky, template, origin: 'https://ritocode.example', config: { demoTask: 'x' } });

    for (const [path, text] of leaked) {
      if (path.startsWith('tasks')) {
        expect(text, path).not.toContain('СЕКРЕТН');
        expect(text, path).not.toContain('weight');
        // The task's pages name no card: neither by slug nor by name.
        for (const card of content.problems.cards) {
          expect(text, path).not.toContain(card.slug);
          expect(text, path).not.toContain(card.name);
        }
      } else {
        expect(text, path).not.toContain('СЕКРЕТН');
      }
    }
  });
});

describe('the data a page is hydrated with (#310)', () => {
  const link = (html: string): string | undefined => /<link rel="preload" id="rc-prerendered" href="\/([^"]+)" as="fetch"[^>]*crossorigin="anonymous" \/>/.exec(html)?.[1];
  const data = (html: string): Record<string, unknown> | null => {
    const file = link(html);
    return file === undefined ? null : (JSON.parse(page(file)) as Record<string, unknown>);
  };

  it('preloads, from the head, a file of what each page the application renders the same was rendered from, for its own address', () => {
    expect(data(page('index.html'))).toMatchObject({ path: '/', arena: { catalogue: { classes: content.problems.classes } } });
    expect(data(page('problems.html'))).toEqual({ path: '/problems', catalogue: content.problems });
    expect(data(page('privacy.html'))).toEqual({ path: '/privacy' });
    expect(page('privacy.html')).toMatch(/<link rel="preload" id="rc-prerendered"[^>]*>\n {2}<\/head>/);
    expect(page('problems.html')).not.toContain('<script type="application/json"');

    const [card] = content.problems.cards;
    if (card === undefined) throw new Error('The test content has no card.');
    const own = data(page(`problems/${card.slug}.html`)) as { path: string; catalogue: { cards: { class: string }[] } };
    expect(own.path).toBe(`/problems/${card.slug}`);
    expect(own.catalogue.cards.every((each) => each.class === card.class)).toBe(true);
  });

  it('gives /tasks each task as a guest lists it, and nothing beyond', () => {
    const tasks = data(page('tasks.html'))?.tasks as { items: unknown[] };
    expect(tasks.items).toEqual(content.tasks.map(({ slug, title, difficulty }) => ({ slug, title, difficulty, status: null })));
  });

  it('gives none to a task page, which the task screen replaces, nor to the page of an unknown address', () => {
    expect(data(page(`tasks/${content.tasks[0]?.slug ?? ''}.html`))).toBeNull();
    expect(data(page('404.html'))).toBeNull();
  });

  it('names each file under assets/ by its content, so it can be cached for good and changes name when the content does', () => {
    expect(link(page('problems.html'))).toMatch(/^assets\/data\/problems-[0-9a-f]{12}\.json$/);
    expect(link(page(`problems/${content.problems.cards[0]?.slug ?? ''}.html`))).toMatch(/^assets\/data\/problems\/[a-z0-9-]+-[0-9a-f]{12}\.json$/);

    const again = prerender({ content, template, origin: 'https://ritocode.example', config: { demoTask: 'x' } }).get('problems.html') ?? '';
    expect(link(again)).toBe(link(page('problems.html')));

    const edited: ContentExport = { ...content, problems: { ...content.problems, cards: content.problems.cards.map((card) => ({ ...card, summary: `${card.summary} Ещё.` })) } };
    const changed = prerender({ content: edited, template, origin: 'https://ritocode.example', config: { demoTask: 'x' } }).get('problems.html') ?? '';
    expect(link(changed)).not.toBe(link(page('problems.html')));
  });
});

describe('what else the build writes', () => {
  it('writes the untouched shell under every other address of the site, and under no other', () => {
    for (const path of ['profile.html', 'admin.html', 'admin/signals.html', 'admin/users.html', 'admin/attempts.html', 'admin/stats.html', 'admin/system.html', 'admin/errors.html']) {
      expect(page(path)).toBe(template);
    }

    expect([...files.values()].filter((text) => text === template)).toHaveLength(8);
    expect([...files.keys()].filter((path) => path.startsWith('tasks/'))).toHaveLength(2);
    expect([...files.keys()].filter((path) => path.startsWith('problems/'))).toHaveLength(2);
    expect(files.has('spa.html')).toBe(false);
  });

  it('finds every card and task with its page, and names what is missing', () => {
    expect(checkPages(content, files, images)).toEqual([]);

    const broken = new Map(files);
    broken.delete('problems/money-in-float.html');
    broken.set('tasks/orders-report-small-shop.html', template);
    broken.set('sitemap.xml', '');

    expect(checkPages(content, broken, images)).toEqual([
      '/problems/secrets-in-repo: not in sitemap.xml.',
      '/problems/money-in-float: no page was written.',
      '/problems/money-in-float: not in sitemap.xml.',
      "/tasks/orders-report-small-shop: the page does not hold 'Выгрузка заказов для магазина чая' as its heading.",
      '/tasks/orders-report-small-shop: not in sitemap.xml.',
      '/tasks/flower-shop-daily-revenue: not in sitemap.xml.',
      '/tasks/orders-report-small-shop: the page has no link preview.',
      '/tasks/orders-report-small-shop: the page has no valid structured data.',
    ]);
    expect(checkPages({ ...content, tasks: [] }, files, images)).toContain('The export has no cards or no tasks: the content was not read.');
  });

  it('writes the page of an unknown address, not to be indexed', () => {
    const html = page('404.html');

    expect(html).toContain(`<h1>${ru.notFound.title}</h1>`);
    expect(html).toContain(`<title>${ru.meta.notFound.title}</title>`);
    expect(html).toContain('<meta name="robots" content="noindex" />');
    expect(html).not.toContain('rel="canonical"');
  });

  it('lists the public pages in the sitemap with the content date, and points robots at it', () => {
    expect(page('sitemap.xml')).toContain('<url><loc>https://ritocode.example/</loc><lastmod>2026-11-19</lastmod></url>');
    expect(page('sitemap.xml')).toContain('<loc>https://ritocode.example/problems</loc>');
    expect(page('sitemap.xml')).toContain('<loc>https://ritocode.example/privacy</loc>');
    expect(page('sitemap.xml')).toContain('<loc>https://ritocode.example/tasks</loc>');
    expect(page('sitemap.xml')).toContain('<loc>https://ritocode.example/problems/money-in-float</loc>');
    expect(page('sitemap.xml')).toContain('<loc>https://ritocode.example/tasks/orders-report-small-shop</loc>');
    expect(page('sitemap.xml')).not.toContain('/404');
    expect(page('robots.txt')).toContain('Sitemap: https://ritocode.example/sitemap.xml');
  });

  it('writes the commit built into version.json, and null without one', () => {
    const built = prerender({ content, template, origin: 'https://ritocode.example', config: { demoTask: 'x' }, commit: 'ac1caf1b2c3d4e5f' });

    expect(JSON.parse(built.get('version.json') ?? '')).toEqual({ commit: 'ac1caf1b2c3d4e5f' });
    expect(JSON.parse(page('version.json'))).toEqual({ commit: null });
    expect(() => prerender({ content, template, origin: 'https://ritocode.example', config: { demoTask: 'x' }, commit: 'main' })).toThrow(/SOURCE_COMMIT/);
  });

  it('leaves the date out of the sitemap when the content has none', () => {
    const undated = prerender({ content, template, origin: 'https://ritocode.example', config: { demoTask: 'x' } });

    expect(undated.get('sitemap.xml')).toContain('<url><loc>https://ritocode.example/</loc></url>');
  });

  it('refuses a content date that is not a date, and a task slug that cannot name a file', () => {
    expect(() => prerender({ content, template, origin: 'https://ritocode.example', config: { demoTask: 'x' }, lastmod: '19.11.2026' })).toThrow(/CONTENT_LASTMOD/);
    const escaping = { ...content.tasks[0], slug: '../escape' } as ContentExport['tasks'][number];
    expect(() => prerender({ content: { ...content, tasks: [escaping] }, template, origin: 'https://ritocode.example', config: { demoTask: 'x' } })).toThrow(/cannot name a file/);
    const card = { ...content.problems.cards[0], slug: 'Bad/Slug' } as ContentExport['problems']['cards'][number];
    expect(() => prerender({ content: { ...content, problems: { ...content.problems, cards: [card] } }, template, origin: 'https://ritocode.example', config: { demoTask: 'x' } })).toThrow(/cannot name a file/);
  });

  it('refuses a template already prerendered, and an origin that is not an address', () => {
    expect(() => prerender({ content, template: page('index.html'), origin: 'https://ritocode.example', config: { demoTask: 'x' } })).toThrow(/empty/);
    expect(() => prerender({ content, template, origin: 'ritocode', config: { demoTask: 'x' } })).toThrow(/SITE_ORIGIN/);
  });
});

/** The JSON-LD of a page, parsed: one document, of schema.org. */
function ld(path: string): Record<string, unknown> {
  const data = structuredData(page(path));
  expect(data).toHaveLength(1);
  const [document] = data as Record<string, unknown>[];
  expect(document?.['@context']).toBe('https://schema.org');
  return document ?? {};
}

/** The content of a `<meta>` tag of a page, by its property or name. */
function tag(html: string, key: string): string | undefined {
  return new RegExp(`<meta (?:property|name)="${key}" content="([^"]*)" />`).exec(html)?.[1];
}

describe('the link previews and structured data (#302)', () => {
  const publicPages = {
    'index.html': 'https://ritocode.example/',
    'problems.html': 'https://ritocode.example/problems',
    'tasks.html': 'https://ritocode.example/tasks',
    'privacy.html': 'https://ritocode.example/privacy',
    'problems/secrets-in-repo.html': 'https://ritocode.example/problems/secrets-in-repo',
    'problems/money-in-float.html': 'https://ritocode.example/problems/money-in-float',
    'tasks/orders-report-small-shop.html': 'https://ritocode.example/tasks/orders-report-small-shop',
    'tasks/flower-shop-daily-revenue.html': 'https://ritocode.example/tasks/flower-shop-daily-revenue',
  };

  it('gives every public page its Open Graph and Twitter tags, with an image the build makes', () => {
    for (const [path, url] of Object.entries(publicPages)) {
      const html = page(path);
      const title = /<title>([^<]*)<\/title>/.exec(html)?.[1];

      expect(tag(html, 'og:url'), path).toBe(url);
      expect(html).toContain(`<link rel="canonical" href="${url}" />`);
      expect(tag(html, 'og:title'), path).toBe(title);
      expect(tag(html, 'og:description'), path).toBe(/<meta name="description" content="([^"]*)" \/>/.exec(html)?.[1]);
      expect(tag(html, 'og:site_name')).toBe('Ritocode');
      expect(tag(html, 'og:locale')).toBe('ru_RU');
      expect(tag(html, 'og:image:width')).toBe('1200');
      expect(tag(html, 'og:image:height')).toBe('630');
      expect(tag(html, 'twitter:card')).toBe('summary_large_image');
      expect(tag(html, 'twitter:title')).toBe(title);

      const image = tag(html, 'og:image') ?? '';
      expect(image, path).toMatch(/^https:\/\/ritocode\.example\/og\/.+\.png$/);
      expect(tag(html, 'twitter:image')).toBe(image);
      expect(images.has(image.slice('https://ritocode.example/'.length)), path).toBe(true);
    }

    expect(tag(page('problems/secrets-in-repo.html'), 'og:image')).toBe('https://ritocode.example/og/problems/secrets-in-repo.png');
    expect(tag(page('tasks/flower-shop-daily-revenue.html'), 'og:image')).toBe('https://ritocode.example/og/tasks/flower-shop-daily-revenue.png');
    expect(tag(page('problems/secrets-in-repo.html'), 'og:image:alt')).toBe('Ritocode: Секреты в репозитории');
  });

  it('describes the site and its owner on the landing page', () => {
    const graph = ld('index.html')['@graph'] as Record<string, unknown>[];

    expect(graph.map((node) => node['@type'])).toEqual(['WebSite', 'Organization']);
    expect(graph[0]).toMatchObject({ name: 'Ritocode', url: 'https://ritocode.example/', inLanguage: 'ru', publisher: { '@id': 'https://ritocode.example/#organization' } });
    expect(graph[1]).toMatchObject({ '@id': 'https://ritocode.example/#organization', name: 'Ritocode', url: 'https://ritocode.example/' });
  });

  it('gives the catalogue as a set of defined terms, and each card as one of them', () => {
    const set = ld('problems.html');
    expect(set).toMatchObject({ '@type': 'DefinedTermSet', name: ru.meta.problems.title, url: 'https://ritocode.example/problems' });
    expect(set.hasDefinedTerm).toEqual([
      { '@type': 'DefinedTerm', name: 'Секреты в репозитории', termCode: 'secrets-in-repo', description: 'Пароли и ключи в коде.', url: 'https://ritocode.example/problems/secrets-in-repo' },
      { '@type': 'DefinedTerm', name: 'Деньги во float', termCode: 'money-in-float', description: 'Копейки теряются.', url: 'https://ritocode.example/problems/money-in-float' },
    ]);

    expect(ld('problems/money-in-float.html')).toMatchObject({
      '@type': 'DefinedTerm',
      name: 'Деньги во float',
      url: 'https://ritocode.example/problems/money-in-float',
      inDefinedTermSet: { '@type': 'DefinedTermSet', '@id': 'https://ritocode.example/problems#catalogue', url: 'https://ritocode.example/problems' },
    });
  });

  it('gives each task as a learning resource, and the tasks page as their list', () => {
    expect(ld('tasks/orders-report-small-shop.html')).toMatchObject({
      '@type': 'LearningResource',
      name: 'Выгрузка заказов для магазина чая',
      description: 'Сделай выгрузку заказов в CSV для менеджеров.',
      url: 'https://ritocode.example/tasks/orders-report-small-shop',
      learningResourceType: 'exercise',
      educationalLevel: 'Лёгкая',
      timeRequired: 'PT5M',
      isAccessibleForFree: true,
      provider: { '@type': 'Organization', name: 'Ritocode' },
    });
    expect(ld('tasks/flower-shop-daily-revenue.html')).toMatchObject({ educationalLevel: 'Сложная', timeRequired: 'PT25M' });

    const list = ld('tasks.html');
    expect(list).toMatchObject({ '@type': 'ItemList', numberOfItems: 2 });
    expect(list.itemListElement).toEqual([
      { '@type': 'ListItem', position: 1, url: 'https://ritocode.example/tasks/orders-report-small-shop', name: 'Выгрузка заказов для магазина чая' },
      { '@type': 'ListItem', position: 2, url: 'https://ritocode.example/tasks/flower-shop-daily-revenue', name: 'Выручка цветочной лавки за день' },
    ]);
    expect(ld('privacy.html')).toMatchObject({ '@type': 'WebPage', url: 'https://ritocode.example/privacy' });
  });

  it('keeps the answer out of a task’s structured data, and a card’s text from closing the script', () => {
    const leakyFiles = prerender({ content: leaky, template, origin: 'https://ritocode.example', config: { demoTask: 'x' } });
    expect(leakyFiles.get('tasks/flower-shop-daily-revenue.html')).not.toContain('СЕКРЕТН');

    const card = { ...content.problems.cards[0], summary: 'Ключ </script><script>alert(1)</script> & "кавычки"' } as ContentExport['problems']['cards'][number];
    const hostile = prerender({ content: { ...content, problems: { ...content.problems, cards: [card] } }, template, origin: 'https://ritocode.example', config: { demoTask: 'x' } });
    const html = hostile.get('problems/secrets-in-repo.html') ?? '';

    expect(html).not.toContain('<script>alert(1)');
    expect(structuredData(html)).toEqual([expect.objectContaining({ description: 'Ключ </script><script>alert(1)</script> & "кавычки"' })]);
  });

  it('leaves the page of an unknown address without a preview, as it is not to be shared', () => {
    expect(page('404.html')).not.toContain('og:');
    expect(structuredData(page('404.html'))).toEqual([]);
  });

  it('draws a preview for each page type and for each card and task, from its text', () => {
    expect([...svgs.keys()]).toEqual([
      'og/home.png',
      'og/problems.png',
      'og/tasks.png',
      'og/problems/secrets-in-repo.png',
      'og/problems/money-in-float.png',
      'og/tasks/orders-report-small-shop.png',
      'og/tasks/flower-shop-daily-revenue.png',
    ]);

    /** The text of each `<text>` of an SVG, without its runs' markup. */
    const texts = (svg: string) => [...svg.matchAll(/<text[^>]*>([\s\S]*?)<\/text>/g)].map(([, inner]) => (inner ?? '').replace(/<tspan x[^>]*>/g, '\n').replace(/<[^>]+>/g, '').trim());

    const card = svgs.get('og/problems/secrets-in-repo.png') ?? '';
    expect(card).toMatch(/^<svg xmlns="http:\/\/www\.w3\.org\/2000\/svg" width="1200" height="630"/);
    expect(texts(card)).toEqual(['Ritocode', 'Гигиена и безопасность', 'Секреты в репозитории', 'Пароли и ключи в коде.', 'ritocode.example']);
    expect(card).toContain('fill="#ec8f7e"');
    expect(texts(svgs.get('og/tasks/flower-shop-daily-revenue.png') ?? '')[1]).toBe('Сложная задача · 20–25 минут');
  });

  it('draws each run of text in the font of its script, at its weight', () => {
    const card = svgs.get('og/problems/money-in-float.png') ?? '';

    expect(card).toContain('<tspan font-family="Preview cyrillic 600">Деньги</tspan><tspan font-family="Preview latin 600"> </tspan>');
    expect(card).toContain('<tspan font-family="Preview latin 600"> float</tspan>');
    expect(card).toContain('<tspan font-family="Preview latin 700">Ritocode</tspan>');
    expect(card).not.toMatch(/font-family="(?!Preview (latin|cyrillic) (400|600|700)")/);
  });

  it('breaks a long title into at most three lines, with an ellipsis where it is cut', () => {
    const long = 'Очень длинное название карточки, которое никак не помещается в одну строку превью и даже в две, а потом ещё и в три строки';
    const { lines, cut } = wrap(long, 68, 600, 3);

    expect(lines).toHaveLength(3);
    expect(cut).toBe(true);
    expect(lines[2]).toMatch(/…$/);
    expect(wrap('Деньги во float', 68, 600, 3)).toEqual({ lines: ['Деньги во float'], cut: false });
  });

  it('names a page whose preview image was not made', () => {
    const fewer = new Set([...images].filter((path) => path !== 'og/tasks.png'));
    expect(checkPages(content, files, fewer)).toEqual(['/tasks: its preview image og/tasks.png was not made.']);
  });
});

describe('the page hashes a release tells IndexNow from (#303)', () => {
  const build = (input: Partial<Parameters<typeof prerender>[0]> = {}) => {
    const written = prerender({ content, template, origin: 'https://ritocode.example', config: { demoTask: 'flower-shop-daily-revenue' }, builtAt: new Date('2026-11-20T10:00:00Z'), ...input });
    const text = written.get('page-hashes.txt') ?? '';
    return new Map(text.trimEnd().split('\n').map((line) => {
      const [hash, path] = line.split(' ');
      return [path, hash] as const;
    }));
  };
  const before = build();

  it('lists every page of the sitemap, and only those, each with a hash', () => {
    const sitemap = page('sitemap.xml');
    const paths = [...sitemap.matchAll(/<loc>https:\/\/ritocode\.example([^<]*)<\/loc>/g)].map((match) => match[1] ?? '');
    expect([...before.keys()].sort()).toEqual(paths.sort());
    for (const hash of before.values()) {
      expect(hash).toMatch(/^[0-9a-f]{16}$/);
    }
  });

  it('stays the same when only the bundle changes, as on a release of code alone', () => {
    expect(build({ template: template.replace('index-abc.js', 'index-def.js') })).toEqual(before);
  });

  it('changes on another day only for the landing page, whose arena turns daily', () => {
    const tomorrow = build({ builtAt: new Date('2026-11-21T10:00:00Z') });
    const changed = [...tomorrow].filter(([path, hash]) => before.get(path) !== hash).map(([path]) => path);
    expect(changed.filter((path) => path !== '/')).toEqual([]);
  });

  it('changes for the pages whose text changed, and for no other', () => {
    const edited: ContentExport = {
      ...content,
      problems: { ...content.problems, cards: content.problems.cards.map((card) => (card.slug === 'money-in-float' ? { ...card, summary: 'Копейки теряются при округлении.' } : card)) },
    };
    const after = build({ content: edited });
    const changed = [...after].filter(([path, hash]) => before.get(path) !== hash).map(([path]) => path);
    expect(changed).toContain('/problems/money-in-float');
    expect(changed).not.toContain('/problems/secrets-in-repo');
    expect(changed).not.toContain('/tasks/orders-report-small-shop');
  });
});
