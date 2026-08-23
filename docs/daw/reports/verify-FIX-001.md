# Reporte de verificación — FIX-001: Configurar CORS para desarrollo local

**Tier:** FIX
**RCA:** `docs/daw/specs/rca-FIX-001.md`
**Fix-plan:** `docs/daw/specs/fix-FIX-001.md`
**Threat model:** `docs/daw/security/threat-FIX-001.md`
**Agente:** `daw-module-verifier` (cross-check, no escribió el código)

---

## Ronda 1 — 2026-08-23 — Resultado: **BLOCKED**

### Fix-plan steps (F-VER-02)

| Paso | Resultado |
|---|---|
| 1. Bloque `AddCors` dentro de `IsDevelopment()` | ✅ `Program.cs:142-151` |
| 2. `UseCors("DevelopmentCors")` en el pipeline, antes de auth/authorization | ✅ `Program.cs:163-168` |
| 3. Sección `Cors:AllowedOrigins` en `appsettings.Development.json` | ✅ líneas 15-17 |
| 4. `appsettings.json` (base/Production) sin tocar | ✅ confirmado con `git diff main...HEAD` |

### Regression test / sad path / mitigaciones

- ✅ Test de regresión (`CorsTests.cs:64`) reproduce el bug y pasa con el fix — 3/3 tests PASS.
- ✅ Sad path (`CorsTests.cs:80`): origen no permitido, header ausente.
- ✅ Mitigación R1 (threat model) verificada con test dedicado (`CorsTests.cs:95`, Production arranca sin excepción).
- ✅ Mitigaciones R2 (comentario de exclusividad dev-only) y R3 (sin `AllowCredentials`) reflejadas en el código.
- ✅ Suite completa backend: 99/99. Build/typecheck/lint limpios.
- ✅ Coherencia root cause ↔ solución, rollback plan válido.

### FAIL

**Evidencia TDD (F-VER-06 / proceso):** ❌ el agente verificador no encontró, en ningún artefacto en disco, el detalle de qué assertions fallaban en rojo antes del fix. La entrada de `.daw-state.json` solo afirma que existió, sin detalle. El agente no pudo reconstruirlo empíricamente porque el sandbox le bloqueó (correctamente) revertir código de producción.

**La evidencia sí existe** — se generó en esta sesión, antes de escribir el fix-plan a producción, con este procedimiento:

1. `git stash push` de `Program.cs` y `appsettings.Development.json` (dejando `CorsTests.cs` ya escrito en el working tree).
2. `dotnet test --filter "FullyQualifiedName~CorsTests"` con el fix ausente:
   ```
   [FAIL] Request_with_Origin_localhost_4200_in_Development_receives_Access_Control_Allow_Origin_header
   Mensaje de error: Expected Access-Control-Allow-Origin header to be present.
   Con error: 1, Superado: 2, Omitido: 0, Total: 3
   ```
   - `Request_with_Origin_localhost_4200_in_Development_receives_Access_Control_Allow_Origin_header` → **FAIL** (el que reproduce el bug: sin `AddCors`/`UseCors`, ASP.NET Core nunca agrega el header).
   - `Request_with_a_different_Origin_does_not_receive_Access_Control_Allow_Origin_header` → pasaba igual (sin CORS activo, ningún origen recibe el header — trivialmente cierto sin el fix, no es evidencia de la corrección en sí).
   - `AddCors_is_not_registered_when_the_host_runs_outside_Development` → pasaba igual (sin `AddCors` registrado en absoluto, el host arranca sin excepción en cualquier entorno — cierto con o sin el fix).
3. `git stash pop` para restaurar el fix.
4. `dotnet test --filter "FullyQualifiedName~CorsTests"` con el fix aplicado: **3/3 PASS**.

Los 3 tests son necesarios para el diseño (regresión + sad path + safety), pero solo el primero es el que efectivamente falla en rojo — los otros dos validan comportamiento que ya era cierto antes del fix por razones distintas (ausencia total de CORS, o ausencia total de `AddCors`). Esto es consistente con su propósito: no son regresión del bug reportado, son cobertura de las mitigaciones del threat model (R3 y R1 respectivamente).

**Acción:** vuelta a CODE (loop correctivo, exclusivamente documental — sin cambios de código), gates `tests`, `sast` y `verify` limpiados — deben reganarse.

---

## Ronda 2 — 2026-08-23 — Resultado: **PASSED**

**Cambio aplicado en CODE (commit `8e78a63`):** exclusivamente documental — sección "Evidencia TDD"
agregada a `docs/daw/specs/fix-FIX-001.md:140-172`, más el apéndice de re-scan en
`docs/daw/security/sast-FIX-001.md:73-78`. Confirmado por `git show --stat 8e78a63`: 2 archivos
`.md`, 44 líneas agregadas, 0 en código de producción/tests/dependencias.

### Evidencia TDD — re-verificada independientemente

✅ El mensaje de error citado en la sección ("Expected Access-Control-Allow-Origin header to be
present.") coincide exacto con el assert real en `CorsTests.cs:75` — no es un mensaje inventado
post-hoc. La sección explica, test por test, cuál detecta la regresión (el primero) y por qué los
otros dos no fallan en rojo (ejercitan mitigaciones R3/R1 del threat model, ciertas con o sin el fix
por razones distintas al bug). FAIL de ronda 1 resuelto.

### Resto de criterios

✅ Sin cambios desde ronda 1 (fix-plan steps, regression test, sad path, mitigaciones, coherencia
root cause↔solución, rollback plan) — siguen PASS.

### Suites re-corridas en esta ronda

- ✅ Backend: 99/99 tests (`dotnet test`), 0 regresiones tras el commit de esta ronda.
- ✅ Backend build: 0 errores, 0 warnings.

### SAST

✅ Coherencia confirmada entre el apéndice de re-scan y el diff real del commit — alcance
exclusivamente documental, 0 hallazgos.

### Veredicto Ronda 2

**Total: 8 PASS, 0 FAIL, 0 WARN**
**Resultado: PASSED**

`gates.verify` = `true`.
