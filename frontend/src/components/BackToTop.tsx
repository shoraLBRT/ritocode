import { useEffect, useState } from 'react';
import { useT } from '../i18n';
import { Icon } from '../pages/task/parts';

function prefersReducedMotion(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/**
 * A round button that appears after a screen of scroll, above the feedback button, and takes the
 * learner back to the top: smoothly, or at once under reduced motion. Focus goes to the page's
 * heading, so a keyboard or screen-reader user starts again from the top rather than from the button.
 * Hidden it is `visibility: hidden`, so it is neither seen nor reached by Tab; the prerendered page
 * (no scroll, no JavaScript) carries it hidden.
 */
export function BackToTop() {
  const t = useT();
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const update = () => {
      setVisible(window.scrollY > window.innerHeight);
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
    return () => {
      window.removeEventListener('scroll', update);
      window.removeEventListener('resize', update);
    };
  }, []);

  const goTop = () => {
    window.scrollTo({ top: 0, behavior: prefersReducedMotion() ? 'auto' : 'smooth' });
    const heading = document.querySelector<HTMLElement>('#main h1') ?? document.getElementById('main');
    if (heading !== null) {
      if (!heading.hasAttribute('tabindex')) heading.setAttribute('tabindex', '-1');
      heading.focus({ preventScroll: true });
    }
  };

  return (
    <button type="button" className="rc-top" data-visible={visible ? '' : undefined} aria-label={t('app.backToTop')} onClick={goTop}>
      <Icon name="up" />
    </button>
  );
}
