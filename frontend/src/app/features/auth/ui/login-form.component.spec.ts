import { Component } from '@angular/core';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { GoogleOutline } from '@ant-design/icons-angular/icons';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { of, throwError } from 'rxjs';
import { AuthService } from '../data/auth.service';
import { LoginFormComponent } from './login-form.component';
import { RegisterFormComponent } from './register-form.component';

@Component({ selector: 'app-dummy-home', template: '' })
class DummyHomeComponent {}

describe('LoginFormComponent', () => {
  let authService: { login: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    authService = { login: vi.fn() };

    await TestBed.configureTestingModule({
      // RegisterFormComponent se registra acá también para el test de equivalencia estructural
      // (AC-06), que compara las clases de ambos formularios contra el mismo TestBed.
      imports: [LoginFormComponent, RegisterFormComponent],
      providers: [
        // A successful login() redirects to / — routed so that navigation resolves instead of
        // surfacing an unrelated "cannot match any routes" error in these tests.
        provideRouter([{ path: '', component: DummyHomeComponent }]),
        { provide: AuthService, useValue: authService },
        // Sin esto, NzIconService intenta resolver "google-o" contra el registro global y falla
        // con IconNotFoundError (mismo registro que provideNzIcons en app.config.ts, Block 8).
        provideNzIcons([GoogleOutline]),
      ],
    }).compileComponents();
  });

  // Required test 3: el mensaje de error mostrado es exactamente el que devuelve el backend, sin
  // texto adicional que distinga campos (FR-05/AC-05: mismo mensaje para usuario o contraseña
  // incorrectos).
  it('muestra exactamente el mensaje genérico del backend, sin distinguir usuario/contraseña', () => {
    authService.login.mockReturnValue(
      throwError(() => ({ status: 401, message: 'Invalid username or password.' })),
    );

    const fixture = TestBed.createComponent(LoginFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    component.form.setValue({ username: 'ana', password: 'wrong-pass' });
    component.submit();
    fixture.detectChanges();

    const errorEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="error-message"]',
    );
    expect(errorEl).toBeTruthy();
    expect(errorEl.textContent?.trim()).toBe('Invalid username or password.');
  });

  // Required test (Block 2, AC-01): el formulario queda envuelto por app-auth-card. LogoComponent
  // ya no se usa (ver PRD Out of Scope) — el wordmark de texto vive en AuthCardComponent (Block 1).
  it('envuelve el form dentro de app-auth-card', () => {
    const fixture = TestBed.createComponent(LoginFormComponent);
    fixture.detectChanges();

    const authCard: HTMLElement | null = fixture.nativeElement.querySelector('app-auth-card');
    expect(authCard).toBeTruthy();

    const formEl: HTMLElement | null = authCard!.querySelector('form');
    expect(formEl).toBeTruthy();
  });

  // Required test (Block 2, AC-06): login y register comparten auth-form.css — mismas clases
  // estructurales en el mismo lugar del árbol.
  it('usa las mismas clases estructurales (.auth-header/.auth-form/.auth-submit-button) que register-form', () => {
    const loginFixture = TestBed.createComponent(LoginFormComponent);
    loginFixture.detectChanges();

    const registerFixture = TestBed.createComponent(RegisterFormComponent);
    registerFixture.detectChanges();

    for (const selector of ['.auth-header', '.auth-form', '.auth-submit-button']) {
      const loginEl: HTMLElement | null = loginFixture.nativeElement.querySelector(selector);
      const registerEl: HTMLElement | null = registerFixture.nativeElement.querySelector(selector);
      expect(loginEl).toBeTruthy();
      expect(registerEl).toBeTruthy();
    }
  });

  // Required test (Block 2, AC-07): fix del bug de Block 2.1 — sin NzIconModule importado, el ícono
  // se compila como un custom element vacío (Angular tolera tags con guion sin CUSTOM_ELEMENTS_SCHEMA)
  // pero la directiva NzIconDirective nunca se activa y no aplica sus host bindings (role="img",
  // class "anticon-google"). Ese host binding es lo que distingue "el tag está" de "el ícono se
  // renderiza".
  it('el botón de Google contiene el ícono de Google renderizado por NzIconDirective', () => {
    const fixture = TestBed.createComponent(LoginFormComponent);
    fixture.detectChanges();

    const googleButton: HTMLElement | null = fixture.nativeElement.querySelector('.google-button');
    expect(googleButton).toBeTruthy();

    const icon: HTMLElement | null = googleButton!.querySelector('nz-icon[nzType="google"]');
    expect(icon).toBeTruthy();
    expect(icon!.getAttribute('role')).toBe('img');
    expect(icon!.classList.contains('anticon-google')).toBe(true);
  });

  it('no muestra ningún mensaje de error cuando no se envió el formulario', () => {
    const fixture = TestBed.createComponent(LoginFormComponent);
    fixture.detectChanges();

    const errorEl = fixture.nativeElement.querySelector('[data-testid="error-message"]');
    expect(errorEl).toBeFalsy();
  });

  it('inicia sesión exitosamente y no muestra ningún error', () => {
    authService.login.mockReturnValue(of(undefined));

    const fixture = TestBed.createComponent(LoginFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    component.form.setValue({ username: 'ana', password: 'Passw0rd' });
    component.submit();
    fixture.detectChanges();

    const errorEl = fixture.nativeElement.querySelector('[data-testid="error-message"]');
    expect(errorEl).toBeFalsy();
  });
});
