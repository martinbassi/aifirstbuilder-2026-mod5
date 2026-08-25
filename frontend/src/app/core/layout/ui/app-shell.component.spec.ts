import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import {
  CloudUploadOutline,
  CompassOutline,
  LogoutOutline,
  MenuFoldOutline,
  MenuUnfoldOutline,
  SafetyCertificateOutline,
} from '@ant-design/icons-angular/icons';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { AuthService } from '../../../features/auth/data/auth.service';
import { SessionStore } from '../../../features/auth/state/session.store';
import { AppShellComponent } from './app-shell.component';

@Component({ selector: 'app-dummy-routed-content', template: 'contenido ruteado' })
class DummyRoutedContentComponent {}

// Íconos usados por SidebarComponent/NavbarComponent (Block 2/3), registrados localmente igual que
// sus propios specs — app.config.ts (Block 4's actual responsibility) no se involucra en este test.
describe('AppShellComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppShellComponent],
      providers: [
        provideRouter([{ path: '', component: DummyRoutedContentComponent }]),
        // AuthService no se ejercita en este bloque (eso es responsabilidad de Block 2) — se
        // reemplaza por un stub, igual que hace sidebar.component.spec.ts.
        { provide: AuthService, useValue: { logout: vi.fn() } },
        SessionStore,
        provideNzIcons([
          CompassOutline,
          CloudUploadOutline,
          SafetyCertificateOutline,
          LogoutOutline,
          MenuFoldOutline,
          MenuUnfoldOutline,
        ]),
      ],
    }).compileComponents();
  });

  // Required test 1 (AC-01/FR-01/FR-12): renderiza app-sidebar, app-navbar y router-outlet.
  it('renderiza app-sidebar, app-navbar y el contenido ruteado (router-outlet)', async () => {
    // Navegar explícitamente antes de crear el fixture: `provideRouter` no dispara la navegación
    // inicial automáticamente dentro de TestBed (a diferencia del bootstrap real de la app) — mismo
    // patrón que ya usa navbar.component.spec.ts.
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/');

    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('app-sidebar');
    const navbar = fixture.nativeElement.querySelector('app-navbar');
    const routedContent = fixture.nativeElement.querySelector('app-dummy-routed-content');

    expect(sidebar).toBeTruthy();
    expect(navbar).toBeTruthy();
    expect(routedContent).toBeTruthy();
  });

  // Required test 2 (FR-12): color de fondo distinto entre sidebar y navbar.
  //
  // Deviación documentada respecto al spec: jsdom no resuelve `var(...)` al calcular
  // `getComputedStyle().backgroundColor` (limitación conocida de su motor CSS — siempre devuelve
  // 'rgba(0, 0, 0, 0)' para cualquier `background` declarado con una custom property, sin importar
  // la implementación). El propio spec habilita la alternativa para este caso ("o comparando las
  // custom properties aplicadas"): se lee el shorthand `background` vía `getPropertyValue`, que
  // jsdom sí expone tal como fue declarado, y se comparan los tokens de FEAT-002 aplicados a cada
  // elemento — sidebar usa `--app-color-secondary` (navy), navbar usa `--ant-primary-color` (coral),
  // son literales distintos y por lo tanto el fondo visual resultante también lo es.
  it('el sidebar y el navbar aplican tokens de color de fondo distintos (FR-12)', () => {
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();

    const sidebar: HTMLElement = fixture.nativeElement.querySelector('app-sidebar');
    const navbar: HTMLElement = fixture.nativeElement.querySelector('app-navbar');

    const sidebarBackground = getComputedStyle(sidebar).getPropertyValue('background');
    const navbarBackground = getComputedStyle(navbar).getPropertyValue('background');

    expect(sidebarBackground).toContain('--app-color-secondary');
    expect(navbarBackground).toContain('--ant-primary-color');
    expect(sidebarBackground).not.toBe(navbarBackground);
  });
});
