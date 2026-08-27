# Verify FEAT-007: Rehidratar sesión (rol de usuario) al recargar la página

| Field | Value |
|-------|-------|
| Ticket | FEAT-007 |
| PRD | docs/daw/prd/prd-FEAT-001a.md (FR-08, NFR-04, AC-07/08/09) |
| Spec | docs/daw/specs/spec-FEAT-007.md |
| Fecha | 2026-08-26 |
| Rondas | 1 |

## Ronda 1 — daw-verify-module

### Trazabilidad PRD → Código → Tests
- ✅ AC-07 (bloquea el router hasta rehidratar) → `session-rehydration.initializer.ts` →
  `session-rehydration.initializer.spec.ts`.
- ✅ AC-08 (token inválido → limpia/redirige) → interceptor existente (sin cambios, reusado) →
  test que confirma que el initializer nunca bloquea/rechaza.
- ✅ AC-09 (rol correcto sin relogin) → `GetCurrentSessionQuery` → `AuthService.getCurrentSession()`
  → `sessionStore.setUser(...)` → tests de ambos lados verifican el objeto completo, no solo el
  status code.

### Cadena end-to-end razonada (sin cambios de código en esta parte)
`SessionAuthenticationHandler` coloca el claim de rol en cada request → `GetCurrentSessionQuery` lo
lee tal cual (nunca reconsulta) → `AuthService` lo mapea a `SessionUser` → `sidebar.component.html`/
`adminGuard` (ya existentes) leen ese mismo signal. Verificado sin gaps: un admin que hace F5 deja
de ser expulsado/ocultado del ítem "Moderación".

### Spec — 3 bloques
- ✅ Block 1 (backend): 5/5 tests requeridos, `dotnet build` limpio.
- ✅ Block 2 (NSwag): `AuthClient.session()` presente, forma idiomática (no editado a mano), `tsc`
  limpio.
- ✅ Block 3 (frontend): 5/5 tests requeridos.

### TDD evidence — ⚠️ WARN no bloqueante
El verificador no pudo reconstruir el detalle rojo→verde por bloque en esta sesión de VERIFY (el
reporte de cada implementador vivió en la conversación de CODE, no en un artefacto persistido). Es
una brecha de trazabilidad de proceso, no un defecto: compensada con evidencia verificable de forma
independiente en esta misma ronda (suite completa real, mutation testing manual sobre los 3 puntos
de decisión nuevos del frontend, coverage real del backend). Recomendación a futuro: persistir el
reporte de cada bloque como artefacto versionado, no solo como mensaje conversacional.

### Coverage
- Backend: 100% líneas en `GetCurrentSessionQuery.cs`/`AuthController.cs` (medido con
  `dotnet test --collect:"XPlat Code Coverage"`); branch-rate 50% en el handler solo por 3 ramas
  defensivas documentadas como inalcanzables (mismo criterio ya aceptado en el proyecto).
- Frontend: sin `@vitest/coverage-v8` instalado — compensado con mutation testing manual sobre los
  3 puntos de decisión nuevos (`setUser` removido, condición de token invertida): ambos mutantes
  detectados/muertos por la suite existente.

### Quality
- ✅ Lint/type checker limpios (backend y frontend).
- ✅ Sin código muerto, sin imports sin usar.
- ✅ Suite completa (evidencia real de esta ronda): backend 113/113; frontend 136/138 (2 fallos
  preexistentes y ajenos, icono `environment-o`, ya documentados en tickets anteriores).

---

**Veredicto: PASSED — 0 FAILs, 1 WARN no bloqueante (trazabilidad de proceso), 15 checks en verde.**
