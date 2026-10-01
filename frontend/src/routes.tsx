import { Navigate } from 'react-router';
import type { RouteObject } from 'react-router';
import { AppLayout } from './components/AppLayout';
import { AdminAttemptsPage } from './pages/admin/AdminAttemptsPage';
import { AdminSignalsPage } from './pages/admin/AdminSignalsPage';
import { AdminUsersPage } from './pages/admin/AdminUsersPage';
import { RequireAdmin } from './pages/admin/RequireAdmin';
import { HomePage } from './pages/HomePage';
import { NotFoundPage } from './pages/NotFoundPage';
import { ProblemsPage } from './pages/ProblemsPage';
import { ProgressPage } from './pages/ProgressPage';
import { TasksPage } from './pages/TasksPage';
import { ReviewPage } from './pages/task/ReviewPage';
import { TaskPage } from './pages/task/TaskPage';
import { RequireSignIn } from './session';

/**
 * The route table, as data.
 *
 * Kept out of the component tree so a test can mount one route with a memory router and no
 * browser history. A page that needs a signed-in learner goes under a `RequireSignIn` layout
 * route (`src/session`), which renders its children only for one: the review of an attempt and
 * the progress page. The admin area goes under `RequireAdmin`, which shows anyone but an admin the
 * page of an unknown address.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    Component: AppLayout,
    children: [
      { index: true, Component: HomePage },
      { path: 'tasks', Component: TasksPage },
      { path: 'tasks/:slug', Component: TaskPage },
      {
        Component: RequireSignIn,
        children: [
          { path: 'tasks/:slug/attempts/:id', Component: ReviewPage },
          { path: 'progress', Component: ProgressPage },
        ],
      },
      {
        path: 'admin',
        Component: RequireAdmin,
        children: [
          { index: true, element: <Navigate to="/admin/signals" replace /> },
          { path: 'signals', Component: AdminSignalsPage },
          { path: 'users', Component: AdminUsersPage },
          { path: 'attempts', Component: AdminAttemptsPage },
        ],
      },
      { path: 'problems', Component: ProblemsPage },
      { path: '*', Component: NotFoundPage },
    ],
  },
];
