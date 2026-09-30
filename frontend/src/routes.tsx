import type { RouteObject } from 'react-router';
import { AppLayout } from './components/AppLayout';
import { HomePage } from './pages/HomePage';
import { NotFoundPage } from './pages/NotFoundPage';
import { ProblemsPage } from './pages/ProblemsPage';
import { TasksPage } from './pages/TasksPage';

/**
 * The route table, as data.
 *
 * Kept out of the component tree so a test can mount one route with a memory router and no
 * browser history. A page that needs a signed-in learner goes under a `RequireSignIn` layout
 * route (`src/session`), which renders its children only for one: the progress page (#30) and
 * the review of an attempt (#29) are the first.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    Component: AppLayout,
    children: [
      { index: true, Component: HomePage },
      { path: 'tasks', Component: TasksPage },
      { path: 'problems', Component: ProblemsPage },
      { path: '*', Component: NotFoundPage },
    ],
  },
];
