import { Routes } from '@angular/router';
import { roleGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'claims' },
  {
    path: 'login',
    title: 'Sign in - ClaimDesk',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'claims',
    title: 'Claims - ClaimDesk',
    canActivate: [roleGuard()],
    loadComponent: () => import('./features/claims/claim-list.component').then((m) => m.ClaimListComponent),
  },
  {
    path: 'claims/new',
    title: 'File a claim - ClaimDesk',
    canActivate: [roleGuard('Claimant')],
    loadComponent: () => import('./features/claims/claim-form.component').then((m) => m.ClaimFormComponent),
  },
  {
    path: 'claims/:id',
    title: 'Claim - ClaimDesk',
    canActivate: [roleGuard()],
    loadComponent: () => import('./features/claims/claim-detail.component').then((m) => m.ClaimDetailComponent),
  },
  {
    path: 'queue',
    title: 'Review queue - ClaimDesk',
    canActivate: [roleGuard('Adjuster', 'Admin')],
    loadComponent: () => import('./features/queue/queue.component').then((m) => m.QueueComponent),
  },
  {
    path: 'alerts',
    title: 'SLA alerts - ClaimDesk',
    canActivate: [roleGuard('Adjuster', 'Admin')],
    loadComponent: () =>
      import('./features/notifications/notifications.component').then((m) => m.NotificationsComponent),
  },
  {
    path: 'admin/users',
    title: 'Users - ClaimDesk',
    canActivate: [roleGuard('Admin')],
    loadComponent: () => import('./features/admin/users.component').then((m) => m.UsersComponent),
  },
  { path: '**', redirectTo: 'claims' },
];
