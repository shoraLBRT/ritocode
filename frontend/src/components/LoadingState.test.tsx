import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LoadingState } from './LoadingState';

describe('LoadingState', () => {
  it('announces itself to a screen reader rather than showing a silent spinner', () => {
    render(<LoadingState label="Loading problems…" />);

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Loading problems…');
    expect(status).toHaveAttribute('aria-live', 'polite');
  });
});
