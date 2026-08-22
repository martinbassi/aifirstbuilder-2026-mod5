import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthClient } from './core/api-client/api-client.generated';
import { SessionStore } from './features/auth/state/session.store';
import { adminGuard, authGuard, routes } from './app.routes';

describe('authGuard', () => {
  let sessionStore: SessionStore;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SessionStore],
    });

    sessionStore = TestBed.inject(SessionStore);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  function runGuard() {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url: '/protected' } as never),
    );
  }

  // Required test 5: authGuard redirige a /login sin sesión, permite el acceso con sesión.
  it('redirige a /login cuando no hay sesión activa', () => {
    const result = runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
  });

  it('permite el acceso cuando hay una sesión activa', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });

    const result = runGuard();

    expect(result).toBe(true);
  });
});

describe('adminGuard', () => {
  let sessionStore: SessionStore;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SessionStore],
    });

    sessionStore = TestBed.inject(SessionStore);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  function runGuard() {
    return TestBed.runInInjectionContext(() =>
      adminGuard({} as never, { url: '/moderation' } as never),
    );
  }

  // Required test: adminGuard permite el acceso con role: 'Administrator'.
  it('permite el acceso cuando el rol es Administrator', () => {
    sessionStore.setSession('token-abc', { username: 'admin', role: 'Administrator' });

    const result = runGuard();

    expect(result).toBe(true);
  });

  // Required test: adminGuard redirige cuando role es 'Standard'.
  it('redirige a / cuando el rol es Standard', () => {
    sessionStore.setSession('token-abc', { username: 'ana', role: 'Standard' });

    const result = runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/');
  });

  // Required test: adminGuard redirige cuando no hay sesión.
  it('redirige a /login cuando no hay sesión activa', () => {
    const result = runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
  });
});

// Required test (Block 8): navegar a /murals/new sin sesión redirige a /login — AC-06.
describe('routes — /murals/new (protected)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      // provideHttpClient: the redirect target (/login) lazy-loads the real LoginFormComponent,
      // which injects AuthService -> AuthClient (needs an HttpClient to construct, even though no
      // request is actually made in this test).
      providers: [SessionStore, provideRouter(routes), provideHttpClient(), AuthClient],
    });
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('redirige a /login al navegar a /murals/new sin sesión activa', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/murals/new');

    const router = TestBed.inject(Router);
    expect(router.url).toBe('/login');
  });
});
