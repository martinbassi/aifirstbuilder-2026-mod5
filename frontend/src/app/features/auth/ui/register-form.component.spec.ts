import { Component } from '@angular/core';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthService } from '../data/auth.service';
import { RegisterFormComponent } from './register-form.component';

@Component({ selector: 'app-dummy-login', template: '' })
class DummyLoginComponent {}

describe('RegisterFormComponent', () => {
  let authService: { register: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    authService = { register: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [RegisterFormComponent],
      providers: [
        // A successful register() redirects to /login — routed so that navigation resolves
        // instead of surfacing an unrelated "cannot match any routes" error in these tests.
        provideRouter([{ path: 'login', component: DummyLoginComponent }]),
        { provide: AuthService, useValue: authService },
      ],
    }).compileComponents();
  });

  // Required test 3: el mensaje de error mostrado es exactamente el que devuelve el backend, sin
  // texto adicional que distinga campos.
  it('muestra exactamente el mensaje genérico del backend, sin agregar distinción de campos', () => {
    authService.register.mockReturnValue(
      throwError(() => ({ status: 400, message: 'Username or email is already in use.' })),
    );

    const fixture = TestBed.createComponent(RegisterFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    component.form.setValue({ username: 'ana', email: 'ana@example.com', password: 'Passw0rd' });
    component.submit();
    fixture.detectChanges();

    const errorEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="error-message"]',
    );
    expect(errorEl).toBeTruthy();
    expect(errorEl.textContent?.trim()).toBe('Username or email is already in use.');
  });

  // Required test (Block 3, AC-04): la pantalla de register muestra el mismo logo compartido que
  // login (mismo componente `LogoComponent` → mismo alt, mismo src).
  it('renderiza el logo compartido (app-logo), con el mismo alt y src que en login', () => {
    const fixture = TestBed.createComponent(RegisterFormComponent);
    fixture.detectChanges();

    const logoEl = fixture.nativeElement.querySelector('app-logo');
    expect(logoEl).toBeTruthy();

    const img: HTMLImageElement = logoEl.querySelector('img');
    expect(img.getAttribute('src')).toBe('/images/logo.jpg');
    expect(img.getAttribute('alt')).toBe('paretto — urban art discovery');
  });

  // Required test (Block 2, AC-02/AC-03/AC-08): el logo y el form quedan envueltos por
  // app-auth-card, con el logo antes que el form, y usando la misma clase/ancho máximo de card
  // que login (mismo componente compartido AuthCardComponent, no duplicación).
  it('envuelve el logo y el form dentro de app-auth-card, con el logo antes que el form y la misma clase que login', () => {
    const fixture = TestBed.createComponent(RegisterFormComponent);
    fixture.detectChanges();

    const authCard: HTMLElement | null = fixture.nativeElement.querySelector('app-auth-card');
    expect(authCard).toBeTruthy();

    const logoEl: HTMLElement | null = authCard!.querySelector('app-logo');
    const formEl: HTMLElement | null = authCard!.querySelector('form');
    expect(logoEl).toBeTruthy();
    expect(formEl).toBeTruthy();

    const position = logoEl!.compareDocumentPosition(formEl!);
    // eslint-disable-next-line no-bitwise
    expect(position & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    // Mismo ancho máximo/clase que login: el wrapper es el mismo AuthCardComponent compartido,
    // reconocible por la clase `auth-card` en el nz-card interno.
    const card: HTMLElement | null = authCard!.querySelector('nz-card.auth-card');
    expect(card).toBeTruthy();
  });

  it('no muestra ningún mensaje de error cuando no se envió el formulario', () => {
    const fixture = TestBed.createComponent(RegisterFormComponent);
    fixture.detectChanges();

    const errorEl = fixture.nativeElement.querySelector('[data-testid="error-message"]');
    expect(errorEl).toBeFalsy();
  });

  it('registra exitosamente y no muestra ningún error', () => {
    authService.register.mockReturnValue(of({ id: 'user-1', username: 'ana' }));

    const fixture = TestBed.createComponent(RegisterFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    component.form.setValue({ username: 'ana', email: 'ana@example.com', password: 'Passw0rd' });
    component.submit();
    fixture.detectChanges();

    const errorEl = fixture.nativeElement.querySelector('[data-testid="error-message"]');
    expect(errorEl).toBeFalsy();
  });
});
