import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import {
  ApiException,
  AuthClient,
  LoginCommand,
  RegisterUserCommand,
  RegisterUserResponse,
} from '../../../core/api-client/api-client.generated';
import { SessionStore } from '../state/session.store';

/**
 * Typed error surfaced by this service to its callers (components). It is always the result of
 * translating whatever the generated client threw — network failures included — never swallowed.
 */
export interface ApiError {
  /** HTTP status code, or `0` for a request that never reached the server (network/CORS failure). */
  status: number;
  /** Message ready to show to the user as-is — already the backend's own generic message when one
   * exists (FR-02/FR-05: same text regardless of which field caused the failure). */
  message: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

const GENERIC_NETWORK_ERROR_MESSAGE = 'No se pudo conectar con el servidor. Intentá nuevamente.';
const GENERIC_UNEXPECTED_ERROR_MESSAGE = 'Ocurrió un error inesperado. Intentá nuevamente.';

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

function toApiError(error: unknown): ApiError {
  if (error instanceof ApiException) {
    return { status: error.status, message: extractMessage(error) };
  }
  if (isApiError(error)) {
    return error;
  }
  return { status: 0, message: GENERIC_NETWORK_ERROR_MESSAGE };
}

function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === 'object' &&
    error !== null &&
    'status' in error &&
    'message' in error &&
    typeof (error as ApiError).status === 'number' &&
    typeof (error as ApiError).message === 'string'
  );
}

function extractMessage(error: ApiException): string {
  try {
    const body = JSON.parse(error.response) as { title?: string };
    if (body?.title) {
      return body.title;
    }
  } catch {
    // error.response was not parseable JSON — fall through to the generic message below.
  }
  return GENERIC_UNEXPECTED_ERROR_MESSAGE;
}
