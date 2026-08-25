# PRD FEAT-004: Sidebar de navegación global colapsable + navbar de contexto

| Field | Value |
|-------|-------|
| Ticket | FEAT-004 |
| Tracker | none |
| Date | 2026-08-25T11:59:30Z |
| PRD loops | 1 |

## Context and Problem

Hoy cada feature de Paretto es standalone: no existe un layout compartido entre pantallas. FEAT-002
(identidad visual) y FEAT-003 (rediseño de login/register) dejaron explícitamente afuera de su
alcance un "header/navbar global", señalando que crear uno era un cambio de arquitectura de
navegación que ameritaba su propio ticket. Este es ese ticket.

Sin un layout compartido, el usuario no tiene una forma consistente de moverse entre "Descubrir",
"Cargar mural" y (si es administrador) "Moderación", ni de ver en todo momento qué cuenta tiene
iniciada o cómo cerrar sesión. Un visitante anónimo que llega a `/discover` tampoco tiene ninguna
invitación visible a registrarse o publicar sus propios murales.

## Goals

- Dar a la aplicación una navegación global consistente: un sidebar izquierdo con el logo y los
  puntos de menú, y un navbar superior que ubica al usuario en la pantalla activa.
- Que el sidebar funcione también como invitación a registrarse para un visitante anónimo en
  `/discover`, sin necesidad de un elemento de UI adicional (una card lateral) para ese propósito.
- Que el layout se adapte a resoluciones chicas (colapsado por defecto) y grandes (expandido por
  defecto), siempre pudiendo alternarse manualmente.
- Reutilizar la sesión, los roles y los guards ya existentes (`SessionStore`, `authGuard`,
  `adminGuard`) sin duplicar esa lógica en el componente de navegación.

## Functional Requirements

- FR-01: El sistema SHALL mostrar un sidebar de navegación fijo a la izquierda en las pantallas
  `/discover`, `/murals/new` y `/moderation`.
- FR-02: El sidebar SHALL mostrar el logo de Paretto en su parte superior.
- FR-03: El sidebar SHALL listar los ítems de menú "Descubrir" (→ `/discover`) y "Cargar mural"
  (→ `/murals/new`), cada uno con su ícono.
- FR-04: El sidebar SHALL mostrar el ítem de menú "Moderación" (→ `/moderation`) únicamente cuando
  la sesión activa tiene el rol `Administrator`.
- FR-05: El sidebar SHALL resaltar visualmente el ítem de menú correspondiente a la ruta activa.
- FR-06: El sidebar SHALL mostrar, en su parte inferior, el nombre de usuario de la sesión activa y
  un botón para cerrar sesión, cuando hay una sesión iniciada.
- FR-07: El sidebar SHALL mostrar, en su parte inferior, enlaces a "Iniciar sesión" (→ `/login`) y
  "Registrarse" (→ `/register`) cuando no hay una sesión iniciada.
- FR-08: El botón de cerrar sesión SHALL invocar el flujo de logout ya existente
  (`AuthService.logout()` seguido de la limpieza de `SessionStore`) y redirigir a `/login`.
- FR-09: El sidebar SHALL poder expandirse o contraerse manualmente mediante un control accionable
  tanto desde el propio sidebar como desde el navbar (mismo estado, un único control lógico).
- FR-10: En resoluciones de ancho ≥ 992px, el sidebar SHALL iniciar en estado expandido (logo +
  íconos + etiquetas de texto).
- FR-11: En resoluciones de ancho < 992px, el sidebar SHALL iniciar en estado colapsado (logo +
  íconos, sin etiquetas de texto), permitiendo expandirse manualmente.
- FR-12: El sistema SHALL mostrar un navbar superior, de un color distinto al del sidebar, en las
  mismas pantallas donde se muestra el sidebar (`/discover`, `/murals/new`, `/moderation`).
- FR-13: El navbar SHALL mostrar un texto que identifique la pantalla/ruta activa.
- FR-14: El navbar SHALL mostrar un ícono de expandir/contraer que, al hacer clic, alterna el
  estado del sidebar descrito en FR-09.

## Non-Functional Requirements

- NFR-01: El control de expandir/contraer (en el sidebar y en el navbar) SHALL ser operable por
  teclado y exponer un `aria-label` que describa la acción disponible ("Expandir menú" / "Contraer
  menú"), para no introducir un control inaccesible en cada pantalla de la aplicación.

## Acceptance Criteria

- AC-01: WHEN un usuario navega a `/discover`, `/murals/new` o `/moderation`, THE sistema SHALL
  renderizar el sidebar y el navbar de contexto envolviendo el contenido de la pantalla (FR-01,
  FR-12).
- AC-02: WHEN un usuario navega a `/login` o `/register`, THE sistema SHALL NOT renderizar el
  sidebar ni el navbar (FR-01, FR-12).
- AC-03: WHEN el sidebar se renderiza, THE sidebar SHALL mostrar el logo de Paretto en su parte
  superior (FR-02).
- AC-04: WHEN el sidebar se renderiza, THE sidebar SHALL mostrar los ítems de menú "Descubrir" y
  "Cargar mural", cada uno con su ícono (FR-03).
- AC-05: WHEN la sesión activa no tiene el rol `Administrator`, THE sidebar SHALL NOT mostrar el
  ítem "Moderación" (FR-04).
- AC-06: WHEN la sesión activa tiene el rol `Administrator`, THE sidebar SHALL mostrar el ítem
  "Moderación" (FR-04).
- AC-07: WHEN el usuario se encuentra en una de las rutas con layout, THE sidebar SHALL resaltar
  visualmente el ítem de menú correspondiente a esa ruta (FR-05).
- AC-08: WHEN hay una sesión activa, THE sidebar SHALL mostrar el nombre de usuario de la sesión y
  un botón de cerrar sesión en su parte inferior (FR-06).
- AC-09: WHEN no hay sesión activa, THE sidebar SHALL mostrar los enlaces "Iniciar sesión" y
  "Registrarse" en lugar del label de usuario y el botón de cerrar sesión (FR-07).
- AC-10: WHEN el usuario hace clic en "Cerrar sesión", THE sistema SHALL invalidar la sesión activa
  y redirigir a `/login` (FR-08).
- AC-11: WHEN el usuario hace clic en el ícono de expandir/contraer, ya sea en el sidebar o en el
  navbar, THE sidebar SHALL alternar entre expandido y colapsado (FR-09, FR-14).
- AC-12: WHEN el ancho de la ventana es mayor o igual a 992px al cargar una pantalla con layout,
  THE sidebar SHALL iniciar expandido (FR-10).
- AC-13: WHEN el ancho de la ventana es menor a 992px al cargar una pantalla con layout, THE
  sidebar SHALL iniciar colapsado (FR-11).
- AC-14: WHEN el navbar se renderiza, THE navbar SHALL mostrar un texto que identifique la
  pantalla/ruta activa (FR-13).
- AC-15: IF un visitante sin sesión hace clic en el ítem "Cargar mural", THEN THE sistema SHALL
  redirigirlo a `/login` (comportamiento ya provisto por `authGuard`, reutilizado sin modificarlo)
  (FR-01, FR-03).

## Out of Scope

- Persistir el estado expandido/colapsado entre sesiones o recargas de página — se recalcula según
  el breakpoint en cada carga (FR-10/FR-11).
- Modo oscuro / temas alternativos — ya excluido en el PRD de FEAT-003.
- Notificaciones, buscador, breadcrumbs o cualquier otro elemento en el navbar más allá del título
  de la pantalla activa y el ícono de expandir/contraer.
- Una card lateral de invitación a registrarse en `/discover` — se descarta a favor de que el
  propio sidebar (ítems + CTA de "Iniciar sesión"/"Registrarse") cumpla ese rol, según lo definido
  con el usuario en DEFINE.
- Cambiar la lógica de negocio de autenticación, logout o los guards existentes (`authGuard`,
  `adminGuard`, `rootRedirectGuard`) — el sidebar los reutiliza, no los modifica.
- Sub-menús anidados o agrupación de ítems — el menú es una lista plana de hasta 3 ítems
  (Descubrir, Cargar mural, Moderación).

## Risks and Mitigations

- **Riesgo:** hardcodear rutas y condiciones de rol dentro del componente de sidebar, duplicando la
  fuente de verdad que hoy vive en `app.routes.ts` (`authGuard`, `adminGuard`).
  **Mitigación:** el sidebar deriva qué ítems mostrar leyendo `SessionStore` (`isAuthenticated()`,
  `user()?.role`) — la misma fuente que ya usan los guards — sin reimplementar su lógica de
  autorización. Se define en PLAN.
- **Riesgo:** envolver las pantallas con un layout compartido puede requerir reestructurar
  `app.routes.ts` (rutas hijas de un componente shell), tocando las 5 rutas ya existentes.
  **Mitigación:** el cambio es de estructura de ruteo, no de comportamiento de los guards; se
  verifica reutilizando y extendiendo `app.routes.spec.ts` en vez de reescribirlo desde cero.
- **Riesgo:** el botón "Cerrar sesión" del sidebar es la primera vez que algo en la UI invoca
  `AuthService.logout()` — hoy solo existe el método, sin ningún llamador.
  **Mitigación:** PLAN debe reutilizar `AuthService.logout()` tal cual está, sin reimplementar la
  llamada HTTP ni la limpieza de `SessionStore`.

## Dependencies

- `SessionStore` (`features/auth/state/session.store.ts`) para `isAuthenticated()`, `user()` y
  `role`.
- `AuthService.logout()` (`features/auth/data/auth.service.ts`).
- `LogoComponent` (`shared/logo/`), ya usado en login/register.
- Tokens de color de FEAT-002 (`--ant-primary-color` coral, `--app-color-secondary` navy) para
  diferenciar el color del navbar del color del sidebar.
- `ng-zorro-antd` (ya en el stack) para los componentes de layout/menú/íconos.
- Rutas y guards existentes en `app.routes.ts` (`authGuard`, `adminGuard`, `rootRedirectGuard`) —
  reutilizados, no modificados.
