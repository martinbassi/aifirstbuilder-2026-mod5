# SAST — FEAT-011: Autocompletar dirección en el formulario de carga de mural

**Fecha:** 2026-08-30
**Alcance:** archivos tocados por los 3 bloques del spec (backend: proxy `AddressesController` +
`IdeUruguayAddressProviderClient` + queries; frontend: `address.service.ts` +
`create-mural-form.component.ts/html`).
**Resultado:** ✅ **PASSED** — 0 Critical, 0 High, 0 Medium sin mitigar.

## Secrets (F-SAST-01)
- ✅ `AddressProvider:BaseUrl` en `appsettings.json` es una URL pública fija (`https://direcciones.ide.uy`),
  no una credencial — el proveedor es gratuito y sin API key. Sin secretos hardcodeados en el diff.

## Injection
- ✅ F-SAST-02 (SQL/NoSQL): sin acceso a datos nuevo en este ticket, no aplica.
- ✅ F-SAST-03 (Command injection): sin `exec`/`spawn`/`system` en el diff.
- ✅ F-SAST-05 (Path traversal): sin manejo de rutas de archivo en este ticket.
- ✅ **SSRF (F-SAST-07, mitigación R5 del threat model):** `IdeUruguayAddressProviderClient.SearchAsync`/
  `ReverseGeocodeAsync` interpolan solo `q`/`lat`/`lng`, siempre vía `Uri.EscapeDataString`; el host
  (`HttpClient.BaseAddress`) viene fijo por configuración (`Program.cs`), nunca derivado de input de
  usuario. Confirmado en `IdeUruguayAddressProviderClient.cs:52-55,79-81`.

## XSS y funciones inseguras
- ✅ **F-SAST-06 (mitigación R6 del threat model):** grep sobre `create-mural-form.component.ts/html`
  y `address.service.ts` — 0 usos de `[innerHTML]`/`bypassSecurityTrustHtml`; el texto de dirección se
  interpola siempre con `{{ }}` (comentario explícito en el HTML confirma la decisión de diseño).
- ✅ F-SAST-04/17 (eval/deserialización insegura): `JsonSerializerOptions` estándar
  (`System.Text.Json`), sin deserialización dinámica.
- ✅ F-SAST-08 (crypto débil): no aplica, sin criptografía nueva en este ticket.

## Resto de categorías obligatorias
- ✅ **F-SAST-07 SSRF:** ver arriba.
- ✅ F-SAST-09 (debug en producción): sin cambios a configuración de entorno/Swagger.
- ✅ F-SAST-10 (logging de datos sensibles): `_logger.LogWarning(ex, "...")` en el cliente del
  proveedor solo registra el mensaje de excepción, nunca la dirección/coordenadas del usuario ni
  headers de sesión.
- ✅ F-SAST-11 (upload sin restricción): no aplica, sin manejo de archivos en este ticket.
- ✅ **F-SAST-12 (fuga de sesión, mitigación R4 del threat model):** `AddHttpClient<IAddressProviderClient,
  IdeUruguayAddressProviderClient>` es un typed client dedicado — no comparte `DelegatingHandler`
  con el resto de la API, por lo que ningún token/cookie de sesión puede llegar al proveedor externo
  (`Program.cs:104-116`). Ambos endpoints además exigen `[Authorize]` (`AddressesController.cs:22`).
- ✅ F-SAST-14 (validación de input incompleta): `SearchAddressesQueryValidator` exige
  `NotEmpty().MaximumLength(200)` sobre `Q`; `Reverse` recibe `lat`/`lng` como `double` tipados por
  el binder de ASP.NET Core.
- ✅ F-SAST-15 (manejo de errores que filtra internos): `AddressProviderUnavailableException` se
  traduce a 503 sin exponer detalles del proveedor externo ni stack traces; en el frontend
  `catchError` propaga vía `toApiError()` (nunca un catch vacío ni un swallow, regla de AGENTS.md).

## Rate limiting (mitigación R1 del threat model, no es una categoría F-SAST pero está en el
threat model de este ticket)
- ✅ Policy `"addresses"` (20 req/min por IP, `FixedWindowRateLimiter`), mismo esquema que
  `"discovery"` — limita tanto el abuso entrante a la API propia como el abuso saliente hacia el
  proveedor externo gratuito (`Program.cs:162-176`).

## Dependencias (F-SAST-13/16)
- ✅ `npm audit --omit=dev` (frontend): 0 vulnerabilidades.
- ✅ `dotnet list Paretto.sln package --vulnerable --include-transitive` (backend, los 4 proyectos):
  0 paquetes vulnerables.

## Suppressions
Ninguna. 0 hallazgos Medium que requieran documentación de excepción (§4.4).

---

**Total: 0 vulnerabilidades (0 Critical, 0 High, 0 Medium sin mitigar).**
**Next:** `gates.sast = true` → cerrar CODE, avanzar a VERIFY.

## Ronda 2 — 2026-08-30 (loop correctivo VERIFY, fix de AC-18)

**Alcance:** diff acotado de 4 archivos frontend (72 líneas) — `create-mural-form.component.ts/html/css/spec.ts` — agregando el mensaje visible de "sin resultados" del autocomplete de direcciones.

- ✅ F-SAST-06 (XSS): el nuevo mensaje ("No encontramos direcciones que coincidan.") es texto estático interpolado con `{{ }}`, no recibe ningún dato del proveedor externo ni de input de usuario.
- ✅ Sin nuevos endpoints, llamadas HTTP ni paths de input de usuario — solo lógica local nueva de signals/computed (`addressSearchResolved`, `addressNoResults`).
- ✅ F-SAST-13: sin dependencias nuevas (`git diff package.json` vacío).

**Total: 0 vulnerabilidades. `gates.sast = true`.**
