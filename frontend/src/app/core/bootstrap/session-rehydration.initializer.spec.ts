import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../features/auth/data/auth.service';
import { SessionStore } from '../../features/auth/state/session.store';
import { rehydrateSessionOnStartup } from './session-rehydration.initializer';

describe('rehydrateSessionOnStartup', () => {
  let authService: { getCurrentSession: ReturnType<typeof vi.fn> };
  let sessionStore: { token: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    authService = { getCurrentSession: vi.fn() };
  });

  function configure(): void {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: SessionStore, useValue: sessionStore },
      ],
    });
  }

  // Required test 3 (Block 3): sin token, no hay nada que rehidratar.
  it('sin token, no invoca getCurrentSession() y la promesa resuelve', async () => {
    sessionStore = { token: vi.fn().mockReturnValue(null) };
    configure();

    await expect(
      TestBed.runInInjectionContext(() => rehydrateSessionOnStartup()),
    ).resolves.toBeUndefined();
    expect(authService.getCurrentSession).not.toHaveBeenCalled();
  });

  // Required test 4 (Block 3): con token, éxito — valida AC-07.
  it('con token, getCurrentSession() exitoso resuelve la promesa del initializer', async () => {
    sessionStore = { token: vi.fn().mockReturnValue('raw-token') };
    authService.getCurrentSession.mockReturnValue(of({ username: 'ana', role: 'Standard' }));
    configure();

    await expect(
      TestBed.runInInjectionContext(() => rehydrateSessionOnStartup()),
    ).resolves.toBeUndefined();
    expect(authService.getCurrentSession).toHaveBeenCalled();
  });

  // Required test 5 (Block 3): con token, fallo — la promesa IGUAL resuelve, valida AC-08.
  it('con token, getCurrentSession() falla y la promesa del initializer IGUAL resuelve', async () => {
    sessionStore = { token: vi.fn().mockReturnValue('raw-token') };
    authService.getCurrentSession.mockReturnValue(
      throwError(() => ({ status: 401, message: 'Session expired.' })),
    );
    configure();

    await expect(
      TestBed.runInInjectionContext(() => rehydrateSessionOnStartup()),
    ).resolves.toBeUndefined();
  });
});
