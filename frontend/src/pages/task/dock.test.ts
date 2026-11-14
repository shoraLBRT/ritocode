import { describe, expect, it } from 'vitest';
import { followScroll, wheelPixels } from './dock';

/** Runs a series of scrolls from rest, the results scrolling by what the dock leaves them. */
function run(max: number, deltas: number[]) {
  let lift = 0;
  let scrollTop = 0;
  return deltas.map((delta) => {
    const move = followScroll(lift, max, scrollTop, delta);
    lift = move.lift;
    scrollTop = Math.max(0, scrollTop + move.scroll);
    return { lift, scrollTop };
  });
}

describe('followScroll', () => {
  it('raises the dock by the scroll, not all at once', () => {
    expect(run(300, [10, 10, 25])).toEqual([
      { lift: 10, scrollTop: 0 },
      { lift: 20, scrollTop: 0 },
      { lift: 45, scrollTop: 0 },
    ]);
  });

  it('scrolls the results only once the dock is up, splitting the scroll that reaches the top', () => {
    expect(followScroll(280, 300, 0, 50)).toEqual({ lift: 300, scroll: 30 });
    expect(followScroll(300, 300, 30, 50)).toEqual({ lift: 300, scroll: 50 });
  });

  it('scrolls the results back to their top before it lowers the dock', () => {
    expect(followScroll(300, 300, 40, -30)).toEqual({ lift: 300, scroll: -30 });
    expect(followScroll(300, 300, 10, -30)).toEqual({ lift: 280, scroll: -10 });
    expect(followScroll(300, 300, 0, -30)).toEqual({ lift: 270, scroll: 0 });
  });

  it('stops at rest and at the top', () => {
    expect(followScroll(20, 300, 0, -100)).toEqual({ lift: 0, scroll: 0 });
    expect(followScroll(0, 300, 0, -100)).toEqual({ lift: 0, scroll: 0 });
    expect(followScroll(0, 0, 0, 100)).toEqual({ lift: 0, scroll: 100 });
  });

  it('brings a lift past a bound that shrank back to it', () => {
    expect(followScroll(500, 300, 0, 10)).toEqual({ lift: 300, scroll: 10 });
    expect(followScroll(500, 300, 0, -10)).toEqual({ lift: 290, scroll: 0 });
  });

  it('follows a trackpad down and back up to rest, in step with the input', () => {
    const steps = run(60, [...Array<number>(10).fill(8), ...Array<number>(10).fill(-8)]);
    expect(steps.map((step) => step.lift)).toEqual([8, 16, 24, 32, 40, 48, 56, 60, 60, 60, 60, 60, 56, 48, 40, 32, 24, 16, 8, 0]);
    expect(steps.map((step) => step.scrollTop)).toEqual([0, 0, 0, 0, 0, 0, 0, 4, 12, 20, 12, 4, 0, 0, 0, 0, 0, 0, 0, 0]);
  });
});

describe('wheelPixels', () => {
  it('reads lines and pages as pixels', () => {
    expect(wheelPixels({ deltaY: 100, deltaMode: 0 }, 400)).toBe(100);
    expect(wheelPixels({ deltaY: 3, deltaMode: 1 }, 400)).toBe(48);
    expect(wheelPixels({ deltaY: -1, deltaMode: 2 }, 400)).toBe(-400);
  });
});
