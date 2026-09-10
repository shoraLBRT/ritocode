import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { ApiError } from '../api';
import { useApiResource } from './useApiResource';

function Probe({ load, deps }: { load: (signal: AbortSignal) => Promise<string>; deps: readonly unknown[] }) {
  const { state, reload } = useApiResource(load, deps);

  return (
    <div>
      <span data-testid="status">{state.status}</span>
      {state.status === 'success' && <span data-testid="data">{state.data}</span>}
      {state.status === 'error' && <span data-testid="error">{state.error.message}</span>}
      <button type="button" onClick={reload}>
        reload
      </button>
    </div>
  );
}

describe('useApiResource', () => {
  it('moves from loading to success', async () => {
    render(<Probe load={() => Promise.resolve('value')} deps={[]} />);

    await waitFor(() => {
      expect(screen.getByTestId('status')).toHaveTextContent('success');
    });
    expect(screen.getByTestId('data')).toHaveTextContent('value');
  });

  it('reports a failure as an error state carrying the ApiError', async () => {
    const load = () => Promise.reject(new ApiError('Problem does not exist.', 'problem', { code: 'problem_not_found' }));

    render(<Probe load={load} deps={[]} />);

    await waitFor(() => {
      expect(screen.getByTestId('error')).toHaveTextContent('Problem does not exist.');
    });
  });

  it('wraps a non-ApiError rejection rather than leaking it to the screen', async () => {
    render(<Probe load={() => Promise.reject(new Error('boom'))} deps={[]} />);

    await waitFor(() => {
      expect(screen.getByTestId('error')).toHaveTextContent('Something went wrong.');
    });
  });

  it('runs the load again when reload is called', async () => {
    const load = vi.fn<(signal: AbortSignal) => Promise<string>>().mockResolvedValue('value');
    render(<Probe load={load} deps={[]} />);

    await waitFor(() => {
      expect(screen.getByTestId('status')).toHaveTextContent('success');
    });
    const before = load.mock.calls.length;

    act(() => {
      screen.getByRole('button', { name: 'reload' }).click();
    });

    await waitFor(() => {
      expect(load.mock.calls.length).toBeGreaterThan(before);
    });
  });

  it('aborts the in-flight request when the component unmounts', async () => {
    let captured: AbortSignal | undefined;
    const load = (signal: AbortSignal) => {
      captured = signal;
      return new Promise<string>(() => {
        // Never settles: the point is what happens to the signal, not to the value.
      });
    };

    const { unmount } = render(<Probe load={load} deps={[]} />);
    await waitFor(() => {
      expect(captured).toBeDefined();
    });

    unmount();

    expect(captured?.aborted).toBe(true);
  });

  it('reports loading the moment the deps change, without a frame of the previous data', async () => {
    const load = vi
      .fn<(signal: AbortSignal) => Promise<string>>()
      .mockResolvedValueOnce('first')
      .mockImplementation(() => new Promise<string>(() => undefined));

    const { rerender } = render(<Probe load={load} deps={['a']} />);
    await waitFor(() => {
      expect(screen.getByTestId('data')).toHaveTextContent('first');
    });

    rerender(<Probe load={load} deps={['b']} />);

    // The second request has not settled, so there is nothing current to show. Reporting the
    // first request's data here would put the previous problem's content under the new url.
    expect(screen.getByTestId('status')).toHaveTextContent('loading');
    expect(screen.queryByTestId('data')).not.toBeInTheDocument();
  });

  it('does not settle a superseded request over a newer one', async () => {
    // The first load is still in flight when the deps change. If its result were applied, the
    // screen would show stale data that no current request asked for.
    let resolveFirst: ((value: string) => void) | undefined;
    const load = vi
      .fn<(signal: AbortSignal) => Promise<string>>()
      .mockImplementationOnce(
        () =>
          new Promise<string>((resolve) => {
            resolveFirst = resolve;
          }),
      )
      .mockResolvedValue('second');

    const { rerender } = render(<Probe load={load} deps={['a']} />);
    rerender(<Probe load={load} deps={['b']} />);

    await waitFor(() => {
      expect(screen.getByTestId('data')).toHaveTextContent('second');
    });

    await act(async () => {
      resolveFirst?.('first');
      await Promise.resolve();
    });

    expect(screen.getByTestId('data')).toHaveTextContent('second');
  });
});
