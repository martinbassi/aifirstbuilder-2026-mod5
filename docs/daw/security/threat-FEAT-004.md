# Threat Model FEAT-004: Sidebar de navegación global colapsable + navbar de contexto

| Field | Value |
|-------|-------|
| Ticket | FEAT-004 |
| Spec | docs/daw/specs/spec-FEAT-004.md |
| Date | 2026-08-25 |

## Componentes analizados

`LayoutStore` (Block 1), `SidebarComponent` (Block 2), `NavbarComponent` (Block 3),
`AppShellComponent` (Block 4), restructuración de `app.routes.ts` (Block 5).

## Trust boundaries declaradas

1. **Browser UI (Sidebar/Navbar) ↔ `SessionStore`** — mismo trust zone (cliente), pero
   `SessionStore` es el límite entre el almacenamiento crudo del navegador (`sessionStorage`) y el
   estado de la app. Este ticket agrega **consumidores de solo lectura** (Sidebar lee
   `isAuthenticated()`/`user()`) — no agrega escritura nueva ni cambia el mecanismo de persistencia
   ya establecido en FEAT-001a/b.
2. **Browser UI ↔ Backend API** (vía `AuthService.logout()` → `AuthClient` → HTTPS) — boundary
   cliente/servidor ya existente; este ticket agrega el **primer llamador de UI** a
   `AuthService.logout()` (hasta ahora el método existía sin ningún caller), sin crear un boundary
   nuevo.
3. **Guards client-side (`authGuard`/`adminGuard`, conveniencia de UX) ↔ autorización server-side
   (`[Authorize(Roles=...)]`)** — boundary ya documentada en `app.routes.ts` (comentario de
   FEAT-001c: "esto es solo UX... la autorización real es server-side, re-verificada en cada
   request"). Block 5 de este spec **mueve** las rutas guardadas a children de un shell, sin tocar
   los guards en sí — el riesgo es que la restructuración accidentalmente debilite este boundary por
   error humano, no por diseño (ver Riesgo 1).

## Datos sensibles (F-TM-05)

- **`username`** (PII, baja sensibilidad — el propio nombre de cuenta del usuario, ya visible para
  él en otras pantallas): se muestra en el footer del sidebar (FR-06). **Ya clasificado y mitigado**
  en los threat models de FEAT-001a/b (persistencia en `sessionStorage`, riesgo R5 aceptado con CSP
  como control compensatorio). Este ticket **no introduce almacenamiento ni transmisión nueva** de
  este dato — solo lo lee y lo muestra, reutilizando el mismo signal ya cubierto (F-TM-07: sin
  cambios al cifrado/transporte existente, no aplica requisito nuevo).
- **`role`** (mismo tratamiento, ya clasificado en el threat model de FEAT-001c): se lee para decidir
  si mostrar el ítem "Moderación" — de nuevo, solo lectura, sin nuevo almacenamiento.

## STRIDE por componente

**`LayoutStore`** — Spoofing: N/A. Tampering: un usuario podría forzar el signal vía devtools
(cosmético, sin impacto de seguridad). Repudiation/Info Disclosure/DoS/Elevation: N/A (estado
booleano trivial, sin datos sensibles, sin I/O).

**`SidebarComponent`** — Spoofing: `SessionStore.user()?.role` es client-asserted; un usuario podría
falsificarlo en devtools para que se muestre el ítem "Moderación". Tampering: N/A adicional.
Repudiation: N/A (el logout no requiere audit log nuevo — `AuthService.logout()` ya maneja esto en
el backend). Information Disclosure: muestra el propio username del usuario — no es una fuga (dato
que el usuario ya conoce de sí mismo). DoS: N/A. Elevation of Privilege: ver Riesgo 2 abajo.

**`NavbarComponent`** — todas las categorías: N/A. El título viene de `data.title` estático en
`app.routes.ts` (config, no input de usuario), sin interpolación de datos externos — Angular además
escapa bindings de texto por defecto, sin riesgo de XSS.

**`AppShellComponent`** — sin superficie propia más allá de la de sus hijos (Sidebar/Navbar).

**Restructuración de rutas (Block 5)** — Elevation of Privilege: ver Riesgo 1 abajo (el riesgo real
de este ticket no es de diseño sino de implementación del refactor).

## Riesgos

| # | Riesgo | STRIDE | Likelihood | Impact | Mitigación |
|---|---|---|---|---|---|
| 1 | Al mover `/murals/new` y `/moderation` a children del shell (Block 5), el refactor omite o altera por error `canActivate: [authGuard]` / `[authGuard, adminGuard]`, debilitando el boundary #3 | Elevation of Privilege | Medium (error humano en refactor) | Medium (expone UI protegida en el cliente; los endpoints reales del backend siguen `[Authorize]`, así que no hay bypass funcional, pero sí una regresión de UX/defensa en profundidad) | Ya folded en el spec: Block 5 declara explícitamente que los `canActivate` se preservan sin cambios por child, y sus tests requeridos re-verifican el comportamiento de `authGuard`/`adminGuard` post-restructuración (redirect a `/login` y a `/`) como regresión. Además, `daw-arch-auditor` audita Block 5 en CODE. |
| 2 | Un usuario sin rol `Administrator` falsifica `SessionStore.user().role` vía devtools y ve el ítem "Moderación" en el sidebar | Elevation of Privilege | Low | Low (solo afecta qué se **muestra**; la ruta sigue protegida por `adminGuard` client-side y, más importante, el endpoint real por `[Authorize(Roles="Administrator")]` server-side) | **Riesgo aceptado** — es el mismo patrón que `adminGuard` ya acepta explícitamente en `app.routes.ts` desde FEAT-001c ("esto es solo UX... nunca derivado de lo que el cliente envía"). Este ticket no cambia ese modelo de confianza, solo agrega otro consumidor de lectura de la misma señal ya aceptada. **Aceptado por:** el usuario del proyecto (mismo criterio que FEAT-001c). **Condición de revisión:** si en el futuro se agrega un endpoint que confíe en el rol reportado por el cliente sin re-verificarlo server-side, revisar de nuevo. |
| 3 | `SidebarComponent` es el primer llamador de UI a `AuthService.logout()`; si el observable falla (red caída, 5xx), el usuario podría quedar con la sesión "colgada" visualmente | Denial of Service (parcial, UX) | Low | Low | Ya folded en el spec (Block 2, error handling + test dedicado agregado en la validación de spec): la limpieza de `SessionStore` y la redirección a `/login` ocurren igual, sea cual sea el resultado de la llamada al backend. |

No hay riesgos CRITICAL ni HIGH. Los tres riesgos identificados ya tienen su mitigación reflejada en
el spec (Riesgo 1 y 3) o son un riesgo aceptado con las tres condiciones de F-TM-04 completas
(Riesgo 2).

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                           │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Attack surfaces identified: 5 (LayoutStore, Sidebar,     │
│    Navbar, AppShell, restructuración de rutas)             │
│  Trust boundaries declared: 3                              │
│                                                          │
│  Risks:                                                    │
│    🟡 MEDIUM: refactor de rutas debilita authGuard/         │
│       adminGuard por error humano — Mitigación: canActivate │
│       preservado explícitamente + tests de regresión ya en  │
│       el spec (Block 5) + arch-audit en CODE                │
│    🟢 LOW: spoofing client-side de `role` muestra "Moderación"│
│       sin autorizar nada real — Riesgo aceptado (mismo       │
│       criterio que adminGuard desde FEAT-001c)               │
│    🟢 LOW: logout falla en red, sesión visualmente colgada — │
│       Mitigación: limpieza local + redirect ocurren igual     │
│                                                          │
│  Mitigations to fold into the spec:                        │
│    (ya incorporadas — ver spec-FEAT-004.md Block 2, 3 y 5)   │
│                                                          │
│  ─────────────────────────────────────────────────────   │
│  Risks: C:0 H:0 M:1 L:2                                    │
│  Report: docs/daw/security/threat-FEAT-004.md              │
└─────────────────────────────────────────────────────────┘
```
