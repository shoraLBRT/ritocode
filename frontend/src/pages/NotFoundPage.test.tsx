import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { I18nProvider, ru } from '../i18n';
import { NotFoundPage } from './NotFoundPage';

function show(props: { title?: string; text?: string } = {}) {
  render(
    <I18nProvider>
      <MemoryRouter>
        <NotFoundPage {...props} />
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe('NotFoundPage', () => {
  it('says what happened and offers three ways out, home first', () => {
    show();

    expect(screen.getByRole('heading', { level: 1, name: ru.notFound.title })).toBeInTheDocument();
    const links = within(screen.getByRole('navigation', { name: ru.notFound.ways })).getAllByRole('link');
    expect(links.map((link) => link.getAttribute('href'))).toEqual(['/', '/tasks', '/problems']);
    expect(links[0]).toHaveClass('rc-notfound__way--primary');
  });

  it('takes its own words, for a page that reuses the frame', () => {
    show({ title: 'Task is gone', text: 'Nobody knows that task.' });

    expect(screen.getByRole('heading', { level: 1, name: 'Task is gone' })).toBeInTheDocument();
    expect(screen.getByText('Nobody knows that task.')).toBeInTheDocument();
  });
});
