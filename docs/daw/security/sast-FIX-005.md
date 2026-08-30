# SAST — FIX-005: Coordenadas 0,0 en sugerencias de calle+número

**Fecha:** 2026-08-30
**Alcance:** archivos tocados por el fix-plan (backend: `IAddressProviderClient`,
`IdeUruguayAddressProviderClient`, `ResolveAddressQuery`, `AddressesController`; frontend:
`address.service.ts`, `create-mural-form.component.ts`).
**Resultado:** ✅ **PASSED** — 0 Critical, 0 High, 0 Medium sin mitigar.

## Secrets (F-SAST-01)
- ✅ Sin credenciales ni secretos nuevos.

## Injection
- ✅ **SSRF (F-SAST-07, mitigación heredada R5 de threat-FEAT-011.md):** `ResolveAsync` interpola
  `locality`/`type` siempre vía `Uri.EscapeDataString`, mismo criterio que `SearchAsync`/
  `ReverseGeocodeAsync`. El host (`HttpClient.BaseAddress`) sigue fijo por configuración.

## XSS y funciones inseguras
- ✅ F-SAST-06: sin `[innerHTML]` nuevo, sin cambios de renderizado del texto de dirección.

## Sesión/autorización
- ✅ **F-SAST-12 (mitigación heredada R4):** el nuevo endpoint `resolve` hereda
  `[Authorize]`+`[EnableRateLimiting("addresses")]` a nivel de clase de `AddressesController` — sin
  cambios en el mecanismo de autenticación/rate limiting.

## Validación de input
- ✅ F-SAST-14: `ResolveAddressQueryValidator` (`GreaterThan(0)` en `StreetId`/`PortalNumber`,
  `NotEmpty` en `Locality`/`Type`) + validación automática de `[ApiController]` para los parámetros
  `string` no-nullable vacíos (mismo comportamiento ya establecido por `search`'s `q`).

## Manejo de errores
- ✅ F-SAST-15: `ResolveAsync` nunca propaga excepción (mismo contrato never-throw que
  `SearchAsync`/`ReverseGeocodeAsync`); el frontend (`resolveIfNeeded`) atrapa el error y devuelve
  `null`, sin exponer detalles internos.

## Dependencias (F-SAST-13/16)
- ✅ `npm audit --omit=dev` (frontend): 0 vulnerabilidades.
- ✅ `dotnet list Paretto.sln package --vulnerable --include-transitive` (backend, los 4 proyectos):
  0 paquetes vulnerables. Sin dependencias nuevas (`ActivatorUtilitiesConstructor`/`find` son parte
  del framework/proveedor ya usados).

## Suppressions
Ninguna.

---

**Total: 0 vulnerabilidades (0 Critical, 0 High, 0 Medium sin mitigar).**
**Next:** `gates.sast = true` → cerrar CODE, avanzar a VERIFY.
