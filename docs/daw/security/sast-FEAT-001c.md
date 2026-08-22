# SAST FEAT-001c: Moderación mínima

| Field | Value |
|-------|-------|
| Ticket | FEAT-001c |
| Date | 2026-08-22 |
| Scope | Todos los archivos de producción y test tocados por los 7 bloques (23 archivos, ver `git diff --stat main...HEAD`) |

## Secrets (F-SAST-01)

- ✅ Sin patrones de password/api-key/secret/token hardcodeados en el diff de producción.
- ✅ `.env` no aplica a este stack (config vía `appsettings.json`/env vars, sin cambios en este
  ticket).

## Inyección (F-SAST-02, F-SAST-03, F-SAST-05)

- ✅ SQL/NoSQL: todas las queries nuevas (`GetPendingMuralsQuery`, `ApproveMuralCommand`,
  `RejectMuralCommand`) usan EF Core LINQ (`SingleOrDefaultAsync`, `Where`, `OrderBy`, `Skip`,
  `Take`) — sin `FromSqlRaw`/`ExecuteSqlRaw` ni concatenación de strings en queries.
- ✅ Command injection: sin `Process.Start`, sin `child_process`, sin `eval()`.
- ✅ Path traversal: este ticket no toca manejo de archivos/blobs (reutiliza
  `IBlobStorageService.GenerateReadSasUrl` ya existente y ya auditado en FEAT-001b).

## XSS (F-SAST-06)

- ✅ Sin `innerHTML`/`bypassSecurityTrust`/`dangerouslySetInnerHTML` en el diff. El template nuevo
  (`pending-murals-list.component.html`) usa binding de Angular estándar (interpolación,
  `[src]`), que escapa por defecto.

## Funciones inseguras / criptografía débil (F-SAST-04, F-SAST-08, F-SAST-17)

- ✅ Sin `eval()`, sin deserialización insegura nueva.
- ✅ Sin MD5/SHA1/DES/ECB en el diff — este ticket no toca hashing de contraseñas ni tokens
  (reutiliza `IPasswordHasher`/`ISessionTokenGenerator` ya existentes).

## Resto de categorías obligatorias (F-SAST-07, F-SAST-09, F-SAST-10, F-SAST-11, F-SAST-12)

- ✅ SSRF: sin llamadas salientes nuevas a URLs derivadas de input del usuario.
- ✅ Debug mode en producción: sin cambios a la gating de Swagger (`app.Environment.IsDevelopment()`,
  ya corregido en FEAT-001a).
- ✅ Logging de datos sensibles: sin `Console.Write`/`Debug.Write`/logging de password/token en el
  diff.
- ✅ Unrestricted upload: este ticket no agrega ni modifica ningún endpoint de upload.
- ✅ CSRF: los dos endpoints `POST` nuevos (`approve`/`reject`) usan el mismo esquema de
  autenticación por `Authorization: Bearer {token}` que el resto de la API (nunca cookies) — el
  mismo argumento ya aceptado en el threat model de FEAT-001a/b aplica sin cambios: Bearer-en-header
  no es explotable por CSRF clásico.

## Medium — validación de input y manejo de errores (F-SAST-14, F-SAST-15)

- ✅ F-SAST-14: `page`/`pageSize` validados por `GetPendingMuralsQueryValidator`
  (FluentValidation, rango `1..50`); `id` en las rutas de aprobar/rechazar restringido por
  `{id:guid}` a nivel de routing.
- ✅ F-SAST-15: las 4 excepciones nuevas (`ModeratedMuralNotFoundException`,
  `MuralNotPendingException`, `ModerationPersistenceException`, la extensión de `LoginResponse` no
  agrega ninguna) llevan mensajes genéricos constantes, sin interpolar detalles internos (stack
  trace, SQL, rutas de archivo). Mismo patrón que `InvalidCredentialsException`/
  `MuralAccessDeniedException` ya auditado.

## Autorización (least privilege, `.daw/rules/security.instructions.md`)

- ✅ Los 3 endpoints de moderación (`GET pending`, `POST approve`, `POST reject`) exigen
  `[Authorize(Roles = "Administrator")]` a nivel de clase — verificado en las 4 rondas de revisión
  de CODE (Blocks 2-4) que ningún endpoint quedó sin el atributo tras las modificaciones
  incrementales del controller.

## Dependencias (F-SAST-13, F-SAST-16)

- ✅ `dotnet list package --vulnerable --include-transitive` (los 4 proyectos backend) → sin
  paquetes vulnerables.
- ✅ `npm audit --production` (frontend) → 0 vulnerabilidades.
- ✅ Sin dependencias nuevas agregadas por este ticket (confirmado en PLAN: reutiliza
  MediatR/FluentValidation/Mapster/EF Core ya presentes).

## Suppressions

Ninguna — no hubo hallazgos Medium que requirieran documentación de supresión.

## Resultado

Total: 19 categorías revisadas, 0 hallazgos Critical/High/Medium, 0 warnings.
