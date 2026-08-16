import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  ApiException,
  AuthClient,
  LoginResponse,
  RegisterUserResponse,
} from '../../../core/api-client/api-client.generated';
import { SessionStore } from '../state/session.store';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let authClient: {
    register: ReturnType<typeof vi.fn>;
    login: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };
  let service: AuthService;
  let sessionStore: SessionStore;

  beforeEach(() => {
    authClient = {
      register: vi.fn(),
      login: vi.fn(),
      logout: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [AuthService, SessionStore, { provide: AuthClient, useValue: authClient }],
    });

    service = TestBed.inject(AuthService);
    sessionStore = TestBed.inject(SessionStore);
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  // Required test 1: register()/login() exitosos actualizan session.store correctamente.
  it('register() exitoso registra al usuario creado en session.store sin autenticarlo todavía', () => {
    const response = new RegisterUserResponse({ id: 'user-1', username: 'ana' });
    authClient.register.mockReturnValue(of(response));

    service
      .register({ username: 'ana', email: 'ana@example.com', password: 'Passw0rd' })
      .subscribe();

    expect(sessionStore.user()).toEqual({ id: 'user-1', username: 'ana' });
    expect(sessionStore.isAuthenticated()).toBe(false);
  });

  it('login() exitoso actualiza el token y el usuario en session.store, y queda autenticado', () => {
    const response = new LoginResponse({
      token: 'raw-token-123',
      expiresAt: new Date('2026-08-30T00:00:00Z'),
    });
    authClient.login.mockReturnValue(of(response));

    service.login({ username: 'ana', password: 'Passw0rd' }).subscribe();

    expect(sessionStore.token()).toBe('raw-token-123');
    expect(sessionStore.user()).toEqual({ username: 'ana' });
    expect(sessionStore.isAuthenticated()).toBe(true);
  });

  // Required test 2: un error del cliente generado se propaga como ApiError, no se swallowea.
  it('propaga un error del cliente generado como ApiError en vez de swallowearlo', () => {
    const apiException = new ApiException(
      'Bad Request',
      400,
      JSON.stringify({ title: 'Username or email is already in use.' }),
      {},
      null,
    );
    authClient.register.mockReturnValue(throwError(() => apiException));

    let receivedError: unknown;
    let completed = false;

    service
      .register({ username: 'ana', email: 'ana@example.com', password: 'Passw0rd' })
      .subscribe({
        next: () => {
          completed = true;
        },
        error: (error) => {
          receivedError = error;
        },
      });

    expect(completed).toBe(false);
    expect(receivedError).toEqual({ status: 400, message: 'Username or email is already in use.' });
  });

  it('propaga un error de red (sin ApiException) como ApiError genérico, no lo swallowea', () => {
    authClient.login.mockReturnValue(throwError(() => new Error('network down')));

    let receivedError: unknown;

    service.login({ username: 'ana', password: 'Passw0rd' }).subscribe({
      error: (error) => {
        receivedError = error;
      },
    });

    expect(receivedError).toBeDefined();
    expect((receivedError as { status: number }).status).toBe(0);
  });
});
