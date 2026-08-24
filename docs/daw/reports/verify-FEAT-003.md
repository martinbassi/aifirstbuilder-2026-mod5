# VERIFY FEAT-003: Rediseño visual de login/register (card centrada)

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| PRD | docs/daw/prd/prd-FEAT-003.md |
| Spec | docs/daw/specs/spec-FEAT-003.md |
| Tier | FEATURE |
| Date | 2026-08-24 |
| Rondas | 1 (con 1 excepción documentada y aceptada por el usuario, sin corrective loop) |

## Acceptance criteria (F-VER-01)

| AC | Criterio | Código | Test automatizado | Confirmación adicional |
|----|----------|--------|--------------------|-------------------------|
| AC-01 | Login envuelto en split-screen (`app-auth-card`) | `login-form.component.html` | ✅ `login-form.component.spec.ts` | — |
| AC-02 | Register envuelto en split-screen (`app-auth-card`) | `register-form.component.html` | ✅ `register-form.component.spec.ts` | — |
| AC-03 | Panel de marca: wordmark, mensaje, imagen | `auth-card.component.html` | ✅ `auth-card.component.spec.ts` | — |
| AC-04 | Panel de formulario, ancho máx. ~420px | `auth-card.component.css:107-109` (`width: min(410px, 80%)`) | ⚠️ Test verifica proyección de contenido, no el ancho computado (jsdom no calcula layout) | ✅ **Confirmado visualmente** — desktop 1440px, panel de formulario no excede el ancho esperado (screenshot) |
| AC-05 | Colapso responsive: un solo panel <700px | `auth-card.component.css:139-156` (`@media (max-width: 700px)`) | — (Vitest/jsdom no evalúa media queries, documentado en el spec) | ✅ **Confirmado visualmente por el usuario** — panel de marca desaparece, formulario ocupa 100% del ancho por debajo de 700px |
| AC-06 | Login y register comparten ancho/tratamiento visual | `auth-form.css` compartido | ✅ Test de equivalencia estructural (`.auth-header`/`.auth-form`/`.auth-submit-button`) | ✅ **Confirmado visualmente** — screenshots de `/login` y `/register` a 1440px, mismo layout y estilo |
| AC-07 | Ícono de Google visible en el botón | `NzIconModule` agregado a ambos formularios | ✅ Test verifica `role="img"` + clase `anticon-google` | ✅ **Confirmado visualmente** — ícono "G" coral renderiza correctamente en `/login` y `/register` (zoom de screenshot) |

## Spec tasks (F-VER-02/F-VER-06)

- ✅ Block 1 — AuthCardComponent: 3/3 tests requeridos implementados y en verde.
- ✅ Block 2 — Ícono Google/CSS compartido/imports muertos: 7/7 tareas de test requeridas
  implementadas y en verde.

## Coverage (F-VER-03)

`@vitest/coverage-v8` no está instalado en el proyecto — no hay reporte automatizado disponible.
Evaluado por revisión línea por línea (daw-module-verifier): cobertura funcional prácticamente
completa del código modificado, con una excepción conocida y aceptada — `loginWithGoogle()`/
`registerWithGoogle()` (no-ops, placeholders de Google OAuth, PRD Out of Scope) no tienen test que
las invoque. Riesgo bajo: son funciones vacías documentadas, no lógica de negocio.

## Sad paths (F-VER-04)

No aplica lógica de negocio nueva en este ticket (componentes presentacionales + wiring de ícono).
Sin gaps identificados.

## Calidad (F-VER-05, W-VER-01, W-VER-03)

- ✅ `tsc --build --noEmit`: sin errores.
- ✅ `ng lint`: 0 errores dentro de `features/auth/` (1 error ajeno a este ticket en
  `discovery-map.component.ts`, cambio suelto sin commitear de otro trabajo, no contado).
- ✅ Sin código muerto ni imports sin usar en el scope del ticket.
- ⚠️ Drift menor de Prettier en `auth-card.component.css`/`.html` (saltos de línea extra, `<br>` sin
  autocierre) — cosmético, no bloqueante.
- ⚠️ `auth-card.component.css:42` (`.logo span { color: #0d2348 }`) con hex sin token, aunque
  coincide con `--app-color-secondary` — deuda menor de consistencia con ADR-006.

## Excepción documentada: evidencia TDD (F-VER — proceso)

**Hallazgo:** la estructura HTML/CSS split-screen de `AuthCardComponent` (Block 1) ya existía en el
working tree antes de que el spec y sus tests actuales existieran — proviene de un intento de CODE
de una sesión anterior que se abandonó por divergir del spec original (nz-card). El spec actual
("spec loop 1") se escribió explícitamente para describir ese código ya construido, en vez de
generarlo primero por TDD estricto.

**Disposición: aceptado como documentado, sin corrective loop.** Motivo:
- Esta secuencia (código → spec → tests) fue la premisa explícita y declarada del spec loop 1, no un
  hallazgo oculto: el propio `spec-FEAT-003.md` lo dice en su Summary ("reconstruido desde el código
  ya escrito en disco").
- Pasó por el loop correctivo que corresponde (PLAN↔DEFINE) en una sesión anterior: DEFINE
  re-validó el PRD, PLAN re-escribió el spec con threat modeling y arch-audit, y el usuario aprobó
  esa premisa en ambos gates.
- Dentro de los 2 bloques efectivamente implementados en la sesión de CODE de este ticket, sí hubo
  evidencia roja→verde genuina y verificada de forma independiente por `daw-module-verifier`
  (Block 1: test de `carousel-dots`; Block 2: 4/12 tests fallando confirmados vía `git stash`,
  pasando a 12/12 tras el fix).
- Confirmado por el usuario en esta sesión: se acepta como documentado en vez de reabrir un
  corrective loop VERIFY→CODE para reconstruir con TDD estricto una estructura visual ya revisada y
  aprobada.

## Verificación visual manual (navegador)

Realizada en esta sesión con Claude in Chrome sobre `http://localhost:4200` (build de desarrollo):
- `/login` y `/register` a 1440×900: split-screen correcto, panel de marca + panel de formulario,
  mismo tratamiento en ambas pantallas (AC-04, AC-06).
- Ícono de Google renderizado correctamente en ambos botones, confirmado con zoom sobre el
  screenshot (AC-07).
- Colapso responsive por debajo de 700px confirmado directamente por el usuario en su propio
  navegador (AC-05) — la herramienta de resize de este entorno no logró emular el viewport angosto
  de forma mecánica (`window.innerWidth` no cambiaba pese al resize reportado como exitoso).

─────────────────────────────────────────────────────────
**Total: 7/7 AC cubiertos (4 con confirmación visual manual), 1 excepción de proceso documentada y
aceptada, 3 WARN no bloqueantes**
**Result: PASSED**
**Next:** avanzar a RELEASE
