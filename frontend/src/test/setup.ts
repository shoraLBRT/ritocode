import '@testing-library/jest-dom/vitest';

// jsdom lays nothing out, so it has no scrollIntoView. A no-op stands in for it, and a test that
// cares where the page scrolls spies on it. A test that runs in Node, as the prerender does, has no
// Element at all.
if (typeof Element !== 'undefined' && !('scrollIntoView' in Element.prototype)) {
  Object.defineProperty(Element.prototype, 'scrollIntoView', { configurable: true, writable: true, value: () => undefined });
}
