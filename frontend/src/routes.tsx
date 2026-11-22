import { Navigate } from 'react-router';
import type { RouteObject } from 'react-router';
import { AppLayout } from './components/AppLayout';
import { RouteErrorPage } from './errors/RouteErrorPage';
import { RequireAdmin } from './pages/admin/RequireAdmin';
import { HomePage } from './pages/HomePage';
import { NotFoundPage } from './pages/NotFoundPage';
import { PrivacyPage } from './pages/PrivacyPage';
import { ProblemPage } from './pages/ProblemPage';
import { ProblemsPage } from './pages/ProblemsPage';
import { TasksPage } from './pages/TasksPage';
import { RequireSignIn } from './session';

/**
 * The route table, as data.
 *
 * Kept out of the component tree so a test can mount one route with a memory router and no
 * browser history. A page that needs a signed-in learner goes under a `RequireSignIn` layout
 * route (`src/session`), which renders its children only for one: the review of an attempt and
 * the profile. The admin area goes under `RequireAdmin`, which shows anyone but an admin the
 * page of an unknown address.
 *
 * The public pages are in the bundle every page loads; the task screen, the review, the profile and
 * the admin area are each a chunk of their own, loaded when their route is first matched (#310), so
 * a prerendered page loads none of them.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    Component: AppLayout,
    // A page whose render throws shows the error page with its report's number (#294).
    ErrorBoundary: RouteErrorPage,
    children: [
      { index: true, Component: HomePage },
      { path: 'tasks', Component: TasksPage },
      { path: 'tasks/:slug', lazy: () => import('./pages/task/TaskPage').then((page) => ({ Component: page.TaskPage })) },
      {
        Component: RequireSignIn,
        children: [
          { path: 'tasks/:slug/attempts/:id', lazy: () => import('./pages/task/ReviewPage').then((page) => ({ Component: page.ReviewPage })) },
          { path: 'profile', lazy: () => import('./pages/profile/ProfilePage').then((page) => ({ Component: page.ProfilePage })) },
        ],
      },
      // The progress page became the profile (#261); an old link still lands there.
      { path: 'progress', element: <Navigate to="/profile" replace /> },
      {
        path: 'admin',
        Component: RequireAdmin,
        children: [
          { index: true, lazy: () => import('./pages/admin/AdminOverviewPage').then((page) => ({ Component: page.AdminOverviewPage })) },
          { path: 'signals', lazy: () => import('./pages/admin/AdminSignalsPage').then((page) => ({ Component: page.AdminSignalsPage })) },
          { path: 'users', lazy: () => import('./pages/admin/AdminUsersPage').then((page) => ({ Component: page.AdminUsersPage })) },
          { path: 'attempts', lazy: () => import('./pages/admin/AdminAttemptsPage').then((page) => ({ Component: page.AdminAttemptsPage })) },
          { path: 'stats', lazy: () => import('./pages/admin/AdminStatsPage').then((page) => ({ Component: page.AdminStatsPage })) },
          { path: 'system', lazy: () => import('./pages/admin/AdminSystemPage').then((page) => ({ Component: page.AdminSystemPage })) },
          { path: 'errors', lazy: () => import('./pages/admin/AdminErrorsPage').then((page) => ({ Component: page.AdminErrorsPage })) },
        ],
      },
      { path: 'problems', Component: ProblemsPage },
      { path: 'problems/:slug', Component: ProblemPage },
      { path: 'privacy', Component: PrivacyPage },
      { path: '*', Component: NotFoundPage },
    ],
  },
];
