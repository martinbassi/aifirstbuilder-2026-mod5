import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { SessionStore } from './features/auth/state/session.store';
import { authGuard } from './app.routes';

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
