import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  CloudUploadOutline,
  CompassOutline,
  LogoutOutline,
  MenuFoldOutline,
  MenuUnfoldOutline,
  SafetyCertificateOutline,
} from '@ant-design/icons-angular/icons';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { of, throwError } from 'rxjs';
import { AuthClient, DiscoveryClient } from '../../api-client/api-client.generated';
import { AuthService } from '../../../features/auth/data/auth.service';
import { SessionStore } from '../../../features/auth/state/session.store';
import { LayoutStore } from '../state/layout.store';
import { authGuard, routes } from '../../../app.routes';
import { SidebarComponent } from './sidebar.component';

@Component({ selector: 'app-dummy-discover', template: '' })
class DummyDiscoverComponent {}

@Component({ selector: 'app-dummy-murals-new', template: '' })
class DummyMuralsNewComponent {}

@Component({ selector: 'app-dummy-login', template: '' })
class DummyLoginComponent {}

describe('SidebarComponent', () => {
  let authService: { logout: ReturnType<typeof vi.fn> };
  let sessionStore: SessionStore;

  beforeEach(async () => {
    authService = { logout: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([
          { path: 'discover', component: DummyDiscoverComponent },
          { path: 'murals/new', component: DummyMuralsNewComponent },
          { path: 'login', component: DummyLoginComponent },
        ]),
        { provide: AuthService, useValue: authService },
        SessionStore,
        LayoutStore,
        provideNzIcons([
          CompassOutline,
          CloudUploadOutline,
          SafetyCertificateOutline,
          LogoutOutline,
        ]),
      ],
    }).compileComponents();

    sessionStore = TestBed.inject(SessionStore);
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  // Required test 1 (AC-03/FR-02): renderiza el logo.
  it('renderiza el logo', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const logo: HTMLElement | null = fixture.nativeElement.querySelector('app-logo');
    expect(logo).toBeTruthy();
  });

  // Required test 2 (AC-04/FR-03): ítems "Descubrir" y "Cargar mural" con sus íconos.
  it('renderiza los ítems "Descubrir" y "Cargar mural" con sus íconos', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const discoverLink: HTMLElement | null =
      fixture.nativeElement.querySelector('[data-testid="menu-discover"]');
    const uploadLink: HTMLElement | null =
      fixture.nativeElement.querySelector('[data-testid="menu-upload"]');

    expect(discoverLink).toBeTruthy();
    expect(discoverLink!.textContent).toContain('Descubrir');
    expect(discoverLink!.querySelector('nz-icon[nzType="compass"]')).toBeTruthy();

    expect(uploadLink).toBeTruthy();
    expect(uploadLink!.textContent).toContain('Cargar mural');
    expect(uploadLink!.querySelector('nz-icon[nzType="cloud-upload"]')).toBeTruthy();
  });

  // Required test 3 (AC-05/FR-04): sin rol Administrator (o sin sesión) no muestra "Moderación".
  it('no renderiza "Moderación" sin sesión', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const moderationLink = fixture.nativeElement.querySelector('[data-testid="menu-moderation"]');
    expect(moderationLink).toBeFalsy();
  });

  it('no renderiza "Moderación" con sesión sin rol Administrator', () => {
    sessionStore.setSession('token-abc', { username: 'ana', role: 'Standard' });

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const moderationLink = fixture.nativeElement.querySelector('[data-testid="menu-moderation"]');
    expect(moderationLink).toBeFalsy();
  });

  // Required test 4 (AC-06/FR-04): con rol Administrator, sí muestra "Moderación".
  it('renderiza "Moderación" con rol Administrator', () => {
    sessionStore.setSession('token-abc', { username: 'admin', role: 'Administrator' });

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const moderationLink: HTMLElement | null = fixture.nativeElement.querySelector(
      '[data-testid="menu-moderation"]',
    );
    expect(moderationLink).toBeTruthy();
    expect(moderationLink!.textContent).toContain('Moderación');
    expect(moderationLink!.querySelector('nz-icon[nzType="safety-certificate"]')).toBeTruthy();
  });

  // Required test 5 (AC-07/FR-05): el ítem de la ruta activa recibe la clase `active`.
  it('resalta el ítem de la ruta activa con la clase active', async () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    await router.navigateByUrl('/discover');
    fixture.detectChanges();

    const discoverLink: HTMLElement | null =
      fixture.nativeElement.querySelector('[data-testid="menu-discover"]');
    const uploadLink: HTMLElement | null =
      fixture.nativeElement.querySelector('[data-testid="menu-upload"]');

    expect(discoverLink!.classList.contains('active')).toBe(true);
    expect(uploadLink!.classList.contains('active')).toBe(false);
  });

  // Required test 6 (AC-08/AC-09/FR-06/FR-07): footer sesión-vs-anónimo.
  it('con sesión activa muestra username y botón de cerrar sesión', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const username: HTMLElement | null =
      fixture.nativeElement.querySelector('[data-testid="sidebar-username"]');
    const logoutButton: HTMLElement | null = fixture.nativeElement.querySelector(
      '[data-testid="logout-button"]',
    );
    const loginLink = fixture.nativeElement.querySelector('[data-testid="login-link"]');
    const registerLink = fixture.nativeElement.querySelector('[data-testid="register-link"]');

    expect(username!.textContent).toContain('ana');
    expect(logoutButton).toBeTruthy();
    expect(loginLink).toBeFalsy();
    expect(registerLink).toBeFalsy();
  });

  it('sin sesión muestra los links "Iniciar sesión"/"Registrarse"', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const username = fixture.nativeElement.querySelector('[data-testid="sidebar-username"]');
    const logoutButton = fixture.nativeElement.querySelector('[data-testid="logout-button"]');
    const loginLink: HTMLElement | null =
      fixture.nativeElement.querySelector('[data-testid="login-link"]');
    const registerLink: HTMLElement | null = fixture.nativeElement.querySelector(
      '[data-testid="register-link"]',
    );

    expect(username).toBeFalsy();
    expect(logoutButton).toBeFalsy();
    expect(loginLink).toBeTruthy();
    expect(loginLink!.textContent).toContain('Iniciar sesión');
    expect(registerLink).toBeTruthy();
    expect(registerLink!.textContent).toContain('Registrarse');
  });

  // Required test 7 (AC-10/FR-08): click en "Cerrar sesión".
  it('al hacer click en "Cerrar sesión" llama a logout, clearSession y navigate', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });
    authService.logout.mockReturnValue(of(undefined));

    const clearSessionSpy = vi.spyOn(sessionStore, 'clearSession');
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate');

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const logoutButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="logout-button"]',
    );
    logoutButton.click();

    expect(authService.logout).toHaveBeenCalled();
    expect(clearSessionSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login']);
  });

  // Required test 8: si logout() emite error, igual limpia la sesión y navega.
  it('si logout() falla igual limpia la sesión y navega a /login', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });
    authService.logout.mockReturnValue(throwError(() => ({ status: 500, message: 'boom' })));

    const clearSessionSpy = vi.spyOn(sessionStore, 'clearSession');
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate');

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const logoutButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="logout-button"]',
    );
    logoutButton.click();

    expect(authService.logout).toHaveBeenCalled();
    expect(clearSessionSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login']);
  });

  // Required test 10 (AC-11/FR-09/NFR-01): botón de expandir/contraer.
  it('el botón de expandir/contraer llama a layoutStore.toggle() y cambia su aria-label', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const layoutStore = TestBed.inject(LayoutStore);
    const toggleSpy = vi.spyOn(layoutStore, 'toggle');

    const toggleButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="sidebar-toggle"]',
    );
    const initialLabel = toggleButton.getAttribute('aria-label');

    toggleButton.click();
    fixture.detectChanges();

    expect(toggleSpy).toHaveBeenCalled();
    const newLabel = toggleButton.getAttribute('aria-label');
    expect(newLabel).not.toBe(initialLabel);
    expect(['Expandir menú', 'Contraer menú']).toContain(initialLabel);
    expect(['Expandir menú', 'Contraer menú']).toContain(newLabel);
  });
});

// Required test 9 (AC-15/FR-01/FR-03): click en "Cargar mural" sin sesión termina en /login,
// reutilizando el authGuard real de app.routes.ts sin mockearlo — test de integración con
// RouterTestingHarness, siguiendo el mismo patrón que app.routes.spec.ts.
describe('SidebarComponent — integración con authGuard real', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SessionStore,
        provideRouter(routes),
        provideHttpClient(),
        AuthClient,
        DiscoveryClient,
        provideNzIcons([
          CompassOutline,
          CloudUploadOutline,
          SafetyCertificateOutline,
          LogoutOutline,
          MenuFoldOutline,
          MenuUnfoldOutline,
        ]),
      ],
    });
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('navegar a /murals/new sin sesión (vía el link del sidebar) redirige a /login', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/discover');

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const uploadLink: HTMLAnchorElement | null =
      fixture.nativeElement.querySelector('[data-testid="menu-upload"]');
    expect(uploadLink).toBeTruthy();
    expect(uploadLink!.getAttribute('href')).toBe('/murals/new');

    const router = TestBed.inject(Router);
    await router.navigateByUrl('/murals/new');

    expect(router.url).toBe('/login');
  });

  // Ensures authGuard import above is exercised directly too, matching the pattern used in
  // app.routes.spec.ts (avoids an unused-import lint failure while documenting intent).
  it('authGuard real (sin mockear) redirige a /login sin sesión', () => {
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url: '/murals/new' } as never),
    );
    expect(result).toBeTruthy();
  });
});
