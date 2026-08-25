# Spec FEAT-004: Sidebar de navegación global colapsable + navbar de contexto

| Field | Value |
|-------|-------|
| Ticket | FEAT-004 |
| PRD | docs/daw/prd/prd-FEAT-004.md |
| Tier | FEATURE |
| Date | 2026-08-25T12:22:01Z |
| Spec loops | 0 |

## Summary

Se agrega un shell de navegación transversal en `frontend/src/app/core/layout/`: un `LayoutStore`
(signal `collapsed` + `toggle()`), un `SidebarComponent` (logo, menú condicionado por sesión/rol,
footer sesión-vs-anónimo) y un `NavbarComponent` (título de la ruta activa + control de
expandir/contraer), compuestos por un `AppShellComponent`. `app.routes.ts` se reestructura para que
`/discover`, `/murals/new` y `/moderation` pasen a ser children de una ruta shell (`AppShellComponent`
vía `loadComponent`, mismo patrón lazy que ya usa el resto de rutas); `/login`, `/register` y el
redirect raíz (`rootRedirectGuard`) quedan fuera del shell, sin cambios de comportamiento.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 4, Block 5 |
| FR-02 | Block 2 |
| FR-03 | Block 2 |
| FR-04 | Block 2 |
| FR-05 | Block 2 |
| FR-06 | Block 2 |
| FR-07 | Block 2 |
| FR-08 | Block 2 |
| FR-09 | Block 1, Block 2, Block 3 |
| FR-10 | Block 1 |
| FR-11 | Block 1 |
| FR-12 | Block 4 |
| FR-13 | Block 3 |
| FR-14 | Block 3 |
| NFR-01 | Strategy: Block 2 y Block 3 dan `aria-label` dinámico y `tabindex`/elemento nativo `button` a sus respectivos controles de expandir/contraer, verificado en sus tests |

## Dependencies between blocks

Block 1 (`LayoutStore`) no depende de nada — es un servicio standalone.
Block 2 (`SidebarComponent`) depende de Block 1 (lee `layoutStore.collapsed()` y llama `toggle()`).
Block 3 (`NavbarComponent`) depende de Block 1 (mismo motivo que Block 2).
Block 4 (`AppShellComponent`) depende de Block 2 y Block 3 (los compone) — y es quien registra en
`app.config.ts` todos los íconos nuevos que 2, 3 y 4 necesitan, en un único edit.
Block 5 (rutas) depende de Block 4 (referencia `AppShellComponent` desde `app.routes.ts`).

Orden de ejecución: 1 → 2 → 3 → 4 → 5.

## Block 1 — `LayoutStore`

**Files**
- `frontend/src/app/core/layout/state/layout.store.ts` (new) — servicio de estado del shell.
- `frontend/src/app/core/layout/state/layout.store.spec.ts` (new).

**Logic**
`@Injectable({ providedIn: 'root' })`. Signal privado `collapsedSignal = signal<boolean>(window.innerWidth < 992)`,
expuesto como `collapsed = this.collapsedSignal.asReadonly()`. Método `toggle(): void` que hace
`this.collapsedSignal.update((v) => !v)`. Sin persistencia (a diferencia de `SessionStore`, que sí
persiste en `sessionStorage` — acá el PRD excluye explícitamente persistir el estado entre cargas,
ver "Out of Scope" del PRD). Sin listener de `resize`: el PRD (FR-10/FR-11, AC-12/AC-13) solo exige
el estado inicial en función del ancho **al cargar**, no una reacción continua al redimensionar — no
se implementa lo que el PRD no pide.

**Error handling**
No hay entradas externas que puedan fallar (no hace I/O, no llama servicios). `window.innerWidth`
siempre está disponible en el contexto del navegador donde corre Angular — no requiere try/catch.

**Required tests**
- [ ] `collapsed()` es `false` cuando `window.innerWidth` es `992` o más al construirse — valida AC-12/FR-10.
- [ ] `collapsed()` es `true` cuando `window.innerWidth` es menor a `992` al construirse — valida AC-13/FR-11.
- [ ] `toggle()` invierte el valor de `collapsed()` (expandido→colapsado y viceversa) — valida AC-11/FR-09.

Mecánica del breakpoint en los tests (gap del impact scan, resuelto acá): antes de cada test, fijar
`Object.defineProperty(window, 'innerWidth', { configurable: true, value: <ancho> })` y recién
después instanciar el store vía `TestBed.inject(LayoutStore)` dentro de un `TestBed.configureTestingModule`
propio de ese test (cada `configureTestingModule` crea un injector nuevo, así que el signal se
inicializa leyendo el `innerWidth` ya fijado) — no hay precedente de esto en el repo, así que este
patrón queda documentado acá para que Block 1 no lo resuelva "a ojo" ni deje un test no determinístico.

**Completion criterion**
Los 3 tests de `layout.store.spec.ts` pasan, sin flakiness al correr la suite completa (orden de
tests no afecta el resultado).

## Block 2 — `SidebarComponent`

**Files**
- `frontend/src/app/core/layout/ui/sidebar.component.ts` (new)
- `frontend/src/app/core/layout/ui/sidebar.component.html` (new)
- `frontend/src/app/core/layout/ui/sidebar.component.css` (new)
- `frontend/src/app/core/layout/ui/sidebar.component.spec.ts` (new)

**Logic**
Standalone, `selector: 'app-sidebar'`, `templateUrl`/`styleUrl` separados (nunca `template:` inline,
por convención del proyecto), `ChangeDetectionStrategy.OnPush`. Inyecta `SessionStore`, `LayoutStore`,
`AuthService`, `Router`.

- Header: `<app-logo />` (reutiliza `shared/logo/logo.component.ts`, sin cambios).
- Menú: lista de 2–3 ítems —
  - "Descubrir" → `routerLink="/discover"`, ícono `compass`.
  - "Cargar mural" → `routerLink="/murals/new"`, ícono `cloud-upload`.
  - "Moderación" → `routerLink="/moderation"`, ícono `safety-certificate`, envuelto en
    `@if (sessionStore.user()?.role === 'Administrator')`.
  - Ruta activa resaltada con `routerLinkActive="active"` sobre cada ítem (Angular ya resuelve esto
    sin lógica manual de comparación de URL).
- Footer, dos ramas con `@if (sessionStore.isAuthenticated())`:
  - Rama autenticada: `<span>{{ sessionStore.user()?.username }}</span>` + `<button>` "Cerrar
    sesión" (ícono `logout`) que llama a `onLogout()`.
  - Rama anónima (`@else`): dos links, "Iniciar sesión" → `/login`, "Registrarse" → `/register`.
- `onLogout()`: suscribe a `authService.logout()`; en su callback de éxito (y también en error,
  igual que ya hace el interceptor existente ante un 401 — la sesión se limpia localmente pase lo
  que pase con la llamada al backend) llama `sessionStore.clearSession()` y
  `router.navigate(['/login'])`.
- Renderizado colapsado/expandido: `[class.collapsed]="layoutStore.collapsed()"` en el `<aside>` raíz;
  el CSS oculta las etiquetas de texto (`display:none` sobre `.label`) cuando `.collapsed` está
  presente, dejando ícono + logo visibles — nunca se desmontan los ítems del DOM, solo se ocultan
  las etiquetas (más simple que alternar `*ngIf` y evita perder el estado de foco/hover).
- Control de expandir/contraer propio del sidebar (además del que va en el navbar, Block 3): un
  `<button>` con `[attr.aria-label]="layoutStore.collapsed() ? 'Expandir menú' : 'Contraer menú'"`
  que llama `layoutStore.toggle()` — cumple NFR-01.

**Error handling**
Si `authService.logout()` falla (ver arriba): la sesión local se limpia igual y se redirige a
`/login` — un fallo del backend no debe dejar al usuario con un botón de logout que no hace nada
visible.

**Required tests**
- [ ] Renderiza el logo — valida AC-03/FR-02.
- [ ] Renderiza los ítems "Descubrir" y "Cargar mural" con sus íconos — valida AC-04/FR-03.
- [ ] Con `SessionStore` sin rol `Administrator` (o sin sesión), NO renderiza "Moderación" — valida AC-05/FR-04.
- [ ] Con `SessionStore.user().role === 'Administrator'`, SÍ renderiza "Moderación" — valida AC-06/FR-04.
- [ ] El ítem de la ruta activa recibe la clase `active` (mock de `Router.url` o navegación real en el test) — valida AC-07/FR-05.
- [ ] Con sesión activa, muestra el username y el botón "Cerrar sesión"; sin sesión, muestra los links "Iniciar sesión"/"Registrarse" — valida AC-08/AC-09/FR-06/FR-07.
- [ ] Al hacer click en "Cerrar sesión": se llama `authService.logout()`, `sessionStore.clearSession()` y `router.navigate(['/login'])` (spies) — valida AC-10/FR-08.
- [ ] Si `authService.logout()` emite error (observable que falla), igual se llama `sessionStore.clearSession()` y `router.navigate(['/login'])` — valida el error handling documentado arriba.
- [ ] Al hacer click en "Cargar mural" sin sesión, la navegación resultante termina en `/login` (test de integración con `RouterTestingHarness` o equivalente, reutilizando el guard real `authGuard`, sin mockearlo) — valida AC-15/FR-01/FR-03.
- [ ] El botón de expandir/contraer del sidebar llama `layoutStore.toggle()` y su `aria-label` cambia según `collapsed()` — valida AC-11/FR-09/NFR-01.

**Completion criterion**
Los 10 tests de `sidebar.component.spec.ts` pasan; el componente no importa `HttpClient` ni el
cliente NSwag directamente (solo `AuthService`/`SessionStore`, ya wrappeados).

## Block 3 — `NavbarComponent`

**Files**
- `frontend/src/app/core/layout/ui/navbar.component.ts` (new)
- `frontend/src/app/core/layout/ui/navbar.component.html` (new)
- `frontend/src/app/core/layout/ui/navbar.component.css` (new)
- `frontend/src/app/core/layout/ui/navbar.component.spec.ts` (new)

**Logic**
Standalone, `selector: 'app-navbar'`, `templateUrl`/`styleUrl` separados, `OnPush`. Inyecta `Router`,
`LayoutStore`.

- Título: signal derivado con `toSignal` sobre
  `router.events.pipe(filter((e) => e instanceof NavigationEnd), map(() => this.readActiveTitle()), startWith(this.readActiveTitle()))`,
  donde `readActiveTitle()` recorre `router.routerState.root` siguiendo `firstChild` hasta el nodo
  más profundo y devuelve `node.snapshot.data['title'] ?? ''`. `startWith` cubre la carga inicial
  (antes del primer `NavigationEnd` posterior al bootstrap del componente).
- Botón de expandir/contraer: mismo patrón que en Block 2 —
  `[attr.aria-label]="layoutStore.collapsed() ? 'Expandir menú' : 'Contraer menú'"`, ícono
  `menu-unfold` cuando está colapsado / `menu-fold` cuando está expandido, `(click)="layoutStore.toggle()"`.

**Error handling**
Si `data['title']` no está definido en la ruta activa (no debería pasar tras Block 5, pero el
componente no debe romperse si pasa): `readActiveTitle()` devuelve cadena vacía en vez de lanzar,
el navbar simplemente no muestra texto de título ese frame.

**Required tests**
- [ ] Con la ruta activa mockeada con `data: { title: 'Descubrir' }`, el navbar muestra "Descubrir" — valida AC-14/FR-13.
- [ ] Al navegar a otra ruta con distinto `data.title`, el texto se actualiza — valida AC-14/FR-13.
- [ ] El botón de expandir/contraer llama `layoutStore.toggle()` y alterna ícono/`aria-label` según `collapsed()` — valida AC-11/FR-09/FR-14/NFR-01.
- [ ] Con la ruta activa mockeada sin `data.title`, el componente no lanza y el título se renderiza vacío — valida el error handling documentado arriba.

**Completion criterion**
Los 4 tests de `navbar.component.spec.ts` pasan.

## Block 4 — `AppShellComponent`

**Files**
- `frontend/src/app/core/layout/ui/app-shell.component.ts` (new)
- `frontend/src/app/core/layout/ui/app-shell.component.html` (new)
- `frontend/src/app/core/layout/ui/app-shell.component.css` (new)
- `frontend/src/app/core/layout/ui/app-shell.component.spec.ts` (new)
- `frontend/src/app/app.config.ts` (modified) — único punto donde este ticket edita `provideNzIcons(...)`.

**Logic**
Standalone, `selector: 'app-shell'`, `templateUrl`/`styleUrl` separados, `OnPush`. Compone
`<app-sidebar />` + `<app-navbar />` + `<router-outlet />` en un layout CSS grid: columna izquierda
(sidebar, ancho fijo cuando expandido / ancho reducido cuando colapsado, transición vía la clase
`.collapsed` que ya expone `LayoutStore` a través de Block 2) y una columna derecha dividida en fila
superior (navbar) + resto (contenido ruteado).

Color distinto entre navbar y sidebar (FR-12), usando los tokens ya existentes de FEAT-002 en
`frontend/src/styles.css` — sin hex nuevos:
- Sidebar: `background: var(--app-color-secondary)` (navy).
- Navbar: `background: var(--ant-primary-color)` (coral).

Edita `app.config.ts`: agrega a la lista de `provideNzIcons([...])` los íconos nuevos que 2/3/4
necesitan — `CompassOutline`, `CloudUploadOutline`, `SafetyCertificateOutline`, `LogoutOutline`,
`UserOutline`, `MenuFoldOutline`, `MenuUnfoldOutline` (de `@ant-design/icons-angular/icons`, mismo
paquete que ya usan `CheckCircleOutline`/`GoogleOutline`/etc.) — en un único edit, sin tocar las
entradas ya existentes salvo agregar estas nuevas al final del array.

**Error handling**
N/A — componente puramente de composición/layout, sin lógica que pueda fallar en runtime más allá
de lo que ya cubren Block 2 y Block 3.

**Required tests**
- [ ] Renderiza `app-sidebar`, `app-navbar` y `router-outlet` — valida AC-01/FR-01/FR-12.
- [ ] El elemento del sidebar y el del navbar tienen `background-color` computado distinto (leído vía `getComputedStyle` en jsdom, o comparando las custom properties aplicadas) — valida FR-12.

**Completion criterion**
Los 2 tests de `app-shell.component.spec.ts` pasan; `app.config.ts` sigue compilando y sus tests
existentes (`app.config.ts` no tiene spec propio hoy — se verifica indirectamente vía
`app.routes.spec.ts` en Block 5) no se rompen.

## Block 5 — Restructuración de rutas

**Files**
- `frontend/src/app/app.routes.ts` (modified)
- `frontend/src/app/app.routes.spec.ts` (modified)
- `frontend/src/app/app.component.html` (no modificado — se confirma explícitamente: sigue siendo
  únicamente `<router-outlet />`; `AppShellComponent` se renderiza indirectamente a través de la
  nueva ruta shell, no reemplaza a `AppComponent`).

**Logic**
En `app.routes.ts`, el array `routes` gana una nueva entrada **después** de la entrada existente
`{ path: '', pathMatch: 'full', canActivate: [rootRedirectGuard], children: [] }` (ese orden queda
en un comentario explícito en el archivo — el impact scan confirmó que Angular Router 21 hace
backtracking y funcionaría en cualquier orden, pero se declara así para no depender de ese detalle
interno):

```ts
{
  path: '',
  loadComponent: () =>
    import('./core/layout/ui/app-shell.component').then((m) => m.AppShellComponent),
  children: [
    {
      path: 'discover',
      data: { title: 'Descubrir' },
      loadComponent: () => import('./features/discovery/ui/discovery-page.component')...,
    },
    {
      path: 'murals/new',
      canActivate: [authGuard],
      data: { title: 'Cargar mural' },
      loadComponent: () => import('./features/murals/ui/create-mural-form.component')...,
    },
    {
      path: 'moderation',
      canActivate: [authGuard, adminGuard],
      data: { title: 'Moderación' },
      loadComponent: () => import('./features/moderation/ui/pending-murals-list.component')...,
    },
  ],
},
```

(usa `loadComponent`, igual que las 5 rutas ya existentes — corrige el FAIL del arch-auditor de usar
`component:` con import estático). Las rutas `login` y `register` NO se mueven — quedan como
entradas de primer nivel, iguales a hoy, sin `data.title` (no llevan navbar).

`app.routes.spec.ts` se actualiza para navegar contra la estructura anidada (los tests que hoy
verifican `authGuard`/`adminGuard` sobre `/murals/new` y `/moderation` siguen verificando lo mismo,
ahora a través de un `router.navigate` que atraviesa la ruta shell primero) y agrega casos para
`data.title` de cada child.

**Error handling**
N/A — es configuración declarativa de ruteo, sin lógica de runtime propia más allá de los guards ya
existentes (sin cambios en `authGuard`/`adminGuard`/`rootRedirectGuard`).

**Required tests**
- [ ] Navegar a `/discover` sin sesión resuelve con `AppShellComponent` activo y `DiscoveryPageComponent` como child — valida AC-01/AC-02.
- [ ] Navegar a `/murals/new` sin sesión redirige a `/login` (mismo comportamiento de `authGuard` que hoy, ahora anidado) — regresión, no nueva AC.
- [ ] Navegar a `/moderation` con sesión pero sin rol `Administrator` redirige a `/` (mismo comportamiento de `adminGuard` que hoy) — regresión, no nueva AC.
- [ ] Navegar a `/login` o `/register` NO activa `AppShellComponent` — valida AC-02.
- [ ] `data.title` de cada child coincide con lo esperado ("Descubrir", "Cargar mural", "Moderación") — valida AC-14 en conjunto con Block 3.
- [ ] La ruta raíz (`/`) sigue redirigiendo a `/discover` (con sesión) o `/login` (sin sesión) sin activar la ruta shell nunca (verifica que las dos entradas `path: ''` no colisionan) — regresión de `rootRedirectGuard`, no nueva AC.

**Completion criterion**
Los 6 tests (actualizados/nuevos) de `app.routes.spec.ts` pasan; ningún test existente de otro
`.spec.ts` del repo que dependa de las rutas actuales (`login-form`, `register-form`,
`discovery-page`, `create-mural-form`, `pending-murals-list`) se rompe.

## Final verification

- Los 5 bloques compilan juntos (`ng build` / `tsc --build --noEmit` sin errores).
- La suite completa de frontend (`npm test` — Vitest) pasa, incluyendo los 25 tests nuevos de este
  spec (3+10+4+2+6, más los ajustados en `app.routes.spec.ts`) sumados a los ~92 existentes.
- Navegando manualmente (o vía test e2e si el repo tuviera uno — no lo tiene, queda cubierto por los
  tests de componente/ruteo de arriba): `/discover`, `/murals/new` y `/moderation` muestran sidebar +
  navbar; `/login` y `/register` no; el ítem "Moderación" solo aparece para `Administrator`; cerrar
  sesión limpia el estado y redirige a `/login`; en `<992px` el sidebar arranca colapsado y en
  `≥992px` expandido; el botón de expandir/contraer (en sidebar o navbar) alterna el mismo estado
  desde cualquiera de los dos lugares.
- `daw-validate-arch` (al inicio de CODE) y el `daw-arch-auditor` por bloque no reportan FAILs contra
  este spec.
