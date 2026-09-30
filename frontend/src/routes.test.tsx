import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { ru } from './i18n';
import { RequireSignIn } from './session';
import { renderApp } from './test/render';
import { jsonResponse, problemResponse } from './test/responses';

/**
 * The shell end to end: the layout renders, each route resolves, the three states a screen can be
 * in all reach the page, and who is signed in reaches the header and the protected routes. The
 * client is stubbed at `fetch`, so what is under test is this application rather than the backend.
 */

const developer = { id: '0199aa00-0000-7000-8000-000000000001', username: 'developer' };

/** `fetch` accepts a `Request` as well as a string, so the url is read rather than stringified. */
function urlOf(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }
  return input instanceof URL ? input.href : input.url;
}

function fetchStub(handler: (url: string) => Response) {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => Promise.resolve(handler(urlOf(input))));
}

/** The API as the development host answers it: signed in as the seeded developer, no modules. */
function api({ signedIn = true } = {}) {
  return fetchStub((url) => {
    if (url.endsWith('/me')) {
      return signedIn ? jsonResponse(developer) : problemResponse(401, 'unauthenticated', 'Authentication is required.');
    }
    return jsonResponse([]);
  });
}

describe('the application shell', () => {
  it('renders the layout chrome around every route', async () => {
    renderApp(api());

    expect(screen.getByRole('navigation', { name: ru.nav.label })).toBeInTheDocument();
    expect(screen.getByRole('main')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByRole('heading', { level: 1, name: ru.app.name })).toBeInTheDocument();
    });
  });

  it('declares the page to be in Russian', () => {
    document.documentElement.lang = '';
    renderApp(api());

    expect(document.documentElement.lang).toBe('ru');
  });

  it('shows the loading state before the first response arrives', () => {
    renderApp(vi.fn<typeof globalThis.fetch>().mockReturnValue(new Promise<Response>(() => undefined)), '/tasks');

    expect(screen.getByRole('status')).toHaveTextContent(ru.tasks.loading);
  });

  it('shows the failure panel when the API cannot be reached', async () => {
    renderApp(vi.fn<typeof globalThis.fetch>().mockRejectedValue(new TypeError('Failed to fetch')), '/tasks');

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(ru.state.unreachable);
    });
  });

  it('renders the not-found page for an unrouted address', () => {
    renderApp(api(), '/nowhere');

    expect(screen.getByRole('heading', { level: 1, name: ru.notFound.title })).toBeInTheDocument();
  });

  it('names the signed-in learner in the header', async () => {
    renderApp(api());

    expect(await screen.findByText('Вы вошли как developer')).toBeInTheDocument();
  });

  it('says in the header when nobody is signed in', async () => {
    renderApp(api({ signedIn: false }));

    expect(await screen.findByText(ru.session.signedOut)).toBeInTheDocument();
  });
});

describe('a route that needs a signed-in learner', () => {
  const table = [{ path: '/', Component: RequireSignIn, children: [{ index: true, element: <p>the protected page</p> }] }];

  it('renders its page for a signed-in learner', async () => {
    renderApp(api(), '/', table);

    expect(await screen.findByText('the protected page')).toBeInTheDocument();
  });

  it('tells anyone else the page needs a sign-in, and does not render it', async () => {
    renderApp(api({ signedIn: false }), '/', table);

    expect(await screen.findByRole('heading', { level: 1, name: ru.session.requiredTitle })).toBeInTheDocument();
    expect(screen.queryByText('the protected page')).not.toBeInTheDocument();
  });

  it('waits, rather than guessing, while it does not know yet', () => {
    renderApp(vi.fn<typeof globalThis.fetch>().mockReturnValue(new Promise<Response>(() => undefined)), '/', table);

    expect(screen.getByRole('status')).toHaveTextContent(ru.state.loading);
    expect(screen.queryByText('the protected page')).not.toBeInTheDocument();
  });

  it('offers to try again when the API could not say who is signed in', async () => {
    renderApp(vi.fn<typeof globalThis.fetch>().mockRejectedValue(new TypeError('Failed to fetch')), '/', table);

    expect(await screen.findByRole('button', { name: ru.state.retry })).toBeInTheDocument();
    expect(screen.queryByText('the protected page')).not.toBeInTheDocument();
  });
});
