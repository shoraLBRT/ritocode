// @vitest-environment node
// The build runs this in Node, with no DOM: so does the test.
import { describe, expect, it } from 'vitest';
import { ru } from '../i18n';
import { prerender } from './prerender';
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
  tasks: ['flower-shop-daily-revenue', 'orders-report-small-shop'],
};

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

describe('what else the build writes', () => {
  it('writes the untouched shell under every other address of the site, and under no other', () => {
    for (const path of ['tasks.html', 'profile.html', 'admin.html', 'admin/signals.html', 'admin/users.html', 'admin/attempts.html', 'admin/stats.html', 'admin/system.html']) {
      expect(page(path)).toBe(template);
    }

    expect(page('tasks/flower-shop-daily-revenue.html')).toBe(template);
    expect(page('tasks/orders-report-small-shop.html')).toBe(template);
    expect([...files.keys()].filter((path) => path.startsWith('tasks/'))).toHaveLength(2);
    expect(files.has('spa.html')).toBe(false);
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
    expect(() => prerender({ content: { ...content, tasks: ['../escape'] }, template, origin: 'https://ritocode.example', config: { demoTask: 'x' } })).toThrow(/cannot name a file/);
  });

  it('refuses a template already prerendered, and an origin that is not an address', () => {
    expect(() => prerender({ content, template: page('index.html'), origin: 'https://ritocode.example', config: { demoTask: 'x' } })).toThrow(/empty/);
    expect(() => prerender({ content, template, origin: 'ritocode', config: { demoTask: 'x' } })).toThrow(/SITE_ORIGIN/);
  });
});
