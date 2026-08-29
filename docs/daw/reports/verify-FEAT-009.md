# Verify FEAT-009: Migrar búsqueda de murales cercanos a geography + NetTopologySuite

| Field | Value |
|-------|-------|
| Ticket | FEAT-009 |
| PRD | docs/daw/prd/prd-FEAT-009.md |
| Spec | docs/daw/specs/spec-FEAT-009.md |
| Date | 2026-08-29 |
| Rondas | 2 (1 corrective loop) |

## Ronda 1 — BLOCKED

**FAIL:** AC-08/FR-08 (verificación de `docs/adr/adr-005-nearby-murals-haversine-sin-geography.md`)
no tenía un test automatizado — el spec (Block 3, "Required tests") la listaba como test versionado,
pero se implementó solo como un comando de shell corrido a mano durante PLAN/CODE. El contenido del
ADR era correcto; el gap era de trazabilidad/repetibilidad de la prueba.

**Causa raíz:** al dispachar Block 3, el orquestador le indicó explícitamente al implementador que
esa verificación "no hacía falta implementarla", contradiciendo lo que el propio spec pedía.

**WARNs no bloqueantes de esta ronda:**
- AC-06 (índice espacial) verificado indirectamente — el test asignado (`AppDbContextFactory_builds_a_model_that_supports_the_Point_column...`) valida el modelo de diseño, no consulta `sys.spatial_indexes` contra la base real.
- `Down()` de la migración sin test — consistente con el patrón preexistente del proyecto (ninguna de las 4 migraciones lo tiene).
- Riesgo residual acotado de tests contra SQL Server real corriendo en paralelo (mitigado con bases/GUID por test, no eliminado por diseño).

**Coverage medido (ronda 1):** 93.1% líneas / 86.0% ramas / 96.2% funciones sobre el código nuevo/modificado — supera el piso del 80% (F-VER-03).

## Loop correctivo (CODE)

Se agregó `backend/tests/Paretto.Api.Tests/AdrDocumentationTests.cs` (commit `7a1169f`): verifica
que el ADR contiene ≥1 ocurrencia de "Option B" y que el campo `Status` de su header contiene
"revisado". Tests re-confirmados (117 backend), SAST re-scan PASSED.

## Ronda 2 — PASSED

**Resolución del FAIL:** `AdrDocumentationTests.cs` verifica exactamente lo que AC-08 pide, corrido
en aislamiento (1/1) y en la suite completa (117/117). Contenido del ADR confirmado independientemente
(2 ocurrencias de "Option B", `Status` contiene "revisado" — no es un falso positivo).

### Trazabilidad PRD → Código → Tests (los 8 AC)

| AC | Implementado en | Test | Resultado |
|---|---|---|---|
| AC-01 | `GetNearbyMuralsQuery.cs` (`Location.Distance`) | `Results_are_ordered_ascending_by_DistanceKm`, `DistanceKm_reflects_the_meters_to_km_conversion_for_two_points_known_to_be_about_1km_apart` | ✅ PASA |
| AC-02 | `DiscoveryMappingConfig.cs`/`MuralMappingConfig.cs` (sin tocar, por diseño) | `DiscoveryControllerTests.cs`/`GetMuralByIdTests.cs` (lat≠lon distinguibles, no invertidos) | ✅ PASA |
| AC-03 | `CreateMuralCommand.cs` (`Mural.CreateLocation`) | `MuralPersistenceTests.cs` round-trip contra SQL Server real | ✅ PASA |
| AC-04 | Migración `Up()` paso 2 (backfill) | `Migration_..._backfills_Location_from_existing_Latitude_and_Longitude` | ✅ PASA |
| AC-05 | Migración `Up()` paso 2 (`geography::Point`) | `Migration_..._fails_explicitly_when_an_existing_row_has_out_of_range_latitude` | ✅ PASA |
| AC-06 | Migración `Up()` pasos 4-5 (índice espacial) | `AppDbContextFactory_builds_a_model_that_supports_the_Point_column...` | ⚠️ PASA — verificación indirecta (WARN, no bloqueante) |
| AC-07 | Eliminación de `GeoDistanceCalculator` | `Old_in_memory_distance_calculator_class_no_longer_exists_anywhere_in_the_backend_source_tree` | ✅ PASA |
| AC-08 | ADR-005 (sección "Revision") | `Adr005_reflects_the_revised_decision_to_adopt_geography` (nuevo, ronda 2) | ✅ PASA |

### Quality (verificación final)

- `dotnet build backend/Paretto.sln`: 0 errores, 0 advertencias.
- `dotnet test backend/Paretto.sln`: 117/117 passed.
- `grep -rn "GeoDistanceCalculator" backend/`: sin resultados.
- `IX_Murals_Status_Latitude_Longitude`: solo en migraciones históricas inmutables y en el `Down()` de rollback de Block 1 — no en código de producción activo.
- Coverage: sin cambios respecto a ronda 1 (93.1%/86.0%/96.2%), la adición de esta ronda es un archivo de test, no código de producción.

### WARNs finales (no bloqueantes, documentados)

1. AC-06 verificado indirectamente (smoke test de diseño, no consulta directa a `sys.spatial_indexes`).
2. `Down()` de la migración sin test — patrón preexistente del proyecto, no una regresión de este ticket.
3. Riesgo residual acotado de tests contra SQL Server real en paralelo — mitigado (bases/filas con GUID), no eliminado por diseño.

## Veredicto

```
┌─────────────────────────────────────────────────────────┐
│  /daw-verify-module FEAT-009 — PASSED (ronda 2)            │
├─────────────────────────────────────────────────────────┤
│  FAILs: 0 | WARNs: 3 | 8/8 AC trazados PRD→código→test      │
│  Result: PASSED                                              │
└─────────────────────────────────────────────────────────┘
```
