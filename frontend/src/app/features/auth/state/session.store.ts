import { Injectable, computed, signal } from '@angular/core';

const TOKEN_STORAGE_KEY = 'paretto.session.token';

/**
 * User data known to the frontend after an auth call.
 *
 * `id` is optional because the two endpoints that populate this store carry different information:
 * - `POST /api/auth/register` returns `{ id, username }` (Block 5).
 * - `POST /api/auth/login` returns `{ token, expiresAt }` only (Block 6) — no user id/username in
 *   the response body. `AuthService.login()` fills `username` from the request it just sent, but
 *   has no `id` to offer. A future "who am I" endpoint (out of scope for this block) would be the
 *   right place to backfill `id` after login.
 */
export interface SessionUser {
  id?: string;
  username: string;
  /**
   * `role` is optional for the same reason `id` is: the two endpoints that populate this store
   * carry different information.
   * - `POST /api/auth/register` returns `{ id, username }` (Block 5) — no `role`.
   * - `POST /api/auth/login` returns `{ token, expiresAt, role }` (Block 6, `role` added in
   *   FEAT-001c Block 1) — `AuthService.login()` propagates it as-is from the response.
   */
  role?: string;
}

/**
 * Session state for the `auth` feature: the opaque session token issued by `POST /api/auth/login`
 * (Block 6, server-side session — not a JWT, see docs/adr and the threat model) and the user it
 * belongs to.
 *
 * The token is persisted in `sessionStorage` (survives a page refresh, lost when the tab closes) —
 * decision documented in the spec (Block 8) as part of risk R5 (accepted risk, mitigated with CSP).
 */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly tokenSignal = signal<string | null>(this.readStoredToken());
  private readonly userSignal = signal<SessionUser | null>(null);

  readonly token = this.tokenSignal.asReadonly();
  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.tokenSignal() !== null);

  /** Called after a successful login: the account now has a valid session token. */
  setSession(token: string, user: SessionUser): void {
    this.tokenSignal.set(token);
    this.userSignal.set(user);
    this.persistToken(token);
  }

  /**
   * Called after a successful registration: the account exists, but registering does not issue a
   * session (the user still needs to log in) — so only `user` is recorded, `token` stays untouched.
   */
  setUser(user: SessionUser): void {
    this.userSignal.set(user);
  }

  /** Called on logout, or when the auth interceptor sees a 401 from the API. */
  clearSession(): void {
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    this.removePersistedToken();
  }

  private readStoredToken(): string | null {
    try {
      return sessionStorage.getItem(TOKEN_STORAGE_KEY);
    } catch {
      // sessionStorage can be unavailable (private browsing, disabled storage) — fall back to an
      // in-memory-only session for this tab instead of throwing during store construction.
      return null;
    }
  }

  private persistToken(token: string): void {
    try {
      sessionStorage.setItem(TOKEN_STORAGE_KEY, token);
    } catch {
      // See readStoredToken — the session still works for the lifetime of this tab.
    }
  }

  private removePersistedToken(): void {
    try {
      sessionStorage.removeItem(TOKEN_STORAGE_KEY);
    } catch {
      // See readStoredToken.
    }
  }
}
