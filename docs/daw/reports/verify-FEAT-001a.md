# VERIFY — FEAT-001a (Autenticación básica)

**Fecha:** 2026-08-16
**Tier:** FEATURE
**PRD:** `docs/daw/prd/prd-FEAT-001a.md`
**Spec:** `docs/daw/specs/spec-FEAT-001a.md`
**SAST (fase CODE):** `docs/daw/security/sast-FEAT-001a.md` — PASSED (1 High encontrado y corregido)

## Veredicto: PASSED

0 FAILs, 3 WARNs no bloqueantes. Verificación cruzada realizada por `daw-module-verifier`
(independiente de los implementadores), sobre el ticket completo (8/8 bloques).

---

## Traceabilidad PRD → Código → Tests (F-VER-01)

| AC | Descripción | Handler/Componente | Test |
|---|---|---|---|
| AC-01 | Registro exitoso, rol default | `RegisterUserCommand.cs` | `RegisterUserTests.cs::Register_with_valid_data_creates_the_account_with_standard_role` |
| AC-02 | Mensaje genérico ante duplicado | `RegisterUserCommand.cs::DuplicateAccountException` | `RegisterUserTests.cs::Duplicate_email_and_duplicate_username_return_the_exact_same_error_message` |
| AC-03 | Contraseña inválida rechazada | `RegisterUserCommandValidator` | `RegisterUserTests.cs` (4 tests: longitud, sin letras, sin dígitos) |
| AC-04 | Login válido emite sesión de 7 días | `LoginCommand.cs` | `LoginTests.cs::Login_with_valid_credentials_returns_a_token_and_expiresAt_about_7_days_out` |
| AC-05 | Credenciales inválidas, mensaje genérico | `LoginCommand.cs::InvalidCredentialsException` | `LoginTests.cs::Nonexistent_user_and_wrong_password_return_the_exact_same_error` + `login-form.component.spec.ts` |
| AC-06 | Logout invalida la sesión | `LogoutCommand.cs` | `LogoutTests.cs::After_logout_a_subsequent_request_with_the_same_token_is_rejected_with_401` |

Las 6 AC tienen al menos un test que las valida, y todos pasan.

## Cobertura por bloque del spec (F-VER-02, F-VER-06)

| Bloque | Tests requeridos por el spec | Estado |
|---|---|---|
| 1 — Bootstrap backend | 2/2 | ✅ |
| 2 — Bootstrap frontend | 1/1 | ✅ |
| 3 — Dominio y persistencia | 3/3 (contra SQL Server real) | ✅ |
| 4 — Servicios de seguridad | 4/4 | ✅ |
| 5 — Registro | 7/7 (6 originales + 1 agregado en ronda 2, cierra F-SPEC-16) | ✅ |
| 6 — Login + esquema de sesión | 6/6 | ✅ |
| 7 — Logout | 3/3 | ✅ |
| 8 — Cliente NSwag + Angular | 5/5 + 2 tests extra de seguridad (spoofing de origen) | ✅ |

Los 8 bloques están implementados íntegramente. 0 tests faltantes de los listados en el spec.

## F-VER-01 a F-VER-06

| Regla | Resultado |
|---|---|
| F-VER-01 — AC con test pasando | ✅ 6/6 AC cubiertas |
| F-VER-02 — Bloque no implementado | ✅ 8/8 bloques completos |
| F-VER-03 — Cobertura ≥ 80% | ✅ Backend: 97.58% líneas / 82.00% branches / 100% funciones. Frontend: 91.83% statements / 87.09% branches / 84.61% funciones. (Excluidas migraciones EF, `AppDbContextFactory.cs` y `api-client.generated.ts` — artefactos de diseño/generados, no código de aplicación) |
| F-VER-04 — Sad-path por endpoint/función | ✅ register, login, logout, interceptor, authGuard, formularios — todos con al menos un caso inválido probado |
| F-VER-05 — Lint/type checker | ✅ `dotnet build`: 0 warnings, 0 errores. `ng lint`: sin hallazgos. `ng build`: compila (warning de bundle-budget, no es de lint/tipos) |
| F-VER-06 — Tests del spec existen y pasan | ✅ 0 faltantes, contrastados bloque por bloque |

## Warnings (no bloqueantes)

| ID | Hallazgo | Severidad | Acción |
|---|---|---|---|
| W-VER-01 | `frontend/src/app/core/api-client/.gitkeep` sobrevive junto al cliente ya generado — perdió su propósito | Baja | Limpieza cosmética, sin impacto funcional |
| W-VER-02 | `AuthService.logout()` (líneas 87-93) sin ningún test directo — es el único método público de la clase sin cobertura, y tiene lógica real (limpia la sesión local incluso si la llamada al server falla). El spec no lo lista como test requerido explícito, por eso no es F-VER-06 | Media | Recomendado un test de seguimiento; no bloquea este ticket |
| W-VER-02 | `LogoutCommandHandler` (81%/50%) y `SessionAuthenticationHandler` (86%/83%) por debajo del 90% recomendado — ramas sin cubrir son early-returns defensivos documentados como inalcanzables en la práctica (`[Authorize]` ya garantiza sesión válida antes del Handler) | Baja | Aceptable, código defensivo documentado |
| W-VER-03 | Tests frágiles | — | Ninguno detectado — DBs InMemory con nombre único, cleanup en `finally`, tolerancias razonables en asserts temporales |

## Verificación mecánica

- Backend: `dotnet test Paretto.sln` (con `ConnectionStrings__DefaultConnection` real) → 26/26 ✅
- Frontend: `ng test --watch=false` → 20/20 ✅
- `dotnet build Paretto.sln` → 0 warnings, 0 errores ✅
- `ng lint` → sin hallazgos ✅
- `ng build` → compila, único warning de bundle-budget (no bloqueante) ✅

## Archivos del ticket (8 bloques + cierre)

Commits `95cc987`..`8adea27` en `feat/FEAT-001a-autenticacion-basica`, incluyendo el fix de SAST
(Swagger gateado por entorno) y el reporte `docs/daw/security/sast-FEAT-001a.md`.
