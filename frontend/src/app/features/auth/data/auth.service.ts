import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import {
  AuthClient,
  LoginCommand,
  RegisterUserCommand,
  RegisterUserResponse,
} from '../../../core/api-client/api-client.generated';
import { toApiError } from '../../../core/http/api-error';
import { SessionStore } from '../state/session.store';

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

/**
 * Wraps `AuthClient` (NSwag-generated, see api-client.generated.ts) for the `auth` feature.
 * Components never call `AuthClient`/`HttpClient` directly — only this service, per AGENTS.md.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authClient = inject(AuthClient);
  private readonly sessionStore = inject(SessionStore);

  /**
   * Creates the account. Does NOT log the user in — `POST /api/auth/register` does not issue a
   * session (Block 5/6 of the spec are separate steps by design). On success, the created user's
   * data is recorded in `session.store` for convenience (e.g. pre-filling the login form), but
   * `isAuthenticated` stays `false` until an explicit `login()`.
   */
  register(request: RegisterRequest): Observable<RegisterUserResponse> {
    const command = new RegisterUserCommand({ ...request });
    return this.authClient.register(command).pipe(
      map((response) => {
        this.sessionStore.setUser({
          id: response.id,
          username: response.username ?? request.username,
        });
        return response;
      }),
      catchError((error: unknown) => throwError(() => toApiError(error))),
    );
  }

  /** Authenticates and, on success, stores the issued session token in `session.store`. */
  login(request: LoginRequest): Observable<void> {
    const command = new LoginCommand({ ...request });
    return this.authClient.login(command).pipe(
      map((response) => {
        if (!response.token) {
          throw toApiError(new Error('The server response did not include a session token.'));
        }
        this.sessionStore.setSession(response.token, { username: request.username });
      }),
      catchError((error: unknown) => throwError(() => toApiError(error))),
    );
  }

  /**
   * Invalidates the current session server-side and clears `session.store` locally either way —
   * even if the server call fails (e.g. the session already expired), there is no reason to keep
   * the client "logged in" for a token the server no longer recognizes.
   */
  logout(): Observable<void> {
    return this.authClient.logout().pipe(
      map(() => {
        this.sessionStore.clearSession();
      }),
      catchError((error: unknown) => {
        this.sessionStore.clearSession();
        return throwError(() => toApiError(error));
      }),
    );
  }
}
