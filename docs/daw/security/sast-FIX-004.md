# SAST FIX-004: Fix clasificación NSFW de imágenes WebP

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Fecha | 2026-08-26 |
| Alcance | `backend/src/Paretto.Infrastructure/Moderation/NsfwSpyClassifier.cs`, `backend/src/Paretto.Infrastructure/Paretto.Infrastructure.csproj` (solo comentario), `backend/tests/Paretto.Api.Tests/NsfwSpyClassifierTests.cs` |

## Secrets
✅ F-SAST-01: sin API keys, passwords, tokens ni connection strings en los archivos tocados.

## Injection
✅ F-SAST-02: sin queries SQL/NoSQL nuevas.
✅ F-SAST-03: sin comandos de sistema (`Process.Start`, `exec`, etc.).
✅ F-SAST-05: sin paths de archivo derivados de input de usuario.

## XSS y funciones inseguras
✅ F-SAST-06: no aplica (backend, sin renderizado HTML).
✅ F-SAST-04/17: sin `eval`/deserialización insegura. `MagickImage(byte[])` decodifica bytes ya
validados por firma mágica y tamaño (≤10MB, NFR-01) antes de llegar acá — mismo patrón de
confianza que ya usaba NsfwSpy internamente (ver threat-FIX-004.md).
✅ F-SAST-08: sin criptografía débil introducida.

## Otras categorías obligatorias
✅ F-SAST-07 (SSRF): sin llamadas de red nuevas.
✅ F-SAST-09 (debug mode): sin cambios de configuración de entorno.
✅ F-SAST-10 (logging de datos sensibles): sin logging nuevo; el catch-all existente de
`NsfwSpyContentScanner` (sin tocar) loguea la excepción, no bytes de imagen.
✅ F-SAST-11 (upload sin restricción): no aplica — el tamaño/formato ya se valida antes de esta capa.
✅ F-SAST-12 (CSRF): no aplica, sin endpoints nuevos.
✅ F-SAST-14 (validación de input incompleta): la detección WebP + reencode opera sobre bytes que ya
pasaron la validación de firma/tamaño de `CreateMuralCommandValidator`; una imagen corrupta hace
fallar el reencode, cuya excepción cae al catch-all existente (cubierto, ver threat-FIX-004.md).
✅ F-SAST-15 (error handling que filtra internals): la excepción de un reencode fallido nunca llega
al cliente — el catch-all la loguea como Warning y el mural cae a `Pending`, sin exponer detalles.

## Dependencias
✅ F-SAST-13/16: `dotnet list package --vulnerable --include-transitive` → 0 paquetes vulnerables en
los 4 proyectos del backend. Sin cambios de versión de paquetes en este fix (el csproj solo actualiza
un comentario).

## Suppressions
Ninguna.

---
Total: 15 checks limpios, 0 vulnerabilidades (0 Critical, 0 High, 0 Medium)
Resultado: **PASSED**
