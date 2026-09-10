import type { RouteObject } from 'react-router';
import { AppLayout } from './components/AppLayout';
import { HomePage } from './pages/HomePage';
import { NotFoundPage } from './pages/NotFoundPage';
import { ProblemDetailPage } from './pages/ProblemDetailPage';
import { ProblemsPage } from './pages/ProblemsPage';

/**
 * The route table, as data.
 *
 * Kept out of the component tree so a test can mount one route with a memory router and no
 * browser history, and so the place a protected route will be introduced — a wrapper element
 * around the routes that need one, once [#6](https://github.com/shoraLBRT/ritocode/issues/6)
 * gives the client an identity to check — is a single visible edit rather than a search.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    Component: AppLayout,
    children: [
      { index: true, Component: HomePage },
      { path: 'problems', Component: ProblemsPage },
      { path: 'problems/:slug', Component: ProblemDetailPage },
      { path: '*', Component: NotFoundPage },
    ],
  },
];
