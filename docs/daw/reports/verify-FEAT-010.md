# Verify FEAT-010: Marcador del centro de búsqueda en el mapa de /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-010 |
| PRD | docs/daw/prd/prd-FEAT-005.md (PRD loop 1, FR-07/08/09, AC-09 a AC-12) |
| Spec | docs/daw/specs/spec-FEAT-010.md |
| Date | 2026-08-29 |
| Rondas | 2 (1 corrective loop) |

## Ronda 1 — BLOCKED

**FAIL:** coverage de `applySearchCenterMarker()` (`discovery-map.component.ts`) por debajo del 80%
(77.78% statements, 78.57% branches, medido aislado sobre el método). Faltaban tests para 2 ramas ya
descritas en el "ciclo de vida explícito" del spec (Block 2, pasos 3 y 4) pero no incluidas en su
lista de "Required tests": reposicionar un marcador ya existente ante otra búsqueda lejana, y
removerlo cuando `searchCenter` vuelve a `null`.

**WARN no bloqueante de esta ronda:** el propio spec cuenta "10 tests nuevos" en su sección "Final
verification" cuando en realidad son 9 tests `it()` genuinamente nuevos + la actualización de 11
aserciones ya existentes (el selector de marcadores de mural). No se corrige (el spec no se edita en
CODE/VERIFY) — queda anotado acá para no arrastrar el conteo erróneo.

**Coverage medido (ronda 1):** `geo-distance.util.ts` 100%/100%/100%; `discovery-page.component.ts`
(línea nueva) 100%; `applySearchCenterMarker()` 77.78%/78.57% (bajo el piso).

## Loop correctivo (CODE)

Se agregaron 2 tests a `discovery-map.component.spec.ts` (commit `2dc2da3`), verificados con
mutación deliberada (cada uno falla si se rompe su rama correspondiente, revertida después). Tests
re-confirmados (161 frontend), SAST re-scan PASSED.

## Ronda 2 — PASSED

**Resolución del FAIL:** coverage de `applySearchCenterMarker()` re-medido de forma independiente:
94.4% statements (17/18), 92.9% branches (13/14) — la única línea sin cubrir es el guard defensivo
`if (!this.map) return`, mismo patrón no testeado que ya tiene `applyCenter()` para el mismo caso,
no una rama de negocio nueva.

### Trazabilidad PRD → Código → Tests (los 4 AC nuevos)

| AC | Implementado en | Test | Resultado |
|---|---|---|---|
| AC-09 | `discovery-page.component.ts` (`lastSearchCenter` set en el callback `next`) + `applySearchCenterMarker()` | "tras una consulta exitosa, lastSearchCenter() queda seteado..." + "remueve el marcador... al pasar de lejano a cercano (AC-10/AC-09)" | ✅ PASA |
| AC-10 | `applySearchCenterMarker()`, rama `!farEnough` | "muestra solo el marcador de visitante cuando center y searchCenter están a <50m (AC-10)" + transición lejano→cercano | ✅ PASA |
| AC-11 | `applySearchCenterMarker()`, rama `farEnough` | "muestra ambos marcadores cuando center y searchCenter están a >=50m (AC-11)" | ✅ PASA |
| AC-12 | `discovery-page.component.ts` (callback `error`, no toca `lastSearchCenter`) | "si la consulta falla, lastSearchCenter() conserva su último valor válido (AC-12)" | ✅ PASA |

### Quality (verificación final)

- `dotnet`/`npx tsc --build --noEmit tsconfig.json`: sin errores.
- `npx ng lint`: sin errores.
- Suite completa del frontend: 161/161 passed (24 archivos), 4 corridas consecutivas.
- SAST: PASSED, re-scan incluido (0 vulnerabilidades, sin dependencias nuevas).

### WARN final (no bloqueante, documentado)

Flakiness intermitente en `discovery-page.component.spec.ts` (9-12 "Unhandled Rejection"/
"EnvironmentTeardownError" en algunas corridas de la suite completa, no en corridas aisladas) —
preexistente desde el commit `e35a86f` (Block 1 de este mismo ticket), causado aparentemente por un
leak de Observable compartido entre tests que usan `throwError`, no un bug de producción. No hace
fallar ningún test (161/161 se sostiene siempre). Recomendado: abrir un ticket de limpieza aparte
para investigarlo.

## Veredicto

```
┌─────────────────────────────────────────────────────────┐
│  /daw-verify-module FEAT-010 — PASSED (ronda 2)             │
├─────────────────────────────────────────────────────────┤
│  FAILs: 0 | WARNs: 1 | 4/4 AC trazados PRD→código→test        │
│  Result: PASSED                                                  │
└─────────────────────────────────────────────────────────┘
```
