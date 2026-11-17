import { useEffect, useRef } from 'react';

/**
 * For a `position: sticky` bar: sets `data-stuck` on it while it is held at its `top` offset by the
 * scroll, so CSS can give it a border or a shadow only then. It reads the bar's own computed `top`,
 * so it needs no knowledge of the header; where the bar does not stick (`position` is not sticky,
 * as on a phone) it never marks it. Written on the element, not in state: scrolling re-renders nothing.
 */
export function useStuck<T extends HTMLElement>() {
  const ref = useRef<T>(null);

  useEffect(() => {
    const element = ref.current;
    if (element === null) return;
    let frame = 0;

    const update = () => {
      frame = 0;
      const style = getComputedStyle(element);
      const top = Number.parseFloat(style.top);
      const stuck = style.position === 'sticky' && !Number.isNaN(top) && element.getBoundingClientRect().top <= top + 0.5 && window.scrollY > 0;
      if (stuck) {
        element.dataset.stuck = '';
      } else {
        delete element.dataset.stuck;
      }
    };
    const schedule = () => {
      frame ||= requestAnimationFrame(update);
    };

    update();
    window.addEventListener('scroll', schedule, { passive: true });
    window.addEventListener('resize', schedule);
    return () => {
      window.removeEventListener('scroll', schedule);
      window.removeEventListener('resize', schedule);
      cancelAnimationFrame(frame);
    };
  }, []);

  return ref;
}
