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
      ...placeholders,
    ],
  },
  { path: '**', redirectTo: '' },
];
