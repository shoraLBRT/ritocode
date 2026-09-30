import { describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { jsonResponse, problemResponse } from '../test/responses';
import { task } from './task/fixtures';

function urlOf(input: RequestInfo | URL): string {
  if (typeof input === 'string') {
    return input;
  }
  return input instanceof URL ? input.href : input.url;
}

/** Signed out, with the demo task served under its configured slug. */
function api() {
  return vi.fn<typeof globalThis.fetch>().mockImplementation((input) => {
    const url = urlOf(input);

    if (url.endsWith('/me')) {
      return Promise.resolve(problemResponse(401, 'unauthenticated', 'Authentication is required.'));
    }

    if (url.endsWith('/tasks/demo-task')) {
      return Promise.resolve(jsonResponse({ ...task, slug: 'demo-task' }));
    }

    return Promise.resolve(jsonResponse({}));
  });
}

describe('the landing page', () => {
  it('says what Ritocode is and who it is for, and how a task goes', () => {
    renderApp(api(), '/');

    expect(screen.getByRole('heading', { level: 1, name: ru.app.name })).toBeInTheDocument();
    expect(screen.getByText(ru.home.lead)).toBeInTheDocument();
    expect(screen.getByText(ru.home.audience)).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: ru.home.taskTitle })).toBeInTheDocument();
    expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(3);
  });

  it('links to the configured demo task, the task catalogue and the problem catalogue', () => {
    renderApp(api(), '/');
    const actions = within(screen.getByRole('navigation', { name: ru.home.actions }));

    expect(actions.getByRole('link', { name: ru.home.demo })).toHaveAttribute('href', '/tasks/demo-task');
    expect(actions.getByRole('link', { name: ru.home.tasks })).toHaveAttribute('href', '/tasks');
    expect(actions.getByRole('link', { name: ru.home.problems })).toHaveAttribute('href', '/problems');
  });

  it('asks the API for nothing but who is signed in', () => {
    const stub = api();
    renderApp(stub, '/');

    expect(stub.mock.calls.map(([input]) => new URL(urlOf(input)).pathname)).toEqual(['/api/v1/me']);
  });

  it('opens the demo task for a visitor who is not signed in', async () => {
    renderApp(api(), '/');

    screen.getByRole('link', { name: ru.home.demo }).click();

    expect(await screen.findByRole('heading', { level: 1, name: task.title })).toBeInTheDocument();
    expect(await screen.findByText(ru.session.signedOut)).toBeInTheDocument();
  });
});
