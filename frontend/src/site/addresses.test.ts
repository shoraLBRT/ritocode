import { describe, expect, it } from 'vitest';
import type { RouteObject } from 'react-router';
import { routes } from '../routes';
import { PRERENDERED_PATHS, SHELL_PATHS } from './pageMeta';

/** Every full path of the route table, `/`-rooted, with index routes as their parent's path. */
function paths(table: RouteObject[], parent = ''): string[] {
  return table.flatMap((route) => {
    const own = route.path === undefined ? parent : `${parent.replace(/\/$/, '')}/${route.path.replace(/^\//, '')}`;
    const here = route.index === true || (route.path !== undefined && route.children === undefined) ? [own || '/'] : [];
    return [...here, ...paths(route.children ?? [], own)];
  });
}

/**
 * The web image answers 404 for an address it has no file for (#300), and the build writes a file
 * for each fixed address it is told of: a route added to the table but not to these lists would be
 * a 404 in production while it works in development.
 */
describe('the addresses the build writes', () => {
  const table = paths(routes);

  it('cover every fixed route of the application', () => {
    const fixed = table.filter((path) => !path.includes(':') && path !== '/*');
    const written = new Set<string>([...PRERENDERED_PATHS, ...SHELL_PATHS, '/progress']);

    expect(fixed.filter((path) => !written.has(path))).toEqual([]);
    expect([...written].filter((path) => !fixed.includes(path))).toEqual([]);
  });

  it('leave only the task addresses to the content export', () => {
    expect(table.filter((path) => path.includes(':'))).toEqual(['/tasks/:slug', '/tasks/:slug/attempts/:id']);
  });
});
