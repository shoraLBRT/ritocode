import { useEffect, useLayoutEffect, useRef } from 'react';
import type { RefObject } from 'react';

function reducedMotion(): boolean {
  return typeof window === 'undefined' || typeof window.matchMedia !== 'function' || window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/**
 * Fades an element in when `key` changes (not on the first render), without remounting it, so focus
 * and scroll stay where they are. Web Animations API: nothing is left behind when it ends, and
 * nothing runs under `prefers-reduced-motion` or where the API is missing.
 */
export function useFadeOnChange<T extends HTMLElement>(key: string, options?: { from?: number; duration?: number }): RefObject<T | null> {
  const ref = useRef<T>(null);
  const previous = useRef(key);
  const from = options?.from ?? 0.35;
  const duration = options?.duration ?? 120;

  useEffect(() => {
    if (previous.current === key) {
      return;
    }
    previous.current = key;
    const element = ref.current;
    if (element === null || typeof element.animate !== 'function' || reducedMotion()) {
      return;
    }
    element.animate([{ opacity: from }, { opacity: 1 }], { duration, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' });
  }, [key, from, duration]);

  return ref;
}

/**
 * A button's width while it is idle, held while `busy`, so the label and the spinner swapping in do
 * not make the button jump. Put the returned ref on the button.
 */
export function useHeldWidth(busy: boolean): RefObject<HTMLButtonElement | null> {
  const ref = useRef<HTMLButtonElement>(null);
  const idle = useRef(0);

  useLayoutEffect(() => {
    const element = ref.current;
    if (element === null) {
      return;
    }
    if (!busy) {
      idle.current = element.offsetWidth;
      element.style.minWidth = '';
    } else if (idle.current > 0) {
      element.style.minWidth = String(idle.current) + 'px';
    }
  });

  return ref;
}
