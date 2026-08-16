import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../api-client/api-client.generated';
import { SessionStore } from '../../features/auth/state/session.store';

/**
 * Attaches `Authorization: Bearer {token}` to every outgoing request towards our own API
 * (`API_BASE_URL`) when there is an active session, and reacts to a `401` from the API by
 * clearing the session and sending the user to `/login` — the server-side session (opaque token,
 * Block 6) is the single source of truth for whether the user is still authenticated, not just
 * the presence of a token client-side.
 *
 * The header is never attached to requests towards a different origin (CDN, analytics, etc.) —
 * a future call to a third party must not leak the session token. The comparison uses the real
 * parsed `origin` (protocol + host + port), never a string prefix: a `startsWith` check would let
 * an adversarial origin that merely begins with the same text (e.g.
 * `https://localhost:71260.evil.com`, when `API_BASE_URL` is `https://localhost:7126`) through.
 * If `req.url` is not a parseable URL, we fail closed — treat it as NOT our own API rather than
 * throw or risk a false positive.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const sessionStore = inject(SessionStore);
  const router = inject(Router);
  const apiBaseUrl = inject(API_BASE_URL, { optional: true });

  const token = sessionStore.token();
  const isOwnApiRequest = isSameOrigin(req.url, apiBaseUrl);
  const authorizedRequest =
    token && isOwnApiRequest
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        sessionStore.clearSession();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
  );
};

/**
 * True only when `url` resolves to the exact same origin as `baseUrl` — never a string-prefix
 * match. Returns `false` (fail closed) if either value is missing or is not a parseable URL.
 */
function isSameOrigin(url: string, baseUrl: string | null | undefined): boolean {
  if (!baseUrl) {
    return false;
  }

  try {
    return new URL(url, baseUrl).origin === new URL(baseUrl).origin;
  } catch {
    return false;
  }
}
