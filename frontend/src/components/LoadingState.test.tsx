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

  it('says it is loading, from the catalogue, when the caller names nothing', () => {
    render(<LoadingState />);

    expect(screen.getByRole('status')).toHaveTextContent(ru.state.loading);
  });
});
