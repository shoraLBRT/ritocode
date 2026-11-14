import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { ru } from '../i18n';
import { renderWithClient } from '../test/render';
import { jsonResponse, problemResponse } from '../test/responses';
import { FeedbackWidget } from './FeedbackWidget';

/** The feedback widget (docs/DESIGN.md › Feedback): the client is stubbed at `fetch`. */

function renderWidget(fetchStub: typeof globalThis.fetch) {
  renderWithClient(
    <MemoryRouter initialEntries={['/tasks/leaked-key']}>
      <FeedbackWidget />
    </MemoryRouter>,
    fetchStub,
  );
  fireEvent.click(screen.getByRole('button', { name: ru.feedback.open }));
  return screen.getByRole('textbox');
}

function send() {
  fireEvent.click(screen.getByRole('button', { name: ru.feedback.send }));
}

describe('FeedbackWidget', () => {
  it('opens a window with the heading, the text and the button', () => {
    renderWidget(vi.fn<typeof fetch>());

    expect(screen.getByRole('dialog', { name: ru.feedback.heading })).toBeInTheDocument();
    expect(screen.getByRole('textbox')).toHaveAttribute('placeholder', ru.feedback.placeholder);
  });

  it('sends the message with the page and clears it once sent', async () => {
    const fetchStub = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ number: 7 }, 201));
    const text = renderWidget(fetchStub);

    fireEvent.change(text, { target: { value: '  Не видно кнопку  ' } });
    send();

    expect(await screen.findByText(ru.feedback.sent)).toBeInTheDocument();
    expect(text).toHaveValue('');
    const [url, init] = fetchStub.mock.calls[0] ?? [];
    expect(url).toBe('http://api.test/api/v1/feedback');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string)).toEqual({ message: 'Не видно кнопку', page: '/tasks/leaked-key', captchaToken: null });
  });

  it('does not send an empty message', () => {
    const fetchStub = vi.fn<typeof fetch>();
    renderWidget(fetchStub);

    send();

    expect(screen.getByText(ru.feedback.empty)).toBeInTheDocument();
    expect(fetchStub).not.toHaveBeenCalled();
  });

  it.each([
    ['feedback_rate_limited', 429, ru.feedback.rateLimited],
    ['feedback_not_verified', 403, ru.feedback.notVerified],
    ['feedback_unreachable', 503, ru.feedback.unavailable],
    ['feedback_failed', 500, ru.feedback.failed],
  ])('explains %s in a word and keeps the text', async (code, status, word) => {
    const text = renderWidget(vi.fn<typeof fetch>().mockResolvedValue(problemResponse(status, code, 'refused')));

    fireEvent.change(text, { target: { value: 'Сообщение' } });
    send();

    expect(await screen.findByText(word)).toBeInTheDocument();
    expect(text).toHaveValue('Сообщение');
  });

  it('explains a network failure and keeps the text', async () => {
    const text = renderWidget(vi.fn<typeof fetch>().mockRejectedValue(new TypeError('offline')));

    fireEvent.change(text, { target: { value: 'Сообщение' } });
    send();

    await waitFor(() => {
      expect(screen.getByText(ru.feedback.failed)).toBeInTheDocument();
    });
    expect(text).toHaveValue('Сообщение');
  });
});
