import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, UrlTree, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import {
  CloudUploadOutline,
  CompassOutline,
  GoogleOutline,
  LogoutOutline,
  MenuFoldOutline,
  MenuUnfoldOutline,
  SafetyCertificateOutline,
} from '@ant-design/icons-angular/icons';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import {
  AddressesClient,
  AuthClient,
  DiscoveryClient,
  ModerationClient,
  MuralsClient,
} from './core/api-client/api-client.generated';
import { SessionStore } from './features/auth/state/session.store';
import { adminGuard, authGuard, rootRedirectGuard, routes } from './app.routes';

// Íconos que necesitan SidebarComponent/NavbarComponent (Block 2/3), ahora ejercitados por estos
// tests porque `/discover`, `/murals/new` y `/moderation` activan `AppShellComponent` (Block 5),
// que los compone. GoogleOutline sigue siendo necesario para LoginFormComponent.
const LAYOUT_ICONS = [
  CompassOutline,
  CloudUploadOutline,
  SafetyCertificateOutline,
  LogoutOutline,
  MenuFoldOutline,
  MenuUnfoldOutline,
  GoogleOutline,
];

/** Recorre la rama activa hasta el nodo más profundo, igual que `NavbarComponent.readActiveTitle()`
 * — se reimplementa localmente en el test para no depender del navbar (Block 3) como forma de
 * verificar `data.title` (Block 5 sólo es responsable de declararlo, no de leerlo). */
function deepestActivatedSnapshot(router: Router): ActivatedRouteSnapshot {
  let node = router.routerState.snapshot.root;
  while (node.firstChild) {
    node = node.firstChild;
  }
  return node;
}

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

describe('rootRedirectGuard', () => {
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
      rootRedirectGuard({} as never, { url: '/' } as never),
    );
  }

  // Required test 1 (Block 8): sin sesión, / resuelve en /login — AC-08.
  it('redirige a /login cuando no hay sesión activa', () => {
    const result = runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
  });

  // Required test 2 (Block 8): con sesión, / resuelve en /discover — AC-09.
  it('redirige a /discover cuando hay una sesión activa', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });

    const result = runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/discover');
  });
});

// Suite de integración compartida por los 6 tests requeridos del Block 5 (restructuración de
// rutas): navega contra la estructura anidada real (`routes` de app.routes.ts, sin mockear ni
// `AppShellComponent` ni los guards) vía `RouterTestingHarness`, igual patrón que ya usaban los
// tests previos de este archivo.
describe('routes — estructura anidada del shell (Block 5)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SessionStore,
        provideRouter(routes),
        provideHttpClient(),
        AuthClient,
        DiscoveryClient,
        MuralsClient,
        ModerationClient,
        AddressesClient,
        provideNzIcons(LAYOUT_ICONS),
      ],
    });
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  // Required test 1: navegar a /discover sin sesión resuelve con AppShellComponent activo y
  // DiscoveryPageComponent como child — AC-01/AC-02.
  it('navegar a /discover sin sesión activa AppShellComponent con DiscoveryPageComponent como child', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/discover');

    const router = TestBed.inject(Router);
    expect(router.url).toBe('/discover');

    const shell = harness.routeNativeElement;
    expect(shell?.tagName.toLowerCase()).toBe('app-shell');
    expect(shell!.querySelector('app-discovery-page')).toBeTruthy();
  });

  // Required test 2: navegar a /murals/new sin sesión redirige a /login (regresión de authGuard,
  // ahora anidado bajo la ruta shell).
  it('navegar a /murals/new sin sesión redirige a /login', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/murals/new');

    const router = TestBed.inject(Router);
    expect(router.url).toBe('/login');
  });

  // Required test 3: navegar a /moderation con sesión pero sin rol Administrator redirige a /
  // (regresión de adminGuard).
  it('navegar a /moderation con sesión sin rol Administrator redirige a /', async () => {
    const sessionStore = TestBed.inject(SessionStore);
    sessionStore.setSession('token-abc', { username: 'ana', role: 'Standard' });

    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/moderation');

    const router = TestBed.inject(Router);
    // adminGuard redirige a '/', que a su vez pasa por rootRedirectGuard: con sesión activa,
    // termina resolviendo en /discover.
    expect(router.url).toBe('/discover');
  });

  // Required test 4: navegar a /login o /register NO activa AppShellComponent — AC-02.
  it('navegar a /login no activa AppShellComponent', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/login');

    const router = TestBed.inject(Router);
    expect(router.url).toBe('/login');
    expect(harness.routeNativeElement?.tagName.toLowerCase()).not.toBe('app-shell');
  });

  it('navegar a /register no activa AppShellComponent', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/register');

    const router = TestBed.inject(Router);
    expect(router.url).toBe('/register');
    expect(harness.routeNativeElement?.tagName.toLowerCase()).not.toBe('app-shell');
  });

  // Required test 5: data.title de cada child coincide con lo esperado — AC-14 (junto con Block 3).
  it('data.title de /discover, /murals/new y /moderation coincide con lo esperado', async () => {
    const sessionStore = TestBed.inject(SessionStore);
    sessionStore.setSession('token-abc', { username: 'admin', role: 'Administrator' });
    const router = TestBed.inject(Router);

    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/discover');
    expect(deepestActivatedSnapshot(router).data['title']).toBe('Descubrir');

    await harness.navigateByUrl('/murals/new');
    expect(deepestActivatedSnapshot(router).data['title']).toBe('Cargar mural');

    await harness.navigateByUrl('/moderation');
    expect(deepestActivatedSnapshot(router).data['title']).toBe('Moderación');
  });

  // Required test 6: la ruta raíz (/) sigue redirigiendo a /discover (con sesión) o /login (sin
  // sesión), sin activar la ruta shell nunca — regresión de rootRedirectGuard, verifica que las dos
  // entradas `path: ''` no colisionan.
  it('/ redirige a /login sin sesión, sin activar AppShellComponent', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    const router = TestBed.inject(Router);
    expect(router.url).toBe('/login');
    expect(harness.routeNativeElement?.tagName.toLowerCase()).not.toBe('app-shell');
  });

  it('/ redirige a /discover con sesión activa, activando AppShellComponent (no la entrada shell raíz)', async () => {
    const sessionStore = TestBed.inject(SessionStore);
    sessionStore.setSession('token-abc', { username: 'ana' });

    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    const router = TestBed.inject(Router);
    expect(router.url).toBe('/discover');
    expect(harness.routeNativeElement?.tagName.toLowerCase()).toBe('app-shell');
  });
});
