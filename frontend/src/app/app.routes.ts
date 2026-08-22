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

/**
 * Admin-only route guard (FEAT-001c Block 6). Meant to run AFTER `authGuard` in a route's
 * `canActivate` array — the unauthenticated case is normally already handled by `authGuard` by the
 * time this one runs — but it is implemented defensively on its own too: no session redirects to
 * `/login`, same as `authGuard`. A session without the `Administrator` role redirects to `/`
 * (no dedicated "access denied" screen, out of scope per the PRD).
 *
 * Security note (see threat-FEAT-001c.md): this is UX-only. The `role` it reads comes from
 * `SessionStore`, client-asserted and tamperable via devtools. The real authorization for the
 * moderation endpoints is `[Authorize(Roles = "Administrator")]` server-side, re-verified on every
 * request — never derived from anything the client sends.
 */
export const adminGuard: CanActivateFn = () => {
  const sessionStore = inject(SessionStore);
  const router = inject(Router);

  if (!sessionStore.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  if (sessionStore.user()?.role !== 'Administrator') {
    return router.createUrlTree(['/']);
  }

  return true;
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
  {
    path: 'moderation',
    canActivate: [authGuard, adminGuard],
    loadComponent: () =>
      import('./features/moderation/ui/pending-murals-list.component').then(
        (m) => m.PendingMuralsListComponent,
      ),
  },
];
