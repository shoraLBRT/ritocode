import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
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

describe('the privacy policy', () => {
  it('renders the policy file at /privacy, its sections as headings', async () => {
    renderApp(api({ signedIn: false }), '/privacy');

    expect(await screen.findByRole('heading', { level: 1, name: ru.privacy.title })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Что сайт получает при входе' })).toBeInTheDocument();
    expect(document.title).toBe(ru.meta.privacy.title);
  });

  it('is linked from the footer of every page', async () => {
    renderApp(api(), '/nowhere');

    const footer = within(screen.getByRole('contentinfo'));
    expect(await footer.findByRole('link', { name: ru.app.privacy })).toHaveAttribute('href', '/privacy');
  });
});

describe('signing in and out from the header', () => {
  it('offers both providers to a visitor, each returning to the page they are on', async () => {
    renderApp(api({ signedIn: false }), '/nowhere?q=float#top');

    const header = within(screen.getByRole('banner'));
    fireEvent.click(await header.findByText(ru.session.signIn));

    const returnUrl = encodeURIComponent('/nowhere?q=float#top');
    expect(header.getByRole('link', { name: ru.session.signInWith.github })).toHaveAttribute(
      'href',
      `http://api.test/auth/login/github?returnUrl=${returnUrl}`,
    );
    expect(header.getByRole('link', { name: ru.session.signInWith.google })).toHaveAttribute(
      'href',
      `http://api.test/auth/login/google?returnUrl=${returnUrl}`,
    );
  });

  it('says what signing in hands over, linking the privacy policy, and closes once it is followed', async () => {
    renderApp(api({ signedIn: false }), '/nowhere');

    const header = within(screen.getByRole('banner'));
    fireEvent.click(await header.findByText(ru.session.signIn));

    expect(header.getByText(ru.session.privacyNotice, { exact: false })).toBeInTheDocument();
    expect(header.getByText(ru.session.signIn).closest('details')).toHaveAttribute('open');
    fireEvent.click(header.getByRole('link', { name: ru.session.privacyLink }));

    expect(await screen.findByRole('heading', { level: 1, name: ru.privacy.title })).toBeInTheDocument();
    expect(header.getByText(ru.session.signIn).closest('details')).not.toHaveAttribute('open');
  });

  it('signs out: ends the session on the server, then shows nobody signed in', async () => {
    let signedIn = true;
    const calls: string[] = [];
    const stub = vi.fn<typeof globalThis.fetch>().mockImplementation((input, init) => {
      const url = urlOf(input);
      calls.push(`${init?.method ?? 'GET'} ${url}`);
      if (url.endsWith('/auth/logout')) {
        signedIn = false;
        return Promise.resolve(new Response(null, { status: 204 }));
      }
      if (url.endsWith('/me')) {
        return Promise.resolve(signedIn ? jsonResponse(developer) : problemResponse(401, 'unauthenticated', 'x'));
      }
      return Promise.resolve(jsonResponse([]));
    });
    renderApp(stub);

    fireEvent.click(await screen.findByRole('button', { name: ru.session.signOut }));

    expect(await screen.findByText(ru.session.signedOut)).toBeInTheDocument();
    expect(calls).toContain('POST http://api.test/auth/logout');
    expect(screen.queryByRole('link', { name: ru.nav.progress })).not.toBeInTheDocument();
  });

  it('says why a sign-in reached nobody, until dismissed', async () => {
    renderApp(api({ signedIn: false }), '/nowhere?signInError=provider_already_linked');

    expect(await screen.findByRole('alert')).toHaveTextContent(ru.session.signInError.provider_already_linked);
    fireEvent.click(screen.getByRole('button', { name: ru.session.dismiss }));
    expect(screen.queryByText(ru.session.signInError.provider_already_linked)).not.toBeInTheDocument();
  });

  it('reads an unknown sign-in error as a provider failure', async () => {
    renderApp(api({ signedIn: false }), '/?signInError=something_new');

    expect(await screen.findByRole('alert')).toHaveTextContent(ru.session.signInError.provider_failed);
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

  it('offers the providers with the privacy notice beside them', async () => {
    renderApp(api({ signedIn: false }), '/', table);

    expect(await screen.findByRole('link', { name: ru.session.privacyLink })).toHaveAttribute('href', '/privacy');
  });

  it('offers the providers, returning to the closed page once signed in', async () => {
    renderApp(api({ signedIn: false }), '/progress', [
      { path: '/', Component: RequireSignIn, children: [{ path: 'progress', element: <p>the protected page</p> }] },
    ]);

    expect(await screen.findByRole('link', { name: ru.session.signInWith.google })).toHaveAttribute(
      'href',
      'http://api.test/auth/login/google?returnUrl=%2Fprogress',
    );
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
