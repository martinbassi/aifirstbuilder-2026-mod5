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

/**
 * Root route guard (FEAT-001d Block 8, AC-08/AC-09). The root `/` is a presentation decision, not
 * an authorization one — no new authorization rule is introduced here, `authGuard`/`adminGuard`
 * above are untouched. Implemented as a `CanActivateFn`, same shape as `authGuard`/`adminGuard`,
 * instead of a resolver or a placeholder component: it is the pattern already established in this
 * file for "read a `SessionStore` signal and redirect via `router.createUrlTree`", and it keeps `/`
 * out of the bundle as an actual routed component (there is nothing to render — it always redirects
 * before any component activates).
 */
export const rootRedirectGuard: CanActivateFn = () => {
  const sessionStore = inject(SessionStore);
  const router = inject(Router);

  return router.createUrlTree([sessionStore.isAuthenticated() ? '/discover' : '/login']);
};

export const routes: Routes = [
  {
    // No component: `rootRedirectGuard` always returns a `UrlTree`, so nothing here ever
    // activates/renders — it only exists to run the guard and redirect (AC-08/AC-09).
    path: '',
    pathMatch: 'full',
    canActivate: [rootRedirectGuard],
    // Required by Angular's route validator (NG04014): a route needs one of
    // component/loadComponent/redirectTo/children/loadChildren. This route never renders
    // anything (the guard always returns a UrlTree), so `children: []` is the correct minimal
    // choice — not redundant, despite looking like it.
    children: [],
  },
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
  // Placed AFTER the root `path: ''` redirect entry above, deliberately — not because Angular
  // Router requires this order to work (the impact scan for spec Block 5 confirmed Angular Router
  // 21 backtracks and would resolve either order correctly), but so this file does not depend on
  // that internal matcher detail: the redirect-only route (no component, always returns a UrlTree)
  // reads unambiguously as "resolved first" this way.
  {
    path: '',
    loadComponent: () =>
      import('./core/layout/ui/app-shell.component').then((m) => m.AppShellComponent),
    children: [
      {
        path: 'discover',
        // Public, deliberately without authGuard (AC-07, FR-07): the same component the root
        // redirects to when there IS a session, but also reachable directly without one.
        data: { title: 'Descubrir' },
        loadComponent: () =>
          import('./features/discovery/ui/discovery-page.component').then(
            (m) => m.DiscoveryPageComponent,
          ),
      },
      {
        path: 'murals/new',
        canActivate: [authGuard],
        data: { title: 'Cargar mural' },
        loadComponent: () =>
          import('./features/murals/ui/create-mural-form.component').then(
            (m) => m.CreateMuralFormComponent,
          ),
      },
      {
        path: 'moderation',
        canActivate: [authGuard, adminGuard],
        data: { title: 'Moderación' },
        loadComponent: () =>
          import('./features/moderation/ui/pending-murals-list.component').then(
            (m) => m.PendingMuralsListComponent,
          ),
      },
    ],
  },
];
