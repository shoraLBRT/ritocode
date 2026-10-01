import '@testing-library/jest-dom/vitest';

// jsdom lays nothing out, so it has no scrollIntoView. A no-op stands in for it, and a test that
// cares where the page scrolls spies on it. A test that runs in Node, as the prerender does, has no
// Element at all.
if (typeof Element !== 'undefined' && !('scrollIntoView' in Element.prototype)) {
  Object.defineProperty(Element.prototype, 'scrollIntoView', { configurable: true, writable: true, value: () => undefined });
}

// The task screen keeps the answer in session storage (src/pages/task/draft.ts); one test's answer
// must not be another's starting point.
if (typeof sessionStorage !== 'undefined') {
  afterEach(() => {
    sessionStorage.clear();
  });
}
