import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, RouterOutlet, Routes, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { MenuFoldOutline, MenuUnfoldOutline } from '@ant-design/icons-angular/icons';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { LayoutStore } from '../state/layout.store';
import { NavbarComponent } from './navbar.component';

@Component({ selector: 'app-dummy-route', template: '' })
class DummyRouteComponent {}

// Reproduce el árbol de rutas que Block 5 conectará: un padre lazy (`loadComponent`, como
// AppShellComponent) que renderiza <app-navbar> como hermano estático de <router-outlet>, con un
// child TAMBIÉN lazy (como `discover`/`murals/new`/`moderation`). En la PRIMERA navegación a ese
// child, Angular Router construye el shell (y por lo tanto el NavbarComponent) antes de que
// `advanceActivatedRoute` complete sobre el `ActivatedRoute` del child — el nodo más profundo del
// árbol tiene `snapshot === undefined` en ese instante.
@Component({
  selector: 'app-test-shell',
  standalone: true,
  imports: [NavbarComponent, RouterOutlet],
  template: '<app-navbar></app-navbar><router-outlet></router-outlet>',
})
class TestShellComponent {}

const lazyShellRoutes: Routes = [
  {
    path: 'shell',
    loadComponent: () => Promise.resolve(TestShellComponent),
    children: [
      {
        path: 'discover',
        loadComponent: () => Promise.resolve(DummyRouteComponent),
        data: { title: 'Descubrir' },
      },
    ],
  },
];

// Dummy routes with `data.title` — isolates NavbarComponent from the real app.routes.ts (Block 5),
// which this block does not depend on. `MenuFoldOutline`/`MenuUnfoldOutline` are registered locally
// here (not in app.config.ts, which is Block 4's responsibility per the spec dependency order),
// same pattern login-form.component.spec.ts already uses for `GoogleOutline`.
const testRoutes: Routes = [
  { path: 'discover', data: { title: 'Descubrir' }, component: DummyRouteComponent },
  { path: 'moderation', data: { title: 'Moderación' }, component: DummyRouteComponent },
  { path: 'no-title', component: DummyRouteComponent },
];

describe('NavbarComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NavbarComponent],
      providers: [provideRouter(testRoutes), provideNzIcons([MenuFoldOutline, MenuUnfoldOutline])],
    }).compileComponents();
  });

  // Required test 1: con la ruta activa mockeada con data: { title: 'Descubrir' }, el navbar
  // muestra "Descubrir" — AC-14/FR-13. Navegar ANTES de crear el componente ejercita la rama
  // `startWith` (no queda ningún NavigationEnd futuro por escuchar).
  it('muestra el título de la ruta activa (data.title) al montarse', async () => {
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/discover');

    const fixture = TestBed.createComponent(NavbarComponent);
    fixture.detectChanges();

    const titleEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="navbar-title"]',
    );
    expect(titleEl.textContent?.trim()).toBe('Descubrir');
  });

  // Required test 2: al navegar a otra ruta con distinto data.title, el texto se actualiza —
  // AC-14/FR-13. Acá el componente ya está montado y reacciona a un NavigationEnd real.
  it('actualiza el título al navegar a otra ruta con distinto data.title', async () => {
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/discover');

    const fixture = TestBed.createComponent(NavbarComponent);
    fixture.detectChanges();

    await router.navigateByUrl('/moderation');
    fixture.detectChanges();

    const titleEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="navbar-title"]',
    );
    expect(titleEl.textContent?.trim()).toBe('Moderación');
  });

  // Required test 3: el botón de expandir/contraer llama layoutStore.toggle() y alterna
  // ícono/aria-label según collapsed() — AC-11/FR-09/FR-14/NFR-01.
  it('el botón de expandir/contraer llama layoutStore.toggle() y alterna ícono/aria-label', async () => {
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/discover');

    const fixture = TestBed.createComponent(NavbarComponent);
    fixture.detectChanges();

    const layoutStore = TestBed.inject(LayoutStore);
    const toggleSpy = vi.spyOn(layoutStore, 'toggle');

    const button: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="navbar-toggle"]',
    );

    const collapsedBefore = layoutStore.collapsed();
    expect(button.getAttribute('aria-label')).toBe(
      collapsedBefore ? 'Expandir menú' : 'Contraer menú',
    );
    let icon = button.querySelector('nz-icon');
    expect(icon?.classList.contains(collapsedBefore ? 'anticon-menu-unfold' : 'anticon-menu-fold')).toBe(
      true,
    );

    button.click();
    fixture.detectChanges();

    expect(toggleSpy).toHaveBeenCalledOnce();
    const collapsedAfter = layoutStore.collapsed();
    expect(collapsedAfter).toBe(!collapsedBefore);
    expect(button.getAttribute('aria-label')).toBe(
      collapsedAfter ? 'Expandir menú' : 'Contraer menú',
    );
    icon = button.querySelector('nz-icon');
    expect(icon?.classList.contains(collapsedAfter ? 'anticon-menu-unfold' : 'anticon-menu-fold')).toBe(
      true,
    );
  });

  // Required test 4: con una ruta activa mockeada SIN data.title, el componente no lanza y el
  // título se renderiza vacío — valida el error handling documentado en el spec.
  it('con una ruta activa sin data.title, no lanza y el título se renderiza vacío', async () => {
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/no-title');

    let fixture!: ReturnType<typeof TestBed.createComponent<NavbarComponent>>;
    expect(() => {
      fixture = TestBed.createComponent(NavbarComponent);
      fixture.detectChanges();
    }).not.toThrow();

    const titleEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="navbar-title"]',
    );
    expect(titleEl.textContent?.trim()).toBe('');
  });
});

describe('NavbarComponent — regresión: TypeError en la primera activación de rutas lazy anidadas', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideRouter(lazyShellRoutes), provideNzIcons([MenuFoldOutline, MenuUnfoldOutline])],
    }).compileComponents();
  });

  // Reproduce el bug real: NavbarComponent, como hermano estático de <router-outlet> dentro de un
  // padre ruteado con `loadComponent` (lazy) que tiene un child TAMBIÉN con `loadComponent` (lazy),
  // se construye al navegar por primera vez a ese child. Antes del fix, `readActiveTitle()` lanza
  // `TypeError: Cannot read properties of undefined (reading 'data')` porque el nodo más profundo
  // del árbol de rutas todavía no tiene `snapshot` asignado en ese instante.
  it('no lanza TypeError al navegar por primera vez a un child lazy dentro de un padre lazy', async () => {
    const harness = await RouterTestingHarness.create();

    let thrown: unknown;
    try {
      await harness.navigateByUrl('/shell/discover');
    } catch (error) {
      thrown = error;
    }

    expect(thrown).toBeUndefined();

    harness.detectChanges();
    const titleEl: HTMLElement | null | undefined = harness.routeNativeElement?.parentElement
      ?.querySelector('app-navbar')
      ?.querySelector('[data-testid="navbar-title"]');
    expect(titleEl?.textContent?.trim()).toBe('Descubrir');
  });
});
