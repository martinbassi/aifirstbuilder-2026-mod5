import { inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../features/auth/data/auth.service';
import { SessionStore } from '../../features/auth/state/session.store';

/**
 * Repopulates `session.store.user()` from `GET /api/auth/session` before the router resolves any
 * route — meant to be registered via `provideAppInitializer` (see `app.config.ts`). The session
 * token survives a page refresh in `sessionStorage`, but the user/role signal starts `null` in
 * memory; without this, `adminGuard` and the sidebar see `null` right after an F5 even though the
 * token is still valid (NFR-04, AC-07).
 *
 * With no stored token, resolves immediately without any network call — nothing to rehydrate.
 * With a token, awaits `AuthService.getCurrentSession()` but NEVER rejects: the auth interceptor
 * already clears the session and redirects to `/login` on a 401 (AC-08), and any other failure
 * (e.g. a network hiccup) must not block the app from booting.
 */
export function rehydrateSessionOnStartup(): Promise<void> {
  const sessionStore = inject(SessionStore);
  const authService = inject(AuthService);

  if (sessionStore.token() === null) {
    return Promise.resolve();
  }

  return firstValueFrom(authService.getCurrentSession()).then(
    () => undefined,
    () => undefined, // el interceptor ya limpia la sesión y redirige ante un 401 (AC-08);
    // cualquier otro error no debe bloquear el arranque de la app.
  );
}
