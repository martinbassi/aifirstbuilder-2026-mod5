# SAST FIX-001: Configurar CORS para desarrollo local

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Date | 2026-08-23 |
| Scope | `backend/src/Paretto.Api/Program.cs`, `backend/src/Paretto.Api/appsettings.Development.json`, `backend/tests/Paretto.Api.Tests/CorsTests.cs` |

## Secrets (F-SAST-01)

- ✅ Sin patrones de password/api-key/secret/token hardcodeados. El único dato agregado es un origin
  de texto plano (`http://localhost:4200`), no un secreto.

## Inyección (F-SAST-02, F-SAST-03, F-SAST-05)

- ✅ Sin queries nuevas, sin `Process.Start`/`child_process`, sin manejo de archivos/paths. `AddCors`/
  `UseCors` son configuración de framework, sin superficie de inyección.

## XSS (F-SAST-06)

- ✅ No aplica — sin templates ni output HTML involucrado.

## Funciones inseguras / criptografía débil (F-SAST-04, F-SAST-08, F-SAST-17)

- ✅ Sin `eval()`, sin deserialización insegura, sin hashing/crypto nuevo.

## Resto de categorías obligatorias (F-SAST-07, F-SAST-09, F-SAST-10, F-SAST-11, F-SAST-12)

- ✅ SSRF: sin llamadas salientes nuevas.
- ✅ Debug mode en producción: sin cambios al gating de Swagger (`IsDevelopment()`, ya auditado en
  FEAT-001a); `AddCors`/`UseCors` usan el mismo gate.
- ✅ Logging de datos sensibles: sin logging nuevo.
- ✅ Unrestricted upload: no aplica, sin endpoint de upload tocado.
- ✅ CSRF: no aplica, el único endpoint usado como sonda en los tests (`GET /api/discovery/nearby-murals`)
  es de solo lectura y preexistente, sin cambios de método ni de superficie de escritura.

## Configuración CORS específica (buena práctica, más allá del catálogo genérico)

- ✅ Sin `AllowAnyOrigin()` en ningún punto — whitelist explícita vía `WithOrigins(allowedOrigins)`,
  leída de configuración.
- ✅ Sin `AllowCredentials()` — la autenticación usa `Authorization: Bearer <token>`, no cookies, así
  que la combinación peligrosa clásica (`AllowAnyOrigin()` + `AllowCredentials()`, que el navegador
  normalmente rechaza pero que algunas configuraciones erróneas fuerzan con
  `SetIsOriginAllowed(_ => true)`) no aplica ni por accidente.
- ✅ La policy completa (`AddCors`) vive dentro de `if (builder.Environment.IsDevelopment())` —
  Production nunca la registra (mitigación R1 del threat model, verificada por el test
  `AddCors_is_not_registered_when_the_host_runs_outside_Development`).

## Medium — validación de input y manejo de errores (F-SAST-14, F-SAST-15)

- ✅ F-SAST-14: no aplica — `Cors:AllowedOrigins` es configuración estática del deploy (appsettings),
  no input de una request de usuario.
- ✅ F-SAST-15: sin excepciones nuevas con mensajes propios; el fallback `?? Array.Empty<string>()`
  evita una excepción no manejada en vez de generar una nueva.

## Dependencias (F-SAST-13, F-SAST-16)

- ✅ `dotnet list package --vulnerable --include-transitive` (los 4 proyectos backend) → sin paquetes
  vulnerables.
- ✅ Sin dependencias nuevas — `AddCors`/`UseCors` son parte de `Microsoft.AspNetCore.App`, ya
  referenciado por el proyecto.

## Suppressions

Ninguna — no hubo hallazgos Medium que requirieran documentación de supresión.

## Resultado

Total: 15 categorías revisadas, 0 hallazgos Critical/High/Medium, 0 warnings.
