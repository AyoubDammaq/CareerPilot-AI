import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { NAV_ITEMS } from './core/nav';
import { Shell } from './layout/shell';

const placeholders: Routes = NAV_ITEMS.filter((n) => n.phase !== null).map((n) => ({
  path: n.path,
  title: `${n.label} | CareerPilot AI`,
  loadComponent: () => import('./features/placeholder/placeholder.page').then((m) => m.PlaceholderPage),
}));

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in | CareerPilot AI',
    loadComponent: () => import('./features/login/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard | CareerPilot AI',
        loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'profile',
        title: 'Career Profile | CareerPilot AI',
        loadComponent: () => import('./features/profile/profile.page').then((m) => m.ProfilePage),
        canDeactivate: [(c: { canDeactivate(): boolean }) => c.canDeactivate()],
      },
      {
        path: 'job-search',
        title: 'Job Search | CareerPilot AI',
        loadComponent: () => import('./features/job-search/job-search.page').then((m) => m.JobSearchPage),
      },
      {
        path: 'opportunities',
        title: 'Job Opportunities | CareerPilot AI',
        loadComponent: () => import('./features/opportunities/opportunities.page').then((m) => m.OpportunitiesPage),
      },
      {
        path: 'application-review',
        title: 'Application Review | CareerPilot AI',
        loadComponent: () => import('./features/application-review/application-review.page').then((m) => m.ApplicationReviewPage),
      },
      ...placeholders,
    ],
  },
  { path: '**', redirectTo: '' },
];
