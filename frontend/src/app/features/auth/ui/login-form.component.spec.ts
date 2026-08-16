import { Component } from '@angular/core';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthService } from '../data/auth.service';
import { LoginFormComponent } from './login-form.component';

@Component({ selector: 'app-dummy-home', template: '' })
class DummyHomeComponent {}

describe('LoginFormComponent', () => {
  let authService: { login: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    authService = { login: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [LoginFormComponent],
      providers: [
        // A successful login() redirects to / — routed so that navigation resolves instead of
        // surfacing an unrelated "cannot match any routes" error in these tests.
        provideRouter([{ path: '', component: DummyHomeComponent }]),
        { provide: AuthService, useValue: authService },
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
