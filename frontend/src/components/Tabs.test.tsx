import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { Tabs } from './TaskTable';

describe('Tabs', () => {
  it('exposes the pressed option with aria-pressed and reports a choice', () => {
    const choose = vi.fn();
    render(
      <Tabs
        label="Сложность"
        value="b"
        onChange={choose}
        options={[
          { value: 'a', label: 'Первая', count: 3 },
          { value: 'b', label: 'Вторая' },
        ]}
      />,
    );

    expect(screen.getByRole('button', { name: /Первая/ })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: /Вторая/ })).toHaveAttribute('aria-pressed', 'true');
    // Without layout (jsdom) there is no thumb: the pressed tab keeps its own background.
    expect(document.querySelector('.rc-tabs__thumb')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: /Первая/ }));
    expect(choose).toHaveBeenCalledWith('a');
  });

  it('places a thumb on the pressed tab when the browser can measure it', () => {
    const width = vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockReturnValue(80);
    const height = vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockReturnValue(32);
    try {
      render(
        <Tabs
          label="Статус"
          value="a"
          onChange={() => undefined}
          options={[
            { value: 'a', label: 'Первая' },
            { value: 'b', label: 'Вторая' },
          ]}
        />,
      );

      const thumb = document.querySelector<HTMLElement>('.rc-tabs__thumb');
      expect(thumb).not.toBeNull();
      expect(thumb?.style.width).toBe('80px');
      expect(screen.getByRole('button', { name: /Первая/ })).toHaveAttribute('aria-pressed', 'true');
    } finally {
      width.mockRestore();
      height.mockRestore();
    }
  });
});
