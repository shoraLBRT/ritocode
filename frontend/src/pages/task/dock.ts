import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * The step's dock at the bottom of the code (docs/DESIGN.md › The task workspace): at rest it
 * shows one row of results and a sliver of the next; scrolling down the results first raises it
 * until only the file bar and the first line of code are left, then scrolls them; scrolling up from
 * the top lowers it again. A grip on top does the same by a click.
 *
 * `attach` is the results element's callback ref, so the listeners follow it as it comes and
 * goes. Wheel listeners are not passive here: the wheel that raises the dock does not also
 * scroll the results.
 */
export function useRisingDock() {
  const [raised, setRaised] = useState(false);
  const [results, setResults] = useState<HTMLDivElement | null>(null);
  const element = useRef<HTMLDivElement | null>(null);
  const current = useRef(raised);

  useEffect(() => {
    current.current = raised;
  }, [raised]);

  useEffect(() => {
    if (results === null) {
      return;
    }

    const overflows = () => results.scrollHeight > results.clientHeight + 1;
    const onWheel = (event: WheelEvent) => {
      if (!current.current && event.deltaY > 0 && overflows()) {
        event.preventDefault();
        setRaised(true);
      } else if (current.current && event.deltaY < 0 && results.scrollTop <= 0) {
        event.preventDefault();
        setRaised(false);
      }
    };
    // Scrolled some other way — the keyboard, a finger, a scrollbar: the dock rises all the same.
    const onScroll = () => {
      if (!current.current && results.scrollTop > 0) {
        setRaised(true);
      }
    };
    let touchStart: number | null = null;
    const onTouchStart = (event: TouchEvent) => {
      touchStart = event.touches[0]?.clientY ?? null;
    };
    const onTouchMove = (event: TouchEvent) => {
      const y = event.touches[0]?.clientY;
      if (touchStart !== null && y !== undefined && current.current && results.scrollTop <= 0 && y - touchStart > 24) {
        touchStart = null;
        setRaised(false);
      }
    };

    results.addEventListener('wheel', onWheel, { passive: false });
    results.addEventListener('scroll', onScroll, { passive: true });
    results.addEventListener('touchstart', onTouchStart, { passive: true });
    results.addEventListener('touchmove', onTouchMove, { passive: true });
    return () => {
      results.removeEventListener('wheel', onWheel);
      results.removeEventListener('scroll', onScroll);
      results.removeEventListener('touchstart', onTouchStart);
      results.removeEventListener('touchmove', onTouchMove);
    };
  }, [results]);

  /** Back to the first row: the results changed. Lowered as well when there are none to show. */
  const reset = useCallback((lower: boolean) => {
    if (element.current !== null) {
      element.current.scrollTop = 0;
    }
    if (lower) {
      setRaised(false);
    }
  }, []);

  const attach = useCallback((node: HTMLDivElement | null) => {
    element.current = node;
    setResults(node);
  }, []);

  return { raised, setRaised, attach, reset };
}
