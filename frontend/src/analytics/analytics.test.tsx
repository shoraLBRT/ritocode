import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen } from '@testing-library/react';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse, problemResponse } from '../test/responses';
import { loadAnalytics, noteSignInStarted, resetAnalytics, resolveAnalyticsConfig, takeSignInStarted, track } from './analytics';

afterEach(() => {
  delete window.umami;
  resetAnalytics();
  document.head.querySelectorAll('script[data-website-id]').forEach((script) => {
    script.remove();
  });
});

describe('the analytics configuration', () => {
  it('is on only when the build names both the script and the site', () => {
    expect(resolveAnalyticsConfig({ VITE_UMAMI_SCRIPT_URL: 'https://stats.example/script.js', VITE_UMAMI_WEBSITE_ID: ' site-1 ' })).toEqual({
      scriptUrl: 'https://stats.example/script.js',
      websiteId: 'site-1',
    });
    expect(resolveAnalyticsConfig({ VITE_UMAMI_SCRIPT_URL: 'https://stats.example/script.js' })).toBeNull();
    expect(resolveAnalyticsConfig({ VITE_UMAMI_WEBSITE_ID: 'site-1', VITE_UMAMI_SCRIPT_URL: '  ' })).toBeNull();
    expect(resolveAnalyticsConfig({})).toBeNull();
  });
});

describe('loading Umami', () => {
  it('adds its script once, deferred, with the site id', () => {
    const config = { scriptUrl: 'https://stats.example/script.js', websiteId: 'site-1' };

    loadAnalytics(config);
    loadAnalytics(config);

    const scripts = document.head.querySelectorAll<HTMLScriptElement>('script[data-website-id]');
    expect(scripts).toHaveLength(1);
    expect(scripts[0]?.src).toBe('https://stats.example/script.js');
    expect(scripts[0]?.defer).toBe(true);
    expect(scripts[0]?.dataset.websiteId).toBe('site-1');
  });

  it('keeps events sent before the script loads, and sends them once it has', () => {
    loadAnalytics({ scriptUrl: 'https://stats.example/script.js', websiteId: 'site-1' });
    track('task-opened', { task: 't' });

    const umami = { track: vi.fn() };
    window.umami = umami;
    document.head.querySelector('script[data-website-id]')?.dispatchEvent(new Event('load'));

    expect(umami.track).toHaveBeenCalledWith('task-opened', { task: 't' });
  });

  it('drops events when no script was added, and never throws for a failing tracker', () => {
    expect(() => {
      track('task-opened');
    }).not.toThrow();

    window.umami = {
      track: () => {
        throw new Error('blocked');
      },
    };
    expect(() => {
      track('task-opened');
    }).not.toThrow();
  });
});

describe('a completed sign-in', () => {
  it('is noted once in the tab, and forgotten as it is read', () => {
    noteSignInStarted('github');

    expect(takeSignInStarted()).toBe('github');
    expect(takeSignInStarted()).toBeNull();
  });

  function api(signedIn: boolean) {
    return vi.fn<typeof globalThis.fetch>().mockImplementation(() =>
      Promise.resolve(signedIn ? jsonResponse({ id: 'u', username: 'developer', admin: false }) : problemResponse(401, 'unauthenticated', 'x')),
    );
  }

  it('is counted with its provider when the page comes back signed in', async () => {
    const umami = { track: vi.fn() };
    window.umami = umami;
    noteSignInStarted('google');

    renderApp(api(true), '/');

    expect(await screen.findByText(ru.session.signedInAs.replace('{username}', 'developer'))).toBeInTheDocument();
    expect(umami.track).toHaveBeenCalledWith('sign-in-completed', { provider: 'google' });
    expect(sessionStorage.length).toBe(0);
  });

  it('is not counted when the page comes back signed out, and the note is dropped', async () => {
    const umami = { track: vi.fn() };
    window.umami = umami;
    noteSignInStarted('google');

    renderApp(api(false), '/');

    expect(await screen.findByText(ru.session.signedOut)).toBeInTheDocument();
    expect(umami.track).not.toHaveBeenCalledWith('sign-in-completed', expect.anything());
    expect(sessionStorage.length).toBe(0);
  });

  it('starts when a provider link is followed', async () => {
    renderApp(api(false), '/');

    fireEvent.click(await screen.findByText(ru.session.signIn));
    const link = screen.getAllByRole('link', { name: ru.session.signInWith.github })[0];
    // jsdom does not navigate; the click handler is what is under test.
    link?.addEventListener('click', (event) => {
      event.preventDefault();
    });
    if (link !== undefined) {
      fireEvent.click(link);
    }

    expect(takeSignInStarted()).toBe('github');
  });
});
