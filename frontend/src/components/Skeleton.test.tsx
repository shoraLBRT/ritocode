import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { Skeleton } from './Skeleton';

describe('Skeleton', () => {
  it('is a block hidden from assistive technology', () => {
    const { container } = render(<Skeleton />);

    const block = container.firstElementChild;
    expect(block?.tagName).toBe('SPAN');
    expect(block).toHaveClass('rc-skeleton');
    expect(block).toHaveAttribute('aria-hidden', 'true');
  });

  it('takes a size and an extra class', () => {
    const { container } = render(<Skeleton width={120} height="2rem" className="mine" />);

    const block = container.firstElementChild as HTMLElement;
    expect(block).toHaveClass('rc-skeleton', 'mine');
    expect(block.style.width).toBe('120px');
    expect(block.style.height).toBe('2rem');
  });
});
