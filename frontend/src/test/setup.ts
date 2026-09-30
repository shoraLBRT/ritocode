import '@testing-library/jest-dom/vitest';

// jsdom lays nothing out, so it has no scrollIntoView. A no-op stands in for it, and a test that
// cares where the page scrolls spies on it.
if (!('scrollIntoView' in Element.prototype)) {
  Object.defineProperty(Element.prototype, 'scrollIntoView', { configurable: true, writable: true, value: () => undefined });
}
