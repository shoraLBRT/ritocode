import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ru } from '../i18n';
import { LoadingState } from './LoadingState';

describe('LoadingState', () => {
  it('announces itself to a screen reader rather than showing a silent spinner', () => {
    render(<LoadingState label="Loading problems…" />);

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Loading problems…');
    expect(status).toHaveAttribute('aria-live', 'polite');
  });

  it('keeps the words for a screen reader when a skeleton is what shows', () => {
    render(<LoadingState label="Loading problems…" skeleton={<span data-testid="blocks" />} />);

    const status = screen.getByRole('status');
    expect(status).toHaveTextContent('Loading problems…');
    expect(status).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByText('Loading problems…')).toHaveClass('visually-hidden');
    expect(screen.getByTestId('blocks')).toBeInTheDocument();
  });

  it('says it is loading, from the catalogue, when the caller names nothing', () => {
    render(<LoadingState />);

    expect(screen.getByRole('status')).toHaveTextContent(ru.state.loading);
  });
});
