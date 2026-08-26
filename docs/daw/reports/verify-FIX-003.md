# Verify FIX-003: Revisar y corregir tests rotos por Title obligatorio en Mural + converters UTC

| Field | Value |
|-------|-------|
| Ticket | FIX-003 |
| Tier | FIX |
| Date | 2026-08-26 |
| Fix-plan | docs/daw/specs/fix-FIX-003.md |
| RCA | docs/daw/specs/rca-FIX-003.md |
| Threat model | docs/daw/security/threat-FIX-003.md |
| SAST | docs/daw/security/sast-FIX-003.md |
| Verificador | Agente `daw-module-verifier` (cross-verificación independiente, no escribió el código) |
| Ronda | 1/1 |

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  module-verifier — Verificación de FIX-003                │
│  (tests rotos por Title obligatorio en Mural + UTC)       │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Acceptance criteria: N/A (tier FIX, no PRD nuevo — el     │
│    PRD relacionado prd-FEAT-001b.md ya se actualizó en     │
│    DEFINE con FR-17/AC-15/AC-16; cubierto abajo como parte │
│    del regression test)                                   │
│                                                          │
│  Fix-plan steps (F-VER-02):                               │
│    ✅ Paso 1: BuildMultipartContent gana parámetro `title` │
│       (default "Mural de prueba") — CreateMuralTests.cs:215 │
│    ✅ Paso 2: las 12 invocaciones reales compilan sin       │
│       cambios (default aplica) — confirmado, 104/104 verde  │
│    ⚠️ Paso 3: 2 tests de borde agregados, PERO con status   │
│       code distinto al documentado en el fix-plan:          │
│       `Missing_title_is_rejected_with_400` (plan decía 422)  │
│       — desviación EXPLICADA y correcta: Title es un `string` │
│       no-anulable (NRT habilitado), [ApiController] le aplica │
│       [Required] implícito en el model binding y devuelve 400  │
│       ANTES de llegar a FluentValidation. Verificado empírica-  │
│       mente corriendo el test: pasa. `Title_longer_than_50_...`  │
│       sí coincide con el plan (422). El fix-plan tenía la         │
│       causa raíz correcta pero el código HTTP mal anticipado —      │
│       el código corrigió la aserción contra el comportamiento        │
│       real, no al revés (no es un bug oculto).                        │
│    ✅ Paso 4: `AddControllers().AddJsonOptions(...)` agregado en       │
│       Program.cs:40-45, junto al bloque `ConfigureHttpJsonOptions`     │
│       existente, tal como especifica el plan                            │
│    ✅ Paso 5: test de formato UTC en GetMuralByIdTests.cs                │
│       (`CreatedAt_is_serialized_with_the_full_utc_format`)                │
│    ⚠️ Paso 6: el plan pedía el test en `GetNearbyMuralsTests.cs`, pero     │
│       se agregó en `DiscoveryControllerTests.cs` — desviación JUSTIFICADA:  │
│       `GetNearbyMuralsTests` invoca el Handler directo (Block 2, sin        │
│       pasar por serialización JSON), por lo que un assert de formato ahí     │
│       no probaría nada real; `DiscoveryControllerTests` sí pasa por el        │
│       pipeline HTTP completo, que es donde `AddJsonOptions` aplica.            │
│       Comentario en el código documenta el razonamiento explícitamente.        │
│    ✅ Paso 7: test de formato UTC en LoginTests.cs                              │
│       (`ExpiresAt_is_serialized_with_the_full_utc_format`), cubre el gap        │
│       detectado por el impact scan (blast radius global de AddJsonOptions)       │
│                                                          │
│  Regression test (F-VER-02):                              │
│    ✅ Reproduce la causa raíz #1: reproducido INDEPENDIENTEMENTE con     │
│       `git worktree add` sobre 9cecf21 (pre-fix) + `dotnet test --filter │
│       CreateMuralTests` → 8/17 tests fallan (más de los 4 documentados   │
│       en el RCA: además de los 4 que assertan 201/Created, también       │
│       fallan `A_DbUpdateException_while_saving_returns_500...` y         │
│       `A_failing_blob_upload_returns_500...`, porque la validación 422   │
│       intercepta antes de llegar al código simulado de falla 500 —       │
│       gap de conteo en el RCA, ver WARN más abajo). Con el fix           │
│       aplicado: 104/104 backend PASS.                                    │
│    ✅ Causa raíz #2 (converter mal wireado) confirmada resuelta:         │
│       `Microsoft.AspNetCore.Mvc.JsonOptions` ahora recibe el converter   │
│       vía `AddControllers().AddJsonOptions`, y los 3 tests nuevos de     │
│       formato (`^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$`) lo verifican    │
│       contra respuestas HTTP reales, no solo contra el Handler.          │
│                                                          │
│  TDD evidence:                                             │
│    ✅ Confirmado independientemente (no solo declarado): antes del fix, │
│       8/17 tests de CreateMuralTests.cs fallaban con 422/500≠422;       │
│       después, 104/104 pasan.                                            │
│                                                          │
│  Suite completa sin regresiones nuevas (F-VER-06):         │
│    ✅ Backend: 104/104 PASS                                              │
│    ✅ Frontend: 131/133 PASS — los 2 fallos son en                       │
│       `discovery-list.component.spec.ts`, confirmados independientemente │
│       como preexistentes del commit `d65842f` (Card→NzList), ajeno.      │
│                                                          │
│  Quality:                                                 │
│    ✅ Lint/typecheck: tsc 0 errores, dotnet build 0 warnings/errores      │
│    ✅ Imports limpios en los archivos tocados                              │
│    ⚠️ Código muerto: `Program.cs:165-171` conserva el bloque              │
│       `ConfigureHttpJsonOptions` original, que no tiene ningún efecto      │
│       real (el proyecto solo sirve MapControllers) — queda duplicado con   │
│       el nuevo `AddJsonOptions`. No incumple el fix-plan (no pedía          │
│       removerlo), pero es deuda técnica para un housekeeping futuro.        │
│    ✅ Sin tests frágiles (W-VER-03): GUIDs únicos por test, sin              │
│       dependencia de orden ni fechas/IDs hardcodeados                        │
│    N/A Coverage (F-VER-03): FIX sin lógica de negocio nueva — mismo          │
│       criterio que FIX-001/FIX-002                                            │
│    ✅ Sad-path tests (F-VER-04): título ausente y >50 chars cubiertos          │
│                                                          │
│  Hallazgos de proceso (no bloqueantes):                    │
│    ⚠️ Alcance ampliado durante CODE sin volver a PLAN: se corrigieron       │
│       también mural.service.spec.ts y create-mural-form.component.spec.ts   │
│       (frontend), gap del mismo commit 9cecf21 que el impact scan original   │
│       (scopeado a backend) no cubrió. Cambio de solo-test, bajo riesgo,       │
│       documentado transparentemente.                                          │
│    ⚠️ El RCA subestimó el blast radius real: documentó 4 tests rotos por      │
│       la causa #1; en la reproducción independiente fallaron 8/17. No         │
│       afecta el resultado (el fix resuelve los 8), pero es una imprecisión     │
│       de diagnóstico a tener en cuenta para RCAs futuros.                      │
│                                                          │
│  ─────────────────────────────────────────────────────   │
│  Verdict: PASSED                                          │
│  FAILs: 0 | WARNs: 5 | PASSes: 13                          │
└─────────────────────────────────────────────────────────┘
```

## Conclusión

Ningún criterio funcional quedó sin implementar ni sin test. La suite completa está en verde salvo
los 2 fallos preexistentes ya confirmados como ajenos a este ticket. El regression test reproduce el
bug original (8/17 tests fallando antes del fix) y confirma que el fix lo resuelve (104/104 después).
Las 5 WARN son desviaciones documentadas y justificadas, no defectos.

`gates.verify` → `true`.
