import { afterEach, describe, expect, it, onTestFinished, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { STUDY_MARKS_CHANGED } from '../hooks/useStudyMarks';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse, problemResponse } from '../test/responses';

const sections = {
  signs: '- Цены объявлены как `float`.\n- Итог округляется в конце.',
  whyAiDoesIt: null,
  cost: 'Копейки расходятся с бухгалтерией.',
  acceptableWhen: 'Никогда.',
  detection: null,
  treatment: 'Хранить в `Decimal`.',
  sources: null,
  counterArguments: null,
};

const catalogue = {
  classes: [
    { id: 'hygiene', name: 'Гигиена и безопасность', description: 'То, что должно быть закрыто оградой.' },
    { id: 'growth', name: 'Разрастание', description: null },
    { id: 'domain', name: 'Предметная область', description: null },
  ],
  cards: [
    {
      slug: 'secrets-in-repo',
      class: 'hygiene',
      name: 'Секреты в репозитории',
      summary: 'Пароли и токены прямо в коде.',
      keywords: ['пароль', 'api key'],
      sections,
    },
    {
      slug: 'money-in-float',
      class: 'domain',
      name: 'Деньги во float',
      summary: 'Суммы считаются в числах с плавающей точкой.',
      keywords: ['копейки', 'decimal'],
      sections,
    },
  ],
};

function byId(id: string): HTMLElement {
  const element = document.getElementById(id);

  if (element === null) {
    throw new Error(`Nothing on the page has the id '${id}'.`);
  }

  return element;
}

function api(body: object = catalogue) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    if (url.endsWith('/me/studied')) {
      return Promise.resolve(jsonResponse({ cards: [] }));
    }
    return Promise.resolve(url.endsWith('/me') ? jsonResponse({ id: 'u', username: 'developer', admin: false, createdAt: '2026-03-14T09:26:53+00:00', providers: [] }) : jsonResponse(body));
  });
}

describe('the problem catalogue', () => {
  it('groups the cards by class, in the classes order, and leaves out classes with none', async () => {
    renderApp(api(), '/problems');

    await screen.findByRole('heading', { level: 3, name: 'Секреты в репозитории' });
    const classes = screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent);

    expect(classes).toEqual(['Гигиена и безопасность', 'Предметная область']);
  });

  it('shows a folded card as its class, name and summary, its sections folded in the page', async () => {
    renderApp(api(), '/problems');

    await screen.findByRole('heading', { level: 3, name: 'Деньги во float' });
    const money = byId('money-in-float');

    expect(money).toHaveClass('rc-pcard', 'k-domain');
    expect(money).toHaveTextContent('Предметная область');
    expect(within(money).getByText('Суммы считаются в числах с плавающей точкой.')).toBeInTheDocument();
    // Without JavaScript the sections unfold in place, so they are in the page, in a closed <details>.
    const details = money.querySelector('details');
    expect(details).not.toBeNull();
    expect(details?.open).toBe(false);
    expect(details).toHaveTextContent('Копейки расходятся с бухгалтерией.');
  });

  it('opens a card at the centre with every section, and closes it with Esc, back to the card', async () => {
    renderApp(api(), '/problems');

    const open = await screen.findByLabelText(ru.problems.openCard.replace('{name}', 'Деньги во float'));
    fireEvent.click(open);

    const dialog = screen.getByRole('dialog', { name: 'Деньги во float' });
    expect(dialog).toHaveClass('rc-pcard--open', 'k-domain');
    expect(within(dialog).getByRole('heading', { level: 4, name: ru.problems.sections.acceptableWhen })).toBeInTheDocument();
    expect(within(dialog).getByText('Никогда.')).toBeInTheDocument();
    expect(within(dialog).getAllByRole('listitem')).toHaveLength(2);
    expect(within(dialog).getByText('float').tagName).toBe('CODE');
    // A section the card leaves empty has no heading.
    expect(within(dialog).queryByRole('heading', { level: 4, name: ru.problems.sections.detection })).not.toBeInTheDocument();
    // The details did not unfold in place; the card's place stays as an outline; the address names it.
    expect(byId('money-in-float').querySelector('details')?.open).toBe(false);
    expect(byId('money-in-float')).toHaveClass('rc-pcard--lifted');
    expect(window.location.hash).toBe('#money-in-float');
    expect(within(dialog).getByRole('button', { name: ru.problems.close })).toHaveFocus();

    fireEvent.keyDown(dialog, { key: 'Escape' });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(window.location.hash).toBe('');
    expect(open).toHaveFocus();
  });

  it('closes an open card with its close button or a click outside it', async () => {
    renderApp(api(), '/problems');

    fireEvent.click(await screen.findByLabelText(ru.problems.openCard.replace('{name}', 'Секреты в репозитории')));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: ru.problems.close }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText(ru.problems.openCard.replace('{name}', 'Секреты в репозитории')));
    // The backdrop is the other close control; it is outside the dialog.
    const backdrop = screen.getAllByRole('button', { name: ru.problems.close }).find((button) => !screen.getByRole('dialog').contains(button));
    if (backdrop === undefined) {
      throw new Error('The open card has no backdrop to click.');
    }
    fireEvent.click(backdrop);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('filters by class, counting what each class would show', async () => {
    renderApp(api(), '/problems');

    const filter = within(await screen.findByRole('group', { name: ru.problems.classFilter }));
    expect(filter.getByRole('button', { name: `${ru.problems.allClasses} 2` })).toHaveAttribute('aria-pressed', 'true');
    expect(filter.getByRole('button', { name: 'Разрастание 0' })).toBeInTheDocument();

    fireEvent.click(filter.getByRole('button', { name: 'Предметная область 1' }));
    expect(document.getElementById('money-in-float')).not.toBeNull();
    expect(document.getElementById('secrets-in-repo')).toBeNull();
    expect(screen.getByText('1 карточка')).toBeInTheDocument();

    // The pressed class again shows them all.
    fireEvent.click(filter.getByRole('button', { name: 'Предметная область 1' }));
    expect(document.getElementById('secrets-in-repo')).not.toBeNull();
  });

  it('searches names, summaries and keywords, ignoring case and ё', async () => {
    const umami = { track: vi.fn() };
    window.umami = umami;
    renderApp(api(), '/problems');
    const search = await screen.findByLabelText(ru.problems.search);

    fireEvent.change(search, { target: { value: 'КОПЕЙКИ' } });
    expect(document.getElementById('money-in-float')).not.toBeNull();
    expect(document.getElementById('secrets-in-repo')).toBeNull();
    expect(screen.getByText('1 карточка')).toBeInTheDocument();

    fireEvent.change(search, { target: { value: 'API KEY' } });
    expect(document.getElementById('secrets-in-repo')).not.toBeNull();

    fireEvent.change(search, { target: { value: 'плавающеи точкои' } });
    expect(document.getElementById('money-in-float')).toBeNull();

    fireEvent.change(search, { target: { value: 'ничего такого нет' } });
    expect(screen.getByText(ru.problems.noMatch)).toBeInTheDocument();

    // A search is counted once a visit, and what was typed is not sent.
    expect(umami.track.mock.calls).toEqual([['catalogue-search', undefined]]);
  });

  it('clears the search from the empty state', async () => {
    renderApp(api(), '/problems');
    const search = await screen.findByLabelText(ru.problems.search);

    fireEvent.change(search, { target: { value: 'ничего такого нет' } });
    fireEvent.click(screen.getByRole('button', { name: ru.problems.resetSearch }));

    expect(search).toHaveValue('');
    expect(screen.queryByText(ru.problems.noMatch)).not.toBeInTheDocument();
    expect(document.getElementById('money-in-float')).not.toBeNull();
  });

  it('treats ё and е as the same letter', async () => {
    const withYo = { ...catalogue, cards: [{ ...catalogue.cards[0], summary: 'Всё лежит в коде.' }] };
    renderApp(api(withYo), '/problems');

    fireEvent.change(await screen.findByLabelText(ru.problems.search), { target: { value: 'все лежит' } });

    expect(document.getElementById('secrets-in-repo')).not.toBeNull();
  });

  it('gives every card an anchor, and opens the card the address names', async () => {
    const scrolled: string[] = [];
    vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(function (this: Element) {
      scrolled.push(this.id);
    });

    renderApp(api(), '/problems#money-in-float');

    await waitFor(() => {
      expect(scrolled).toEqual(['money-in-float']);
    });
    expect(screen.getByRole('dialog', { name: 'Деньги во float' })).toBeInTheDocument();
    // The folded card's seal is its anchor, and the open card carries the same link in its band.
    expect(within(byId('money-in-float')).getByRole('link', { name: 'Ссылка на карточку «Деньги во float»' })).toHaveAttribute('href', '#money-in-float');
    expect(within(screen.getByRole('dialog')).getByRole('link', { name: 'Ссылка на карточку «Деньги во float»' })).toHaveAttribute('href', '#money-in-float');
  });

  it('shows how many cards each class holds, and how many of them a search leaves', async () => {
    const more = {
      ...catalogue,
      cards: [
        ...catalogue.cards,
        { slug: 'tokens-in-logs', class: 'hygiene', name: 'Токены в логах', summary: 'Ключ попадает в лог.', keywords: ['лог'], sections },
      ],
    };
    renderApp(api(more), '/problems');
    const search = await screen.findByLabelText(ru.problems.search);

    const heading = (name: string) => screen.getByRole('heading', { name }).closest('.rc-catalogue__class-head');
    expect(heading('Гигиена и безопасность')).toHaveTextContent('2');
    expect(heading('Предметная область')).toHaveTextContent('1');

    fireEvent.change(search, { target: { value: 'пароль' } });
    expect(heading('Гигиена и безопасность')).toHaveTextContent('1 из 2');
  });

  it('clears the search with its × button, which shows only when there is text', async () => {
    renderApp(api(), '/problems');
    const search = await screen.findByLabelText(ru.problems.search);
    expect(screen.queryByRole('button', { name: ru.problems.clearSearch })).not.toBeInTheDocument();

    fireEvent.change(search, { target: { value: 'копейки' } });
    fireEvent.click(screen.getByRole('button', { name: ru.problems.clearSearch }));

    expect(search).toHaveValue('');
    expect(search).toHaveFocus();
    expect(document.getElementById('secrets-in-repo')).not.toBeNull();
    expect(screen.queryByRole('button', { name: ru.problems.clearSearch })).not.toBeInTheDocument();
  });
});

const ME = { id: 'u', username: 'developer', admin: false, createdAt: '2026-03-14T09:26:53+00:00', providers: [] };

/** The catalogue, signed in with `studied` marked, or as a guest; every request is recorded. */
function studyApi(options: { signedIn: boolean; studied?: string[] }) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input, init) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const method = init?.method ?? 'GET';
    if (url.endsWith('/me')) {
      return Promise.resolve(options.signedIn ? jsonResponse(ME) : problemResponse(401, 'unauthenticated', 'Sign in.'));
    }
    if (url.endsWith('/me/studied')) {
      return Promise.resolve(jsonResponse({ cards: (options.studied ?? []).map((card) => ({ card, studiedAt: '2026-11-18T10:00:00+00:00' })) }));
    }
    if (url.includes('/me/studied/')) {
      const card = url.slice(url.lastIndexOf('/') + 1);
      return Promise.resolve(method === 'DELETE' ? new Response(null, { status: 204 }) : jsonResponse({ card, studiedAt: '2026-11-19T10:00:00+00:00' }));
    }
    return Promise.resolve(jsonResponse(catalogue));
  });
}

function requests(fetchStub: ReturnType<typeof studyApi>, path: string): string[] {
  return fetchStub.mock.calls
    .filter(([input]) => (typeof input === 'string' ? input : input instanceof URL ? input.href : input.url).endsWith(path))
    .map(([, init]) => init?.method ?? 'GET');
}

function classHead(id: string): HTMLElement {
  const heading = byId(`class-${id}`).parentElement;
  if (heading === null) {
    throw new Error(`The class '${id}' has no heading.`);
  }
  return heading;
}

const studiedLabel = (name: string) => ru.problems.studiedCard.replace('{name}', name);

const classStudied = (studied: number, total: number) =>
  ru.problems.classStudied.replace('{studied}', String(studied)).replace('{total}', String(total));

describe('studying the catalogue', () => {
  afterEach(() => {
    localStorage.clear();
  });

  it('shows a signed-in learners marks on the folded cards and counts them in the class heading', async () => {
    renderApp(studyApi({ signedIn: true, studied: ['secrets-in-repo'] }), '/problems');
    await screen.findByRole('heading', { level: 3, name: 'Секреты в репозитории' });

    expect(await within(byId('secrets-in-repo')).findByRole('img', { name: studiedLabel('Секреты в репозитории') })).toBeInTheDocument();
    expect(byId('secrets-in-repo')).toHaveClass('rc-pcard--studied');
    expect(within(byId('money-in-float')).queryByRole('img', { name: studiedLabel('Деньги во float') })).not.toBeInTheDocument();
    expect(classHead('hygiene')).toHaveTextContent(classStudied(1, 1));
    expect(classHead('domain')).toHaveTextContent(classStudied(0, 1));
  });

  it('marks the open card and takes the mark back through the API, signed in', async () => {
    const fetchStub = studyApi({ signedIn: true });
    // Each stored or taken-back mark tells the header's streak to read the marks again.
    let changes = 0;
    const changed = () => {
      changes += 1;
    };
    window.addEventListener(STUDY_MARKS_CHANGED, changed);
    onTestFinished(() => {
      window.removeEventListener(STUDY_MARKS_CHANGED, changed);
    });
    renderApp(fetchStub, '/problems');

    fireEvent.click(await screen.findByLabelText(ru.problems.openCard.replace('{name}', 'Деньги во float')));
    const toggle = await within(screen.getByRole('dialog')).findByRole('button', { name: ru.problems.studied });
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    // Signed in, the marks are the account's: no note about the browser.
    expect(screen.queryByText(ru.problems.guestStudied)).not.toBeInTheDocument();

    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute('aria-pressed', 'true');
    await waitFor(() => {
      expect(requests(fetchStub, '/me/studied/money-in-float')).toEqual(['PUT']);
    });
    expect(classHead('domain')).toHaveTextContent(classStudied(1, 1));

    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    await waitFor(() => {
      expect(requests(fetchStub, '/me/studied/money-in-float')).toEqual(['PUT', 'DELETE']);
    });
    await waitFor(() => {
      expect(changes).toBe(2);
    });
    expect(localStorage.length).toBe(0);
  });

  it('puts the mark back when the API refuses it', async () => {
    const fetchStub = studyApi({ signedIn: true });
    const refusing = vi.fn<typeof globalThis.fetch>().mockImplementation((input, init) =>
      init?.method === 'PUT' ? Promise.resolve(problemResponse(404, 'card_not_found', 'No such card.')) : fetchStub(input, init),
    );
    renderApp(refusing, '/problems');

    fireEvent.click(await screen.findByLabelText(ru.problems.openCard.replace('{name}', 'Деньги во float')));
    const toggle = await within(screen.getByRole('dialog')).findByRole('button', { name: ru.problems.studied });
    fireEvent.click(toggle);

    await waitFor(() => {
      expect(toggle).toHaveAttribute('aria-pressed', 'false');
    });
  });

  it('keeps a guests marks in this browser only, and asks the API nothing about them', async () => {
    const fetchStub = studyApi({ signedIn: false });
    const { unmount } = renderApp(fetchStub, '/problems');

    fireEvent.click(await screen.findByLabelText(ru.problems.openCard.replace('{name}', 'Деньги во float')));
    const dialog = screen.getByRole('dialog');
    const toggle = await within(dialog).findByRole('button', { name: ru.problems.studied });
    expect(within(dialog).getByText(ru.problems.guestStudied)).toBeInTheDocument();

    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute('aria-pressed', 'true');
    expect(JSON.parse(localStorage.getItem('ritocode:studied') ?? '[]')).toEqual(['money-in-float']);
    expect(fetchStub.mock.calls.some(([input]) => String(input instanceof Request ? input.url : input).includes('/studied'))).toBe(false);

    unmount();
    renderApp(studyApi({ signedIn: false }), '/problems');
    await screen.findByRole('heading', { level: 3, name: 'Деньги во float' });
    expect(await within(byId('money-in-float')).findByRole('img', { name: studiedLabel('Деньги во float') })).toBeInTheDocument();
    expect(classHead('domain')).toHaveTextContent(classStudied(1, 1));
  });

  it('does not carry a guests marks over on sign-in', async () => {
    localStorage.setItem('ritocode:studied', JSON.stringify(['money-in-float']));
    renderApp(studyApi({ signedIn: true }), '/problems');
    await screen.findByRole('heading', { level: 3, name: 'Деньги во float' });

    await waitFor(() => {
      expect(classHead('domain')).toHaveTextContent(classStudied(0, 1));
    });
    expect(within(byId('money-in-float')).queryByRole('img', { name: studiedLabel('Деньги во float') })).not.toBeInTheDocument();
  });
});
