# Verify FEAT-005: Geolocalización funcional y refetch de murales por área en /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-005 |
| PRD | docs/daw/prd/prd-FEAT-005.md |
| Spec | docs/daw/specs/spec-FEAT-005.md |
| Date | 2026-08-25 |
| Rondas | 1 |

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  module-verifier — Verificación de FEAT-005 (ticket completo)│
│  Geolocalización y refetch de murales por área en /discover │
├─────────────────────────────────────────────────────────┤
│                                                            │
│  Trazabilidad PRD → Código → Tests (F-VER-01): 8/8 AC       │
│    ✅ AC-01, AC-02 → recentrado reactivo del mapa            │
│       (discovery-map.component.spec.ts)                       │
│    ✅ AC-03 → pin distintivo del visitante                    │
│    ✅ AC-04 → botón "Buscar en esta área" aparece tras         │
│       mapMoved                                                  │
│    ✅ AC-05 → click consulta el centro actual y reemplaza        │
│       resultados                                                  │
│    ✅ AC-06 → resultados previos visibles durante la carga         │
│    ✅ AC-07 → resultado vacío sin ampliar radio                     │
│    ✅ AC-08 → error genérico, resultados previos preservados         │
│                                                                         │
│  Tareas del spec (F-VER-02): 4/4 bloques implementados tal como         │
│  se describieron, sin desvíos                                            │
│                                                                             │
│  Tests requeridos por el spec (F-VER-06): 15/15 existen y pasan            │
│  (Block 1: 4/4, Block 2: 2/2, Block 3: 4/4, Block 4: 5/5)                    │
│                                                                                 │
│  Coverage (F-VER-03), medido con @vitest/coverage-v8:                          │
│    discovery-map.component.ts:  94.93% stmts / 92.68% branch /                  │
│      94.11% funcs / 95.77% lines                                                  │
│    discovery-page.component.ts: 96.29% stmts / 90.90% branch /                   │
│      93.75% funcs / 96.00% lines                                                   │
│    ✅ Los tres umbrales (≥80%) superados con holgura en ambos archivos              │
│                                                                                        │
│  Sad-path tests (F-VER-04): cubiertos en las rutas alcanzables por UI                  │
│    (error de red, center() → null, resultado vacío)                                     │
│                                                                                            │
│  Lint / typecheck (F-VER-05): ✅ tsc --build --noEmit y ng lint sin errores               │
│                                                                                               │
│  Aislamiento del ticket: ✅ único código de producción tocado es                             │
│  `frontend/src/app/features/discovery/ui/` — sin cambios de backend ni de                     │
│  `api-client.generated.ts`                                                                       │
│                                                                                                       │
│  Suite completa: 232/233 (133 frontend + 99 backend). El único fallo es                              │
│  `sidebar.component.spec.ts` (ajeno a FEAT-005, componente removido por el                             │
│  usuario fuera de DAW, confirmado explícitamente — no se toca en este ticket)                            │
│                                                                                                               │
│  ─────────────────────────────────────────────────────                                                     │
│  Verdict: PASSED                                                                                              │
│  FAILs: 0 | WARNs: 3 (no bloqueantes) | PASSes: 27                                                             │
└─────────────────────────────────────────────────────────┘
```

## WARNs (no bloqueantes)

1. **3 ramas defensivas sin test directo**: `applyCenter()` (`!this.map`), `handleMapMoved()`
   (`!this.map`) y `searchThisArea()` (`!center`) — todas guardas que el propio spec documenta como
   inalcanzables por diseño en el flujo normal de UI (ej. `handleMapMoved()` nunca corre antes de que
   `this.map` exista, porque los listeners se enganchan después de crearlo). No corresponden a un
   input inválido que un usuario real pueda disparar. No bajan el branch coverage por debajo del
   mínimo (90.9–92.7%).
2. **Evidencia TDD desigual entre bloques**: Block 3 y 4 citan la aserción/error de compilación
   específico que rompía antes de implementar; Block 1 y 2 solo dan una razón genérica
   ("rojo→verde confirmado"). Todos los conteos de tests fueron verificados contra el diff real de
   cada commit y coinciden exactamente — no hay evidencia inventada, solo menos detalle narrativo en
   los primeros dos bloques.
3. Nota operativa (no es un gap de este ticket): el proyecto no tiene `@vitest/coverage-v8` como
   devDependency, así que medir coverage requiere instalarlo temporalmente. Se instaló, se midió y se
   desinstaló sin dejar rastro en el repo — valdría la pena agregarlo de forma permanente en un
   ticket aparte si se quiere que `daw-test`/CI puedan medir coverage de rutina.

## Excepción de proceso documentada

**Hallazgo:** `frontend/src/app/core/layout/ui/sidebar.component.spec.ts` tiene un test roto
(`el botón de expandir/contraer llama a layoutStore.toggle() y cambia su aria-label`) porque el
elemento `data-testid="sidebar-toggle"` ya no existe en `sidebar.component.html`.

**Por qué no bloquea FEAT-005:** el archivo no fue tocado por ninguno de los 4 bloques de este
ticket (confirmado con `git diff main..HEAD --stat` y `git diff main -- sidebar.component.spec.ts`,
sin diferencias). El usuario confirmó explícitamente en esta sesión que removió el componente
correspondiente fuera de DAW, y que ese test/componente no se toca en este ticket.

**Estado:** pendiente de que el usuario decida cuándo y cómo limpiar el test huérfano (fuera del
alcance de FEAT-005).

## Archivos modificados por el ticket

- `frontend/src/app/features/discovery/ui/discovery-map.component.ts`
- `frontend/src/app/features/discovery/ui/discovery-map.component.spec.ts`
- `frontend/src/app/features/discovery/ui/discovery-page.component.ts`
- `frontend/src/app/features/discovery/ui/discovery-page.component.html`
- `frontend/src/app/features/discovery/ui/discovery-page.component.spec.ts`
