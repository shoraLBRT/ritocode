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
};

const files = prerender({ content, template, origin: 'https://ritocode.example/', config: { demoTask: 'flower-shop-daily-revenue' } });

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

describe('what else the build writes', () => {
  it('keeps the untouched shell for every other route', () => {
    expect(page('spa.html')).toBe(template);
  });

  it('lists the public pages in the sitemap, and points robots at it', () => {
    expect(page('sitemap.xml')).toContain('<loc>https://ritocode.example/</loc>');
    expect(page('sitemap.xml')).toContain('<loc>https://ritocode.example/problems</loc>');
    expect(page('robots.txt')).toContain('Sitemap: https://ritocode.example/sitemap.xml');
  });

  it('refuses a template already prerendered, and an origin that is not an address', () => {
    expect(() => prerender({ content, template: page('index.html'), origin: 'https://ritocode.example', config: { demoTask: 'x' } })).toThrow(/empty/);
    expect(() => prerender({ content, template, origin: 'ritocode', config: { demoTask: 'x' } })).toThrow(/SITE_ORIGIN/);
  });
});
