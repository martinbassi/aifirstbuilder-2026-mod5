# Threat Model FEAT-001a: Autenticación básica

| Field | Value |
|-------|-------|
| Ticket | FEAT-001a |
| Date | 2026-08-15 |
| Result | PASSED (mitigaciones plegadas al spec) |

## Arquitectura analizada

Backend .NET (`Paretto.Domain`/`Paretto.Infrastructure`/`Paretto.Api`, ver
`docs/adr/adr-001-estructura-multiproyecto-dotnet.md`):
- `AuthController`: `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout`.
- `RegisterUserCommand`, `LoginCommand`, `LogoutCommand` (+ Handlers, Validators FluentValidation).
- `IPasswordHasher` (wrapper de `Microsoft.AspNetCore.Identity.PasswordHasher<User>`).
- `ISessionTokenGenerator` (token opaco, `RandomNumberGenerator` 256 bits).
- `SessionAuthenticationHandler`: valida el Bearer token contra la tabla `Sessions` por PK.
- `AppDbContext` / SQL Server: tablas `Users`, `Sessions`.

Frontend Angular: `register-form`/`login-form` (ui), `auth.service.ts` (data, envuelve el cliente
NSwag), `session.store.ts` (state, signals), `auth.interceptor.ts` (adjunta `Authorization: Bearer`).

## Trust boundaries (F-TM-02)

1. **Navegador (no confiable) → Angular SPA (ejecuta en el navegador, no confiable) → HTTPS →
   `Paretto.Api`.** Cruce principal: cualquier input del usuario y el token de sesión que el
   navegador reenvía.
2. **`Paretto.Api` → SQL Server** (red interna). Cruce: las credenciales hasheadas y los tokens de
   sesión persistidos viajan por acá.

## Clasificación de datos sensibles (F-TM-05)

| Dato | Clasificación | En tránsito | En reposo |
|---|---|---|---|
| Contraseña (antes de hashear) | Credentials | HTTPS (NFR-02) | Nunca se persiste — se hashea en el momento (`IPasswordHasher`) |
| `PasswordHash` | Credentials | — | Hash no reversible (`PasswordHasher<User>`), NFR-01 |
| Email | PII | HTTPS (NFR-02) | Texto plano en `Users` — necesario para login/futuro reset. TDE de SQL Server 2025 recomendado a nivel de infraestructura (fuera del alcance de código de este ticket) |
| Token de sesión | Credentials (equivalente a una contraseña temporal) | HTTPS, header `Authorization` (nunca URL/query — `.daw/rules/security.instructions.md`) | Ver mitigación M2 — se persiste hasheado, no en claro |

## STRIDE por componente (F-TM-01)

### `AuthController` (register/login/logout)
| STRIDE | Análisis |
|---|---|
| Spoofing | Fuerza bruta/credential stuffing sin límite de intentos (RF-052 fuera de alcance) → **Riesgo R3** |
| Tampering | Body de request tamperable en tránsito — mitigado por HTTPS obligatorio (NFR-02) |
| Repudiation | Sin logging explícito de eventos de auth → **Riesgo R6** |
| Information Disclosure | Mensajes de registro duplicado — ya resuelto en el PRD (FR-02, mensaje genérico, ver corrección de este mismo loop) |
| DoS | Spam de registro/login sin rate limit → cubierto por la misma mitigación de R3 |
| Elevation of Privilege | Si `RegisterUserCommand` aceptara un campo `role` del cliente, un atacante podría autoasignarse Administrador → **Riesgo R1** |

### `SessionAuthenticationHandler` / tabla `Sessions`
| STRIDE | Análisis |
|---|---|
| Spoofing | Forjar un token — mitigado por entropía alta (256 bits aleatorios, `RandomNumberGenerator`) |
| Tampering | Token opaco sin claims embebidos — nada que tamperar, solo coincidencia exacta |
| Repudiation | Igual que arriba, ver R6 |
| Information Disclosure | Tokens en texto plano en `Sessions`: una fuga de la base da acceso directo a sesiones activas por hasta 7 días → **Riesgo R2** |
| DoS | N/A — lookup por PK, no es un vector de agotamiento distinto de R3 |
| Elevation of Privilege | El handler debe leer `UserId`/`Role` únicamente de la fila de `Sessions`/`Users`, nunca de un claim que llegue en el request — y debe validar `ExpiresAt` en cada request (ya señalado por `daw-arch-auditor`) |

### Frontend (almacenamiento del token, `session.store.ts` + `auth.interceptor.ts`)
| STRIDE | Análisis |
|---|---|
| Information Disclosure | El token viaja en el header `Authorization` (mandado por `.daw/rules/security.instructions.md`), lo que implica guardarlo del lado del cliente (no una cookie httpOnly) — si existiera una vulnerabilidad XSS en la SPA, el token es robable por script → **Riesgo R5** |

## Riesgos (F-TM-03: todos con mitigación o riesgo aceptado)

| # | Riesgo | STRIDE | Likelihood | Impact | Mitigación |
|---|---|---|---|---|---|
| R1 | `RegisterUserCommand` podría aceptar `role` del cliente y autoasignar Administrador | Elevation of Privilege | Medium | Critical | 🟠 **HIGH** — El Command/DTO de registro NO incluye un campo `role`. El handler asigna el rol por defecto hardcodeado en el servidor; cualquier valor de rol en el body se ignora (no se bindea). |
| R2 | Tokens de sesión en texto plano en `Sessions`: una fuga de DB da sesiones activas por hasta 7 días | Information Disclosure | Low | High | 🟠 **HIGH** — Se persiste el hash SHA-256 del token, nunca el token en claro. El lookup compara por hash. El token en claro solo existe en la respuesta al cliente y en memoria del request de login. |
| R3 | Sin límite de intentos en `/login` ni `/register` (RF-052 fuera de alcance): fuerza bruta, credential stuffing, spam de registro | Spoofing / DoS | High | Medium | 🟠 **HIGH** — Rate limiting básico por IP sobre `/api/auth/login` y `/api/auth/register` (middleware nativo `Microsoft.AspNetCore.RateLimiting` de .NET 10, sin dependencia nueva). No reemplaza el bloqueo por cuenta de RF-052 — reduce la superficie mientras ese ticket no existe. |
| R5 | Token Bearer en almacenamiento de cliente, robable vía XSS | Information Disclosure | Low | High | 🟡 **MEDIUM** — Mitigado en profundidad, no eliminado: CSP headers (`Content-Security-Policy`) + las prácticas de XSS ya mandatorias en `.daw/rules/security.instructions.md` (nunca `innerHTML` con input de usuario, escape de output). Se documenta como **riesgo aceptado**: la alternativa (cookie httpOnly) contradice la convención explícita del proyecto ("tokens en headers, Authorization, nunca en URL/query params"), y cambiarla está fuera del alcance de decisión de un solo ticket. |
| R6 | Sin logging estructurado de eventos de auth (registro/login éxito-fallo/logout) | Repudiation | Medium | Low | 🟢 **LOW** — `LoggingBehavior` (MediatR `IPipelineBehavior`) loguea explícitamente estos eventos con timestamp + `UserId` (nunca contraseña ni token). |
| R7 | Tampering del body en tránsito | Tampering | Low | Medium | 🟢 **LOW** — Cubierto por HTTPS obligatorio (NFR-02), sin mitigación adicional necesaria. |

### Riesgo aceptado R5 — aprobación formal (F-TM-04)

| Campo | Valor |
|---|---|
| Quién lo aceptó | Usuario del proyecto (martin-bassi), durante PLAN de FEAT-001a |
| Justificación | `.daw/rules/security.instructions.md` exige explícitamente tokens en el header `Authorization`, lo que implica almacenamiento accesible por JavaScript del lado del cliente. Mitigado en profundidad con CSP + higiene XSS ya mandatoria; no eliminado del todo. Cambiar a cookie httpOnly es una decisión de arquitectura de sesión más amplia, fuera del alcance de este ticket. |
| Condiciones de revisión | Revisar si en un ticket futuro de seguridad de auth (el mismo que retome RF-052) conviene migrar a cookie httpOnly + CSRF token, especialmente si el producto empieza a manejar datos más sensibles que murales públicos. |

## Mitigaciones a plegar en el spec

1. `RegisterUserCommand` sin campo `role` — rol por defecto asignado server-side (R1).
2. `Sessions.TokenHash` (SHA-256), nunca el token en claro — lookup por hash (R2).
3. Rate limiting nativo de ASP.NET Core sobre `/api/auth/login` y `/api/auth/register` (R3).
4. `LoggingBehavior` loguea eventos de auth (registro/login éxito-fallo/logout) sin datos sensibles (R6).
5. `SessionAuthenticationHandler` valida `ExpiresAt` explícitamente en cada request, no solo la
   existencia de la fila (arrastrado de la auditoría de arquitectura).
6. CSP headers configurados en `Paretto.Api` / `frontend/src/index.html` (R5, riesgo aceptado con
   mitigación en profundidad).

─────────────────────────────────────────────────────────
Riesgos: C:0 H:3 M:1 L:2 (R1/R2/R3 con mitigación técnica folded, R5 riesgo aceptado con mitigación
en profundidad, R6/R7 con mitigación técnica LOW)
Result: **PASSED** — toda mitigación queda plegada en el spec antes de escribirlo a disco.
