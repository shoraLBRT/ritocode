import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { markApplicationMounted, useCountUp } from './useCountUp';

function mockMotion(reduce: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn().mockReturnValue({ matches: reduce, addEventListener: vi.fn(), removeEventListener: vi.fn() }),
  );
}

describe('useCountUp', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('is the final value at once under reduced motion', () => {
    mockMotion(true);
    markApplicationMounted();
    const { result } = renderHook(() => useCountUp(42));

    expect(result.current).toBe(42);
  });

  it('is the final value where matchMedia does not exist', () => {
    vi.stubGlobal('matchMedia', undefined);
    const { result } = renderHook(() => useCountUp(7));

    expect(result.current).toBe(7);
  });

  it('counts up from 0 to the value and ends on it', () => {
    mockMotion(false);
    markApplicationMounted();
    const { result } = renderHook(() => useCountUp(100, { duration: 500 }));

    expect(result.current).toBe(0);
    act(() => {
      vi.advanceTimersByTime(250);
    });
    expect(result.current).toBeGreaterThan(0);
    expect(result.current).toBeLessThan(100);
    act(() => {
      vi.advanceTimersByTime(600);
    });
    expect(result.current).toBe(100);
  });
});
