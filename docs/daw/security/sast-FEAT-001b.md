# SAST Report — FEAT-001b (Crear mural)

| Field | Value |
|-------|-------|
| Date | 2026-08-22 |
| Ticket | FEAT-001b |
| Phase | CODE closeout |
| Scope | Todo el diff del ticket contra `main` (Blocks 1-8) + fix de closeout (ADR-004) |

## Result: PASSED

## Secrets (F-SAST-01)
- ✅ Sin credenciales/API keys hardcodeadas. `appsettings.json`/`appsettings.Development.json` usan
  `Trusted_Connection=True` (auth integrada, sin password embebido) y `UseDevelopmentStorage=true`
  (string bien conocido del emulador Azurite, no un secreto). `AzureStorage:ConnectionString` en
  producción queda vacío en el repo (se inyecta por configuración de entorno).
- ✅ `"Sup3rSecret!"` en los tests es un fixture de prueba (mismo patrón ya usado en FEAT-001a), no
  una credencial real.

## Injection (F-SAST-02, F-SAST-03)
- ✅ Sin SQL crudo/concatenado — toda persistencia pasa por EF Core (LINQ), incluidas las
  migraciones. Sin `FromSqlRaw`/`ExecuteSqlRaw`.
- ✅ Sin `Process.Start`/ejecución de comandos del sistema.

## Path traversal (F-SAST-05)
- ✅ `CreateMuralCommandHandler.blobName` se genera siempre server-side con `Guid.NewGuid()`, nunca
  a partir del nombre de archivo del cliente (comentario explícito en el código citando threat
  model R4).

## XSS (F-SAST-06)
- ✅ Sin `innerHTML`/`bypassSecurityTrust*` en los componentes de `features/murals` ni en el routing
  tocado por Block 8.

## Deserialización insegura (F-SAST-04)
- ✅ Sin `BinaryFormatter`/`XmlSerializer` con tipos no controlados en el código propio.

## SSRF (F-SAST-07)
- ✅ Los únicos destinos de red salientes (SQL Server, Azure Storage) vienen de configuración del
  servidor, nunca de input del cliente.

## Cripto débil (F-SAST-08)
- N/A para este ticket (no introduce hashing/cifrado nuevo).

## Debug mode en producción (F-SAST-09)
- ✅ Swagger sigue gateado por `IsDevelopment()` (fix de FEAT-001a, verificado que sigue vigente en
  `Program.cs`).

## Logging de datos sensibles (F-SAST-10)
- ✅ `LoggingBehavior` solo loguea el nombre del tipo de Command/Query y duración, nunca el
  contenido de los campos (comentario explícito en el código).
- ✅ `ExceptionHandlingMiddleware` expone `ex.ToString()` solo bajo `IsDevelopment()`; en cualquier
  otro entorno el detalle queda `null`.

## Unrestricted upload (F-SAST-11)
- ✅ `CreateMuralCommandValidator`: límite de tamaño 10MB (RNF-003/RF constraint), y validación de
  firma de bytes (magic number) para JPEG/PNG/WebP — no confía en `Content-Type`/extensión, ambos
  spoofeable por el cliente (threat model R3).

## CSRF (F-SAST-12)
- ✅ N/A — autenticación por token de sesión opaco enviado en header `Authorization` (no cookie
  ambiente), mismo esquema ya evaluado en FEAT-001a. Un token que el cliente debe adjuntar
  explícitamente vía JS no es vulnerable a CSRF clásico.

## Input validation incompleta (F-SAST-14)
- ✅ `Latitude`/`Longitude` acotados a rango válido (`InclusiveBetween`), tamaño y firma de foto
  validados, todo vía FluentValidation en el pipeline de MediatR (`ValidationBehavior`).

## Error handling inseguro (F-SAST-15)
- ✅ `MuralPersistenceException` unifica el mensaje de error de Storage/DB en uno solo genérico
  (FR-12) — el caller no distingue cuál de las dos operaciones falló, sin filtrar detalle interno.

## Autorización (control de acceso, más allá del catálogo genérico pero exigido por AGENTS.md)
- ✅ Ambos endpoints (`POST /api/murals`, `GET /api/murals/{id}`) requieren `[Authorize]`.
- ✅ `GetMuralByIdQuery` aplica autorización de grano fino: un mural `Pending`/`Rejected` solo es
  visible para su dueño o un Administrador; para cualquier otro caller responde 404 genérico
  (anti-enumeración), nunca 403 (no revela existencia). Cumple la prohibición de AGENTS.md de no
  exponer murales `pending`.

## Dependencias (F-SAST-13/16)
- ❌→✅ **High, corregido en este closeout:** `dotnet list package --vulnerable --include-transitive`
  reportó `Newtonsoft.Json 10.0.3` (transitivo vía `NsfwSpy → Microsoft.ML`, confirmado en
  `project.assets.json`) —
  [GHSA-5crp-9r3c-p9vr](https://github.com/advisories/GHSA-5crp-9r3c-p9vr), resource exhaustion via
  deeply nested JSON, corregido en 13.0.1. **Fix:** `PackageReference` explícito a
  `Newtonsoft.Json 13.0.4` en `Paretto.Infrastructure.csproj` para sobreescribir la resolución
  transitiva — mismo patrón ya usado en Block 3 para `Magick.NET-Q16-AnyCPU`. Re-verificado limpio
  tras el pin; build y suite completa (53/53) en verde después del cambio.
- ✅ `npm audit` (frontend, con y sin `--omit=dev`): 0 vulnerabilidades.

## Suppressions
Ninguna — no hubo hallazgos Medium que requirieran documentar una supresión.

---

**Total: 20 categorías revisadas, 0 Critical, 0 High abiertos (1 High corregido), 0 Medium, 0 Low sin documentar.**
**Result: PASSED**
