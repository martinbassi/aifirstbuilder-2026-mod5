import { inject } from '@angular/core';
import { CanActivateFn, Routes } from '@angular/router';
import { Router } from '@angular/router';
import { SessionStore } from './features/auth/state/session.store';

/**
 * Reusable route guard for any route that requires an active session — meant to be reused by the
 * protected routes future sub-tickets (FEAT-001b/c) will add, not just the two below.
 */
export const authGuard: CanActivateFn = () => {
  const sessionStore = inject(SessionStore);
  const router = inject(Router);

  if (sessionStore.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login']);
};

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/ui/login-form.component').then((m) => m.LoginFormComponent),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/ui/register-form.component').then((m) => m.RegisterFormComponent),
  },
  {
    path: 'murals/new',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/murals/ui/create-mural-form.component').then(
        (m) => m.CreateMuralFormComponent,
      ),
  },
];
