# Verify Report FEAT-004: Sidebar de navegación global colapsable + navbar de contexto

| Field | Value |
|-------|-------|
| Ticket | FEAT-004 |
| PRD | docs/daw/prd/prd-FEAT-004.md |
| Spec | docs/daw/specs/spec-FEAT-004.md |
| Date | 2026-08-25 |
| Result | PASSED |

## Cross-verificación independiente

Ejecutada por un agente (`daw-module-verifier`) que no escribió el código, corriendo la suite
completa, coverage, lint y typecheck de forma independiente (no confía en los números reportados
durante CODE).

## Trazabilidad PRD → Código → Tests (F-VER-01)

Las 15 AC del PRD (AC-01 a AC-15) están implementadas y cada una tiene al menos un test que valida
comportamiento real (DOM, spies, navegación con router real, estado), no solo presencia superficial.
Detalle completo con archivo:línea por cada AC en el reporte del agente (ver historial de la sesión
DAW).

## Tareas del spec (F-VER-02/F-VER-06)

| Bloque | Tests requeridos | Tests en disco | Estado |
|---|---|---|---|
| Block 1 — LayoutStore | 3 | 3 | ✅ |
| Block 2 — SidebarComponent | 10 | 13 (10 + 3 extra) | ✅ |
| Block 3 — NavbarComponent | 4 | 5 (4 + 1 regresión TypeError) | ✅ |
| Block 4 — AppShellComponent | 2 | 2 | ✅ |
| Block 5 — Rutas | 6 | 15 (8 integración + 7 guards preexistentes) | ✅ |

El bug real encontrado durante CODE (`NavbarComponent.readActiveTitle()`, `TypeError` en la primera
activación de rutas lazy anidadas) sigue corregido: `node.snapshot?.data[...]` con optional chaining,
test de regresión en verde.

## Sad paths (F-VER-04)

✅ Logout con error de red, `data.title` ausente, navegación sin sesión, navegación con sesión sin
rol Administrator, y la primera activación de rutas lazy anidadas — todos cubiertos con tests
dedicados.

## Cobertura (F-VER-03) — medida sobre `core/layout/**`, `app.routes.ts`, `app.config.ts`

| Métrica | Valor | Umbral | Resultado |
|---|---|---|---|
| Statements | 100% (139/139) | ≥80% | ✅ |
| Lines | 100% (99/99) | ≥80% | ✅ |
| Functions | 100% (30/30) | ≥80% | ✅ |
| Branches | 98.21% (55/56) | ≥80% | ✅ (1 branch sin cubrir, ver WARN) |

## Calidad

✅ Lint (`npm run lint`): 0 errores — "All files pass linting"
✅ Typecheck (`npx tsc --build --noEmit`): 0 errores
✅ Sin código muerto, sin imports sin usar
✅ Sin tests frágiles (sin timestamps/IDs hardcodeados, aislamiento de `TestBed`/`sessionStorage`
correcto)
✅ SAST: PASSED, 0 hallazgos (docs/daw/security/sast-FEAT-004.md)

## Suite completa (medida independientemente)

- Frontend: 119/119 tests, 22 archivos, 0 fallos (`npx ng test --watch=false`)
- Backend: 99/99 tests, 0 fallos (`dotnet test`)

## Warnings (no bloqueantes)

1. **Branch de cobertura sin ejercitar** — `sidebar.component.html:52`
   (`sessionStore.user()?.username`): la rama "user() undefined" del optional chaining no se
   ejercita porque el footer autenticado solo se renderiza con `isAuthenticated()===true`, momento
   en el que `user()` siempre está poblado en la práctica. Riesgo real bajo — no se corrige, queda
   documentado.
2. **Evidencia TDD con justificación genérica** — los commits de cada bloque describen el "antes"
   con una causa genérica (módulo inexistente / stub sin template / `TypeError`) en vez de citar la
   aserción exacta que rompía por test individual. Los conteos de tests declarados en cada commit
   coinciden exactamente con lo que hay en disco (evidencia cruzada de que no están inventados), pero
   es más débil que el estándar ideal de "aserción específica por test". No bloqueante.

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  /daw-verify-module FEAT-004 — PASSED                     │
├─────────────────────────────────────────────────────────┤
│  Total: 24 passed, 0 failed, 2 warnings                    │
│  Result: PASSED                                             │
│  Next: aprobación del usuario → RELEASE                       │
└─────────────────────────────────────────────────────────┘
```
