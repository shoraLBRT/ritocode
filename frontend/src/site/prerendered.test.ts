import { loadPrerendered, prerenderedLink } from './prerendered';

describe('the data a prerendered page is hydrated with (#310)', () => {
  const file = '/assets/data/problems-0123456789ab.json';

  const pageWith = (head: string): Document => {
    const doc = document.implementation.createHTMLDocument('page');
    doc.head.innerHTML = head;
    return doc;
  };

  const serving = (response: Response | Error) =>
    vi.fn<typeof globalThis.fetch>(() => (response instanceof Error ? Promise.reject(response) : Promise.resolve(response)));

  it('reads the file the page preloads, as the preload asked for it', async () => {
    const fetch = serving(new Response(JSON.stringify({ path: '/problems', catalogue: { classes: [], cards: [] } })));

    const data = await loadPrerendered(pageWith(prerenderedLink(file)), '/problems', fetch);

    expect(data).toEqual({ path: '/problems', catalogue: { classes: [], cards: [] } });
    expect(fetch).toHaveBeenCalledWith(file, { credentials: 'same-origin' });
  });

  it('has none for a page that names no file, and asks for nothing', async () => {
    const fetch = serving(new Response('{}'));

    expect(await loadPrerendered(pageWith(''), '/profile', fetch)).toBeNull();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('has none when the file is for another address, so the page is rendered afresh', async () => {
    const fetch = serving(new Response(JSON.stringify({ path: '/tasks/one' })));

    expect(await loadPrerendered(pageWith(prerenderedLink(file)), '/tasks/one/attempts/7', fetch)).toBeNull();
  });

  it('has none when the file does not arrive or does not parse', async () => {
    const doc = pageWith(prerenderedLink(file));

    expect(await loadPrerendered(doc, '/problems', serving(new Response('gone', { status: 404 })))).toBeNull();
    expect(await loadPrerendered(doc, '/problems', serving(new Response('<html>')))).toBeNull();
    expect(await loadPrerendered(doc, '/problems', serving(new Response('null')))).toBeNull();
    expect(await loadPrerendered(doc, '/problems', serving(new TypeError('offline')))).toBeNull();
  });
});
