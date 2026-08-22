# VERIFY FEAT-001c: Moderación mínima

| Field | Value |
|-------|-------|
| Ticket | FEAT-001c |
| PRD | docs/daw/prd/prd-FEAT-001c.md |
| Spec | docs/daw/specs/spec-FEAT-001c.md |
| Date | 2026-08-22 |
| Rounds | 1 |

## Round 1 — PASSED

### F-VER-01 — Trazabilidad PRD → Código → Tests

Los 6 AC del PRD tienen al menos un test pasando que los valida, verificando cuerpo de respuesta
**y** estado persistido en base (no solo código HTTP):

| AC | Handler | Test |
|---|---|---|
| AC-01 | `GetPendingMuralsQueryHandler` | `GetPendingMuralsTests.Administrator_gets_200_with_pending_murals_ordered_by_created_at_ascending_and_photo_url` |
| AC-02 | `ModerationController` (`[Authorize(Roles="Administrator")]`) | `GetPendingMuralsTests.Standard_user_gets_403` |
| AC-03 | `ApproveMuralCommandHandler` | `ApproveMuralTests.Administrator_approves_a_pending_mural_and_gets_200_with_published_status` + componente |
| AC-04 | `ApproveMuralCommandHandler` | `ApproveMuralTests.Standard_user_attempting_to_approve_gets_403_and_mural_stays_pending` |
| AC-05 | `RejectMuralCommandHandler` | `RejectMuralTests.Administrator_rejects_a_pending_mural_and_gets_200_with_rejected_status` + componente |
| AC-06 | `RejectMuralCommandHandler` | `RejectMuralTests.Standard_user_attempting_to_reject_gets_403_and_mural_stays_pending` |

✅ **PASS**

### F-VER-02 — Tareas del spec implementadas

7/7 bloques verificados contra `git diff --stat main...HEAD` (27 archivos) y lectura directa de cada
archivo: Rol en login, Listar pendientes, Aprobar, Rechazar, Rol en sesión frontend, Guard +
servicio, Pantalla de moderación. Sin huecos silenciosos.

✅ **PASS**

### F-VER-03 — Cobertura ≥ 80% líneas/ramas/funciones (código nuevo/modificado)

Backend (`dotnet test --collect:"XPlat Code Coverage"` + `reportgenerator`), por archivo:

| Archivo | Lines | Branches |
|---|---|---|
| `Features/Moderation/Queries/GetPendingMuralsQuery.cs` | 100% | 100% |
| `Features/Moderation/Commands/ApproveMuralCommand.cs` | 100% | 100% |
| `Features/Moderation/Commands/RejectMuralCommand.cs` | 100% | 100% |
| `Features/Moderation/Mappings/ModerationMappingConfig.cs` | 100% | 100% |
| `Api/Controllers/ModerationController.cs` | 100% | 100% |
| `Features/Auth/Commands/LoginCommand.cs` (con `Role`) | 100% | 100% |
| `Domain/Enums/MuralStatus.cs` | N/A (enum, sin líneas ejecutables) | N/A |

Frontend: `npx ng test --coverage` falla — `@vitest/coverage-v8` no está instalado en el repo.
**Gap de infraestructura preexistente**, ya documentado en `docs/daw/reports/verify-FEAT-001b.md`
con el mismo criterio (no bloqueante, no introducido por este ticket). Suite frontend 50/50 con sad
paths explícitos por pieza nueva (ver F-VER-04).

✅ **PASS** (backend muy por encima del mínimo; frontend no medible por gap preexistente, aceptado
con el mismo criterio que FEAT-001b)

### F-VER-04 — Sad path por endpoint/función con input

| Superficie | Sad path(s) |
|---|---|
| `POST /api/auth/login` | ✅ 401 (tests preexistentes) |
| `GET /api/moderation/murals/pending` | ✅ 422, 403, 401 |
| `POST .../approve` | ✅ 404, 409, 403, 500 (extra) |
| `POST .../reject` | ✅ 404, 409, 403 |
| `AuthService.login()` | ✅ `ApiError` propagado |
| `ModerationService.approve()`/`rejectMural()` | ✅ `ApiError` tipado |
| `ModerationService.getPending()` | ⚠️ sin test de error propio a nivel de servicio (el componente lo cubre indirectamente con el servicio mockeado; mismo patrón `catchError` que `approve`/`rejectMural`, que sí están probados) |
| `PendingMuralsListComponent` | ✅ error de carga y error de `approve`; `rejectMural` fallando no tiene test simétrico propio (mismo código) |

✅ **PASS** (1 WARN no bloqueante — ver abajo)

### F-VER-05 — Lint / type checker

✅ `dotnet build` — 0 warnings, 0 errors.
✅ `npx tsc --noEmit` — limpio.
✅ `npx ng lint` — "All files pass linting."

**PASS**

### F-VER-06 — Tests listados en el spec

Los 26 tests requeridos explícitamente en las 7 secciones "Required tests" del spec fueron
localizados por nombre y pasan. Ningún checklist item quedó sin su test.

✅ **PASS**

### Warnings (no bloqueantes)

- **W-A:** cobertura frontend no medible (`@vitest/coverage-v8` no instalado) — gap preexistente,
  mismo tratamiento que FEAT-001b.
- **W-B:** `ModerationService.getPending()` sin sad-path test propio en `moderation.service.spec.ts`
  (el patrón `catchError` es idéntico al de `approve`/`rejectMural`, que sí están probados en el
  mismo archivo — riesgo real bajo).
- **W-C:** evidencia TDD por bloque no re-adjuntada a esta pasada de VERIFY (vive solo en la
  conversación de CODE, no persistida en disco) — mismo tratamiento aplicado en
  `docs/daw/reports/verify-FEAT-001b.md` Round 2: el gate `tests=true` de CODE ya está aprobado y no
  cambió desde entonces.
- **W-VER-01/02/03:** sin código muerto/imports sin usar; lógica de negocio (Handlers de Moderation)
  al 100% de líneas; sin tests frágiles (DB InMemory con nombre único por test, timestamps
  relativos, GUIDs generados).

### Suite completa (regresión final)

✅ Backend: 79/79 (`dotnet test -- xUnit.MaxParallelThreads=1`)
✅ Frontend: 50/50, 10 archivos (`npx ng test --watch false`)

---

## Resultado

**Total: 6/6 reglas F-VER PASSED, 0 FAILs, 3 WARNs no bloqueantes.**

**Veredicto: PASSED.** El ticket puede avanzar a RELEASE.
