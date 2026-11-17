import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import type { TaskSummary } from '../api';
import { ru } from '../i18n';
import { renderApp } from '../test/render';
import { problemResponse } from '../test/responses';
import { TaskTable } from './TaskTable';

const tasks: TaskSummary[] = [
  { slug: 'orders', title: 'Выгрузка заказов', difficulty: 'easy', status: 'solved' },
  { slug: 'webhook', title: 'Пополнение баланса', difficulty: 'medium', status: 'new' },
];

describe('TaskTable', () => {
  it('has one link per row, named by the task, and the status still reads', () => {
    renderApp(vi.fn<typeof globalThis.fetch>().mockResolvedValue(problemResponse(401, 'unauthenticated', 'x')), '/', [{ path: '/', element: <TaskTable tasks={tasks} /> }]);

    const rows = screen.getAllByRole('row').slice(1);
    expect(rows).toHaveLength(tasks.length);
    for (const task of tasks) {
      const link = screen.getByRole('link', { name: task.title });
      expect(link).toHaveAttribute('href', `/tasks/${task.slug}`);
    }
    expect(screen.getAllByRole('link')).toHaveLength(tasks.length);
    expect(screen.getByRole('img', { name: ru.taskTable.statuses.solved })).toBeInTheDocument();
  });
});
