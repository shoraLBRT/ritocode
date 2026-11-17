import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EmptyState } from './EmptyState';

describe('EmptyState', () => {
  it('shows a title, a line and an action, with the mark hidden from assistive tech', () => {
    const { container } = render(
      <EmptyState title="Nothing here" icon="search" action={<button type="button">Reset</button>}>
        Try another word.
      </EmptyState>,
    );

    expect(screen.getByRole('status')).toHaveTextContent('Nothing here');
    expect(screen.getByText('Try another word.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reset' })).toBeInTheDocument();
    expect(container.querySelector('.rc-disc')).toHaveAttribute('aria-hidden', 'true');
  });

  it('still works as a single line of children', () => {
    render(<EmptyState>Just a line.</EmptyState>);

    expect(screen.getByRole('status')).toHaveTextContent('Just a line.');
  });
});
