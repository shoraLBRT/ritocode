// @vitest-environment node
// The build runs this in Node, with no DOM: so does the test.
import { describe, expect, it } from 'vitest';
import { ru } from '../i18n';
import { checkPages, prerender } from './prerender';
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

describe('what else the build writes', () => {
  it('writes the untouched shell under every other address of the site, and under no other', () => {
    for (const path of ['profile.html', 'admin.html', 'admin/signals.html', 'admin/users.html', 'admin/attempts.html', 'admin/stats.html', 'admin/system.html']) {
      expect(page(path)).toBe(template);
    }

    expect([...files.values()].filter((text) => text === template)).toHaveLength(7);
    expect([...files.keys()].filter((path) => path.startsWith('tasks/'))).toHaveLength(2);
    expect([...files.keys()].filter((path) => path.startsWith('problems/'))).toHaveLength(2);
    expect(files.has('spa.html')).toBe(false);
  });

  it('finds every card and task with its page, and names what is missing', () => {
    expect(checkPages(content, files)).toEqual([]);

    const broken = new Map(files);
    broken.delete('problems/money-in-float.html');
    broken.set('tasks/orders-report-small-shop.html', template);
    broken.set('sitemap.xml', '');

    expect(checkPages(content, broken)).toEqual([
      '/problems/secrets-in-repo: not in sitemap.xml.',
      '/problems/money-in-float: no page was written.',
      '/problems/money-in-float: not in sitemap.xml.',
      "/tasks/orders-report-small-shop: the page does not hold 'Выгрузка заказов для магазина чая' as its heading.",
      '/tasks/orders-report-small-shop: not in sitemap.xml.',
      '/tasks/flower-shop-daily-revenue: not in sitemap.xml.',
    ]);
    expect(checkPages({ ...content, tasks: [] }, files)).toContain('The export has no cards or no tasks: the content was not read.');
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
