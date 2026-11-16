import { useEffect, useRef, useState } from 'react';

let applicationMounted = false;

/**
 * Called once by the top-level component's effect. A prerendered number is shown as it is; a
 * component mounted after this point (its data came from the API) counts up from 0.
 */
export function markApplicationMounted(): void {
  applicationMounted = true;
}

function prefersReducedMotion(): boolean {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return true;
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/** A number that eases (ease-out cubic) from the value shown before to `value`; whole numbers. */
export function useCountUp(value: number, options?: { duration?: number }): number {
  const duration = options?.duration ?? 600;
  const [shown, setShown] = useState(() => (applicationMounted ? 0 : value));
  const shownRef = useRef(shown);

  useEffect(() => {
    if (prefersReducedMotion()) {
      shownRef.current = value;
      return;
    }
    const from = shownRef.current;
    if (from === value) return;
    let frame = 0;
    let start: number | undefined;
    const step = (now: number) => {
      start ??= now;
      const t = Math.min(1, (now - start) / duration);
      const eased = 1 - Math.pow(1 - t, 3);
      const next = t >= 1 ? value : Math.round(from + (value - from) * eased);
      shownRef.current = next;
      setShown(next);
      if (t < 1) frame = requestAnimationFrame(step);
    };
    frame = requestAnimationFrame(step);
    return () => {
      cancelAnimationFrame(frame);
    };
  }, [value, duration]);

  return prefersReducedMotion() ? value : shown;
}
