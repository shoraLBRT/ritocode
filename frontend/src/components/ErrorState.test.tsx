import { describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { ApiError } from '../api';
import { ru } from '../i18n';
import { ErrorState } from './ErrorState';

describe('ErrorState', () => {
  it('quotes the server when the server explained itself', () => {
    render(<ErrorState error={new ApiError('Problem does not exist.', 'problem', { code: 'problem_not_found' })} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Problem does not exist.');
  });

  it.each([
    ['an unreachable server', new ApiError('The server could not be reached.', 'network')],
    ['the edge answering for it', new ApiError('x', 'http', { status: 503 })],
    ['a gateway failure', new ApiError('x', 'http', { status: 502 })],
  ])('leaves the outage to the banner for %s', (_, error) => {
    render(<ErrorState error={error} />);

    expect(screen.getByRole('alert')).toHaveTextContent(ru.state.unavailable);
  });

  it('names the status for a response that was not the documented envelope', () => {
    render(<ErrorState error={new ApiError('x', 'http', { status: 500 })} />);

    expect(screen.getByRole('alert')).toHaveTextContent('500');
  });

  it('shows the request id, which is what maps a report to a log line', () => {
    render(<ErrorState error={new ApiError('x', 'problem', { code: 'internal_error', requestId: 'abc123' })} />);

    expect(screen.getByRole('alert')).toHaveTextContent('abc123');
  });

  it('lists field errors under a validation failure', () => {
    const error = new ApiError('Validation failed.', 'problem', {
      status: 400,
      code: 'validation_failed',
      fieldErrors: { pageSize: ['Page size must be between 1 and 100.'] },
    });

    render(<ErrorState error={error} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Page size must be between 1 and 100.');
  });

  it('offers a retry only when the caller supplied one', () => {
    const onRetry = vi.fn();
    const { rerender } = render(<ErrorState error={new ApiError('x', 'network')} onRetry={onRetry} />);

    screen.getByRole('button', { name: ru.state.retry }).click();
    expect(onRetry).toHaveBeenCalledOnce();

    rerender(<ErrorState error={new ApiError('x', 'network')} />);
    expect(screen.queryByRole('button', { name: ru.state.retry })).not.toBeInTheDocument();
  });

  it('shows a retry that returns a promise as in flight until it settles', async () => {
    let finish: () => void = () => undefined;
    const onRetry = vi.fn(() => new Promise<void>((resolve) => { finish = resolve; }));
    render(<ErrorState error={new ApiError('x', 'network')} onRetry={onRetry} />);

    const button = screen.getByRole('button', { name: ru.state.retry });
    act(() => { button.click(); });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('aria-busy', 'true');

    await act(async () => { finish(); await Promise.resolve(); });
    expect(button).toBeEnabled();
  });
});
