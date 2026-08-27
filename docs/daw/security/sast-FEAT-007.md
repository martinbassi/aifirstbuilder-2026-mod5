# SAST FEAT-007: Rehidratar sesión (rol de usuario) al recargar la página

| Field | Value |
|-------|-------|
| Ticket | FEAT-007 |
| Fecha | 2026-08-26 |
| Alcance | `GetCurrentSessionQuery.cs`, `AuthController.cs` (backend); `auth.service.ts`, `session-rehydration.initializer.ts`, `app.config.ts` (frontend); 4 archivos de test |

## Secrets
✅ F-SAST-01: sin API keys, passwords, tokens ni connection strings hardcodeados.

## Injection
✅ F-SAST-02: sin queries SQL/NoSQL nuevas por concatenación (`AsNoTracking().SingleOrDefaultAsync`
parametrizado vía EF Core, igual que el resto del proyecto).
✅ F-SAST-03/05: sin comandos de sistema ni paths de archivo derivados de input de usuario.

## XSS y funciones inseguras
✅ F-SAST-06: no aplica — sin `innerHTML`/renderizado de HTML nuevo.
✅ F-SAST-04/17: sin `eval`/deserialización insegura.
✅ F-SAST-08: sin criptografía nueva.

## Otras categorías obligatorias
✅ F-SAST-07 (SSRF): sin llamadas de red server-side nuevas hacia URLs de terceros.
✅ F-SAST-09 (debug mode): sin cambios de configuración de entorno.
✅ F-SAST-10 (logging de datos sensibles): sin logging nuevo de username/rol/token.
✅ F-SAST-11 (upload sin restricción): no aplica, sin endpoints de upload.
✅ F-SAST-12 (CSRF): no aplica — mismo mecanismo de sesión Bearer ya existente, sin cookies.
✅ F-SAST-14 (validación de input incompleta): `GET /api/auth/session` no acepta ningún input del
cliente (todo sale del `ClaimsPrincipal` ya validado por `[Authorize]`).
✅ F-SAST-15 (error handling que filtra internals): el caso defensivo
(`InvalidOperationException`) no expone detalles al cliente — cae al middleware genérico existente,
sin cambios.

## Dependencias
✅ F-SAST-13/16: `dotnet list package --vulnerable --include-transitive` → 0 paquetes vulnerables en
los 4 proyectos del backend. Sin paquetes NuGet/npm nuevos en este ticket (confirmado en threat
model y arch audit de PLAN).

## Suppressions
Ninguna.

---
Total: 12 checks limpios, 0 vulnerabilidades (0 Critical, 0 High, 0 Medium)
Resultado: **PASSED**
