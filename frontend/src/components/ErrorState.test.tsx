import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ApiError } from '../api';
import { ErrorState } from './ErrorState';

describe('ErrorState', () => {
  it('quotes the server when the server explained itself', () => {
    render(<ErrorState error={new ApiError('Problem does not exist.', 'problem', { code: 'problem_not_found' })} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Problem does not exist.');
  });

  it('writes its own sentence for an unreachable server, which has nothing to quote', () => {
    render(<ErrorState error={new ApiError('The server could not be reached.', 'network')} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Check that the backend is running.');
  });

  it('names the status for a response that was not the documented envelope', () => {
    render(<ErrorState error={new ApiError('x', 'http', { status: 502 })} />);

    expect(screen.getByRole('alert')).toHaveTextContent('502');
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

    screen.getByRole('button', { name: 'Try again' }).click();
    expect(onRetry).toHaveBeenCalledOnce();

    rerender(<ErrorState error={new ApiError('x', 'network')} />);
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
  });
});
