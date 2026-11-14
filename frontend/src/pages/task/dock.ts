import { useCallback, useEffect, useRef, useState } from 'react';

/** How a scroll moves the dock: how far it is lifted, and what is left to scroll the results by. */
export interface DockMove {
  lift: number;
  scroll: number;
}

/**
 * The dock follows the scroll (docs/DESIGN.md › The task workspace). `lift` is how far, in
 * pixels, the dock is raised above its rest; `max` how far it can go; `scrollTop` where the results
 * are scrolled to; `delta` the scroll, down positive. Down, the dock rises first and only what is
 * left scrolls the results; up, the results scroll back to their top first and only then does the
 * dock come down.
 */
export function followScroll(lift: number, max: number, scrollTop: number, delta: number): DockMove {
  const top = Math.max(0, max);
  const from = Math.min(Math.max(0, lift), top);
  if (delta > 0) {
    const rise = Math.min(delta, top - from);
    return { lift: from + rise, scroll: delta - rise };
  }
  if (delta < 0) {
    const back = Math.max(delta, -Math.max(0, scrollTop));
    const lower = Math.max(delta - back, -from);
    return { lift: from + lower, scroll: back === 0 ? 0 : back };
  }
  return { lift: from, scroll: 0 };
}

/** A wheel's delta in pixels, whatever unit the device reports it in. */
export function wheelPixels(event: Pick<WheelEvent, 'deltaY' | 'deltaMode'>, page: number): number {
  if (event.deltaMode === 1) {
    return event.deltaY * 16;
  }
  if (event.deltaMode === 2) {
    return event.deltaY * page;
  }
  return event.deltaY;
}

const LIFT = '--dock-lift';
// Far more than any screen: the results grow until the dock's own bound stops them.
const PROBE = 100000;

function px(value: number): string {
  return String(value) + 'px';
}

function reducedMotion(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/**
 * How far the dock can rise: `full` until it meets its bound (the file bar and the first line of
 * code; under the header on a phone), `content` until every result is in sight. Measured on the
 * results themselves, by setting the lift and reading the height they get, so the bound stays in
 * the CSS. Nothing is painted in between.
 */
function measure(results: HTMLElement, shown: number): { full: number; content: number } {
  const scrollTop = results.scrollTop;
  results.style.setProperty(LIFT, '0px');
  const rest = results.clientHeight;
  const content = results.scrollHeight;
  results.style.setProperty(LIFT, px(PROBE));
  const top = results.clientHeight;
  results.style.setProperty(LIFT, px(shown));
  if (results.scrollTop !== scrollTop) {
    results.scrollTop = scrollTop;
  }
  const full = Math.max(0, top - rest);
  return { full, content: Math.min(full, Math.max(0, content - rest)) };
}

/**
 * The step's dock at the bottom of the code (docs/DESIGN.md › The task workspace): at rest it
 * shows one row of results and a sliver of the next; scrolling down the results first raises it,
 * in step with the wheel, the trackpad or the finger, until every result is in sight or only the
 * file bar and the first line of code are left, and only then scrolls them; scrolling up from the
 * top lowers it the same way. A grip on top raises or lowers it all the way by a click.
 *
 * The lift is the `--dock-lift` property on the results element (the CSS adds it to their
 * height), written straight to the element, eased frame by frame towards where the input has
 * taken it — unless the reader asked for reduced motion. `raised` (React state) changes only when
 * the dock reaches its top or leaves it.
 *
 * `attach` is the results element's callback ref, so the listeners follow it as it comes and
 * goes. Wheel and touch-move listeners are not passive: the scroll that raises the dock does not
 * also scroll the results.
 */
export function useRisingDock() {
  const [raised, setRaised] = useState(false);
  const [results, setResults] = useState<HTMLDivElement | null>(null);
  const element = useRef<HTMLDivElement | null>(null);
  /** Where the input has taken the dock. */
  const target = useRef(0);
  /** Where it is drawn now, on the way to the target. */
  const shown = useRef(0);
  const frame = useRef<number | null>(null);

  const draw = useCallback((lift: number) => {
    shown.current = lift;
    element.current?.style.setProperty(LIFT, px(lift));
  }, []);

  const stop = useCallback(() => {
    if (frame.current !== null) {
      cancelAnimationFrame(frame.current);
      frame.current = null;
    }
  }, []);

  /** Takes the dock to `lift`: eased over a few frames, or at once for reduced motion. */
  const moveTo = useCallback(
    (lift: number) => {
      target.current = lift;
      if (reducedMotion() || typeof requestAnimationFrame !== 'function') {
        stop();
        draw(lift);
        return;
      }
      if (frame.current !== null) {
        return;
      }
      const step = () => {
        const gap = target.current - shown.current;
        if (Math.abs(gap) < 0.5) {
          draw(target.current);
          frame.current = null;
          return;
        }
        draw(shown.current + gap * 0.35);
        frame.current = requestAnimationFrame(step);
      };
      frame.current = requestAnimationFrame(step);
    },
    [draw, stop],
  );

  useEffect(() => {
    if (results === null) {
      return;
    }

    /** Moves the dock by a scroll of `delta`; true when the dock took some of it. */
    const follow = (delta: number, scrollRest: boolean): boolean => {
      const { full, content } = measure(results, shown.current);
      const max = Math.max(content, Math.min(target.current, full));
      const move = followScroll(target.current, max, results.scrollTop, delta);
      if (move.lift === target.current) {
        return false;
      }
      moveTo(move.lift);
      setRaised(move.lift > 0 && move.lift >= max - 0.5);
      if (scrollRest && move.scroll !== 0) {
        results.scrollTop += move.scroll;
      }
      return true;
    };

    const onWheel = (event: WheelEvent) => {
      if (event.ctrlKey) {
        return;
      }
      if (follow(wheelPixels(event, results.clientHeight), true)) {
        event.preventDefault();
      }
    };
    // Scrolled some other way — the keyboard, a scrollbar: the dock takes the scroll as it would a
    // wheel's, and the results stay at their top until it is up.
    const onScroll = () => {
      const scrolled = results.scrollTop;
      if (scrolled <= 0) {
        return;
      }
      const { full, content } = measure(results, shown.current);
      const max = Math.max(content, Math.min(target.current, full));
      if (target.current >= max - 0.5) {
        return;
      }
      const move = followScroll(target.current, max, 0, scrolled);
      moveTo(move.lift);
      setRaised(move.lift >= max - 0.5);
      results.scrollTop = move.scroll;
    };
    let touchY: number | null = null;
    const onTouchStart = (event: TouchEvent) => {
      touchY = event.touches[0]?.clientY ?? null;
    };
    const onTouchMove = (event: TouchEvent) => {
      const y = event.touches[0]?.clientY;
      if (touchY === null || y === undefined) {
        return;
      }
      const delta = touchY - y;
      touchY = y;
      if (event.cancelable && follow(delta, true)) {
        event.preventDefault();
      }
    };

    results.addEventListener('wheel', onWheel, { passive: false });
    results.addEventListener('scroll', onScroll, { passive: true });
    results.addEventListener('touchstart', onTouchStart, { passive: true });
    results.addEventListener('touchmove', onTouchMove, { passive: false });
    return () => {
      results.removeEventListener('wheel', onWheel);
      results.removeEventListener('scroll', onScroll);
      results.removeEventListener('touchstart', onTouchStart);
      results.removeEventListener('touchmove', onTouchMove);
    };
  }, [results, moveTo]);

  useEffect(() => stop, [stop]);

  /** The grip: all the way up, or all the way down. */
  const toggle = useCallback(() => {
    const node = element.current;
    if (raised) {
      moveTo(0);
      setRaised(false);
      return;
    }
    if (node !== null) {
      moveTo(measure(node, shown.current).full);
    }
    setRaised(true);
  }, [raised, moveTo]);

  /** Back to the first row: the results changed. Lowered as well when there are none to show. */
  const reset = useCallback(
    (lower: boolean) => {
      if (element.current !== null) {
        element.current.scrollTop = 0;
      }
      if (lower) {
        stop();
        target.current = 0;
        draw(0);
        setRaised(false);
      }
    },
    [draw, stop],
  );

  const attach = useCallback(
    (node: HTMLDivElement | null) => {
      element.current = node;
      // A new results element starts at rest unless the dock is held up.
      node?.style.setProperty(LIFT, px(shown.current));
      setResults(node);
    },
    [],
  );

  return { raised, toggle, attach, reset };
}
