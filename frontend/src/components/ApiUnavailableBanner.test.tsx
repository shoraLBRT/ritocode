import { describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { ApiClient, ApiClientProvider } from '../api';
import { I18nProvider, ru } from '../i18n';
import { ApiUnavailableBanner } from './ApiUnavailableBanner';

function mount(fetchStub: typeof globalThis.fetch) {
  const client = new ApiClient({ baseUrl: 'http://api.test/api/v1', fetch: fetchStub });
  render(
    <I18nProvider>
      <ApiClientProvider client={client}>
        <ApiUnavailableBanner />
      </ApiClientProvider>
    </I18nProvider>,
  );
  const ask = () => act(() => client.request('/tasks').then(() => undefined, () => undefined));
  return { ask };
}

const json = (status: number, body: unknown = {}) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const maintenancePage = () => new Response('<!doctype html><title>Ритокод</title>', { status: 503, headers: { 'Retry-After': '60' } });

describe('ApiUnavailableBanner', () => {
  it('is absent while the API answers', async () => {
    const { ask } = mount(vi.fn<typeof globalThis.fetch>().mockResolvedValue(json(200)));

    await ask();

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it.each([
    ['the request never reaches anything', () => Promise.reject(new TypeError('Failed to fetch'))],
    ['the edge answers with its maintenance page', () => Promise.resolve(maintenancePage())],
    ['a gateway answers 502', () => Promise.resolve(new Response('bad gateway', { status: 502 }))],
    ['a gateway answers 504', () => Promise.resolve(new Response('', { status: 504 }))],
  ])('appears when %s', async (_, answer) => {
    const { ask } = mount(vi.fn<typeof globalThis.fetch>().mockImplementation(answer));

    await ask();

    expect(screen.getByRole('alert')).toHaveTextContent(ru.state.unavailableBanner);
    expect(screen.getByRole('button', { name: ru.state.reload })).toBeInTheDocument();
  });

  it('stays away for a failure the API explained, even a 503 of its own', async () => {
    const problem = { title: 'Feedback is not configured.', status: 503, code: 'feedback_not_configured' };
    const { ask } = mount(vi.fn<typeof globalThis.fetch>().mockResolvedValue(json(503, problem)));

    await ask();

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('goes away with the next answer from the API', async () => {
    const fetchStub = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValueOnce(maintenancePage())
      .mockResolvedValueOnce(json(404, { title: 'Not found.', status: 404, code: 'task_not_found' }));
    const { ask } = mount(fetchStub);

    await ask();
    expect(screen.getByRole('alert')).toBeInTheDocument();

    await ask();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
