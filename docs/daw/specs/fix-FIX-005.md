# Fix-plan FIX-005: Coordenadas 0,0 en sugerencias de calle+número del autocomplete de direcciones

| Field | Value |
|-------|-------|
| Ticket | FIX-005 |
| Tier | FIX |
| RCA | docs/daw/specs/rca-FIX-005.md |
| Date | 2026-08-30 |
| Spec loops | 1 (arch audit: 2 FAILs de nombrado en español resueltos, 2 WARNs incorporados) |

## Problem

Al seleccionar una sugerencia de dirección de tipo calle+número (`CALLEyPORTAL`) en el formulario de
carga de mural, la ubicación queda en `lat: 0, lng: 0` en vez de la ubicación real — rompiendo el
mini-mapa de confirmación y, si el usuario no lo nota, guardando el mural en una coordenada inválida.

## Root cause

`GET /api/v1/geocode/candidates` del proveedor externo `direcciones.ide.uy` nunca resuelve
coordenadas para resultados de tipo `CALLEyPORTAL` (calle+número exacto) — siempre devuelve
`lat: 0.0, lng: 0.0` para ese tipo, incluso para direcciones reales de Montevideo. Solo los
resultados `LOCALIDAD`/`POI` traen coordenadas reales en ese endpoint.
`IdeUruguayAddressProviderClient.SearchAsync` confía directamente en esos campos sin ninguna
resolución adicional. Detalle completo en `docs/daw/specs/rca-FIX-005.md`.

**Decisión de diseño (confirmada por el usuario en PLAN):** resolver la coordenada real recién al
**seleccionar** la sugerencia (no en cada tecleo de búsqueda) — 1 llamada extra solo cuando hace
falta, en vez de hasta ~10 llamadas extra por búsqueda debounced. El proveedor expone
`GET /api/v1/geocode/find?idcalle=&portal=&localidad=&type=` para esto, confirmado en vivo:

```
find(idcalle=8143, portal=1234, localidad=MONTEVIDEO, type=CALLEyPORTAL)
→ lat=-34.9059, lng=-56.1639
```

**Decisión de diseño (confirmada por el usuario en PLAN):** `AddressSuggestionDto` se reutiliza tal
cual como DTO de respuesta pública de `/search` y `/reverse` (decisión ya tomada en FEAT-011) — los
4 campos nuevos se agregan directamente a ese DTO en vez de introducir un tipo interno separado.
Quedan expuestos también en `/search` y `/reverse` (no solo en el endpoint nuevo); son metadatos no
sensibles del proveedor, sin implicancia de seguridad.

**Nombrado (corregido tras arch audit — AGENTS.md "Code conventions": todo el código va en
inglés):** el proveedor externo usa claves JSON en español (`idCalle`, `localidad`) que SOLO el wire
type privado (`IdeGeocodeResultWire`) puede replicar literalmente, y únicamente porque son claves de
deserialización — todo lo demás (DTO público, Query, parámetros del controller) usa nombres en
inglés (`StreetId`, `Locality`), mapeados a mano igual que ya hace `ToSuggestion` con `Address`/
`Lat`/`Lng` → `Address`/`Latitude`/`Longitude` hoy. `PortalNumber`/`Type` ya estaban en inglés desde
el borrador original.

## Solution — steps

1. `backend/src/Paretto.Infrastructure/Geocoding/IdeUruguayAddressProviderClient.cs` —
   `IdeGeocodeResultWire` (clase privada, líneas ~117-124): agregar `IdCalle` (int, `[JsonPropertyName("idCalle")]`
   si `System.Text.Json` no lo matchea case-insensitive por diferir en algo más que mayúsculas —
   verificar al implementar; el nombre de la propiedad puede quedar `IdCalle` acá porque es
   estrictamente un wire type de deserialización, no un tipo de dominio propio), `Localidad`
   (string, default `""`), `PortalNumber` (int), `Type` (string, default `""`). Nombres de JSON
   confirmados en vivo contra el proveedor real: `idCalle`, `localidad`, `portalNumber`, `type`.
2. Mismo archivo — `AddressSuggestionDto` (en `IAddressProviderClient.cs`): agregar 4 propiedades
   públicas **en inglés**: `StreetId` (int), `Locality` (string), `PortalNumber` (int), `Type`
   (string).
3. Mismo archivo — `ToSuggestion(IdeGeocodeResultWire wire)`: mapear `wire.IdCalle → StreetId`,
   `wire.Localidad → Locality`, `wire.PortalNumber → PortalNumber`, `wire.Type → Type` (mismo patrón
   de mapeo manual explícito ya usado para `Address`/`Lat`/`Lng`).
4. Mismo archivo — nuevo método `ResolveAsync(int streetId, int portalNumber, string locality,
   string type, CancellationToken ct)` en `IdeUruguayAddressProviderClient`, mismo contrato de
   retorno que `SearchAsync`/`ReverseGeocodeAsync` (`AddressProviderResult<AddressSuggestionDto?>`,
   nunca lanza). Llama a
   `api/v1/geocode/find?idcalle={streetId}&portal={portalNumber}&localidad={Uri.EscapeDataString(locality)}&type={Uri.EscapeDataString(type)}`
   (mismo criterio de `Uri.EscapeDataString` en `locality`/`type` que `SearchAsync`/
   `ReverseGeocodeAsync`, threat model R5 — los nombres de los QUERY PARAMS de la URL siguen siendo
   `idcalle`/`localidad` porque eso lo exige el proveedor externo, no nuestro código). Respuesta es
   un array en la raíz (mismo wire type) — si viene vacío, `Success` con `Data: null` (mismo
   criterio que `ReverseGeocodeAsync` para "sin resultados", no es un error); si hay elemento,
   `Success` con el primero mapeado. Excepción o timeout → `Unavailable` (mismo catch-all que los
   otros dos métodos).
5. `backend/src/Paretto.Infrastructure/Geocoding/IAddressProviderClient.cs` — agregar `ResolveAsync`
   a la interfaz, con el mismo estilo de doc-comment que `SearchAsync`/`ReverseGeocodeAsync`.
   Actualizar el doc-comment de la clase (líneas ~4-8) y de `AddressSuggestionDto` (líneas ~36-41)
   para mencionar el tercer handler.
6. `backend/tests/Paretto.Api.Tests/AddressesControllerTests.cs` — `FakeAddressProviderClient`
   (líneas 61-90): implementar `ResolveAsync` (mismo patrón que `ReverseGeocodeAsync`, con
   `_resolveData`/outcome configurables vía constructor).
7. Nueva Query: `backend/src/Paretto.Api/Features/Addresses/Queries/ResolveAddressQuery.cs` —
   mismo patrón que `ReverseGeocodeQuery.cs`: `ResolveAddressQuery { StreetId, PortalNumber,
   Locality, Type }`, `ResolveAddressResponse { Suggestion }`, validator (`NotEmpty` en
   `Locality`/`Type`, `GreaterThan(0)` en `StreetId`/`PortalNumber`), handler que llama
   `_addressProviderClient.ResolveAsync(...)` y lanza `AddressProviderUnavailableException` en
   `Unavailable`.
8. `backend/src/Paretto.Api/Api/Controllers/AddressesController.cs` — nueva acción
   `[HttpGet("resolve", Name = "ResolveAddress")]` (el `Name=` explícito es obligatorio para NSwag,
   mismo criterio ya documentado en las líneas 33-39/51-53 para `search`/`reverse` — ADR-003):
   ```csharp
   public async Task<IActionResult> Resolve(
       [FromQuery] int streetId,
       [FromQuery] int portal,
       [FromQuery] string locality,
       [FromQuery] string type,
       CancellationToken cancellationToken)
   ```
   `[FromQuery]` explícito en los 4 parámetros (no solo el primero), igual que `Reverse(
   [FromQuery] double lat, [FromQuery] double lng, ...)` — consistencia estilística señalada en el
   arch audit. Mismo `[Authorize]`+`[EnableRateLimiting("addresses")]` heredado de la clase.
9. `backend/tests/Paretto.Api.Tests/IdeUruguayAddressProviderClientTests.cs` — actualizar los
   fixtures de wire JSON existentes con los 4 campos nuevos (no rompen los tests actuales, son
   aditivos); agregar tests de `ResolveAsync`: éxito con coordenadas reales, sin resultados (array
   vacío → `Success`/`Data: null`), `HttpRequestException`/timeout → `Unavailable`.
10. `backend/tests/Paretto.Api.Tests/AddressesControllerTests.cs` — nuevos tests para
    `GET /api/addresses/resolve`: éxito (200 con coordenadas reales), sin resultado (200 con
    `suggestion: null`, no es un error), sin sesión (401), proveedor `Unavailable` (503), parámetros
    inválidos (`streetId`/`portal` ≤ 0, o `locality`/`type` vacíos → 422, mismo código que el resto
    del pipeline de FluentValidation del proyecto).
11. `backend/src/Paretto.Api/nswag.json` — regenerar el cliente (`nswag run nswag.json` desde
    `backend/src/Paretto.Api/`, con el backend corriendo en local) para que
    `frontend/src/app/core/api-client/api-client.generated.ts` incluya la nueva operación
    `resolveAddress` y los 4 campos nuevos de `AddressSuggestion` (`streetId`, `locality`,
    `portalNumber`, `type` — camelCase por convención de NSwag/TypeScript, ya en inglés).
12. `frontend/src/app/features/murals/data/address.service.ts` — dos cambios:
    - Nuevo método privado/interno `resolve(streetId, portalNumber, locality, type)` que llama a
      `AddressesClient.resolveAddress(...)`, mismo `catchError` → `toApiError()` que `search`/
      `reverseGeocode`.
    - Nuevo método público `resolveIfNeeded(suggestion: AddressSuggestion): Observable<AddressSuggestion | null>`
      que encapsula la regla de negocio del proveedor (hallazgo del arch audit: esa regla no debe
      vivir en el componente): si `suggestion.latitude === 0 && suggestion.longitude === 0`, llama a
      `resolve()` con los 4 campos de la sugerencia y devuelve el resultado (o `null` si el
      proveedor no pudo resolverlo); si la sugerencia YA trae coordenadas reales, devuelve
      `of(suggestion)` sin llamar a la red — mismo patrón de "regla propia encapsulada en el
      servicio" que ya usan `search()`/`reverseGeocode()` con sus propios criterios de error.
13. `frontend/src/app/features/murals/data/address.service.spec.ts` — tests de `resolveIfNeeded()`:
    con coordenadas ya reales → no llama a la red, devuelve la sugerencia tal cual; con 0,0 → llama
    a `resolve()` y devuelve el resultado; `resolve()` sin resultado o con 503 → devuelve `null`.
14. `frontend/src/app/features/murals/ui/create-mural-form.component.ts` —
    `onAddressSuggestionSelected(suggestion)` pasa a llamar SIEMPRE a
    `addressService.resolveIfNeeded(suggestion).subscribe(...)` (sin chequear coordenadas ella
    misma — esa regla ahora vive en el servicio), mismo patrón de `.subscribe({...})` ya usado en
    `requestGeolocation()` para `reverseGeocode()`:
    - Si el Observable emite una sugerencia con coordenadas → fijar `latitude`/`longitude`/
      `setCoordinatesInMap()` (comportamiento actual, sin cambios en el caso ya-resuelto: como
      `resolveIfNeeded` devuelve `of(suggestion)` de forma síncrona cuando no hace falta resolver,
      el `subscribe` se ejecuta en el mismo tick — los 3 tests existentes de esta rama NO necesitan
      `fakeAsync`/`tick`).
    - Si emite `null` (el proveedor tampoco pudo resolverlo, o falló) → revelar el fallback manual
      de lat/lng reutilizando `addressProviderUnavailable` (misma señal que ya cubre "el proveedor
      externo no responde", AC-19) — mismo concepto: el proveedor de direcciones no pudo resolver
      esta ubicación.
15. `frontend/src/app/features/murals/ui/create-mural-form.component.spec.ts` — los 3 tests
    existentes que llaman `onAddressSuggestionSelected()` usan sugerencias con coordenadas ya no
    nulas: siguen pasando sin cambios (mockear `addressService.resolveIfNeeded` devolviendo
    `of(suggestion)` para esos casos, o dejar que el fake real de `AddressService` en el spec siga
    funcionando si ya está mockeado a ese nivel — verificar al implementar). Nuevos tests:
    seleccionar una sugerencia `CALLEyPORTAL` con 0,0 → `resolveIfNeeded()` se llama y las
    coordenadas finales son las resueltas; `resolveIfNeeded()` devuelve `null` → revela el fallback
    manual (`data-testid="location-fallback-alert"`).

## Dependencies between steps

1-5 (backend/Infrastructure) → 6-10 (backend/tests + Query + Controller) → 11 (NSwag) → 12-15
(frontend). Estrictamente secuencial: el frontend no puede consumir la operación nueva hasta que
NSwag la regenere desde el backend ya implementado.

## Error handling

- Proveedor externo no responde/timeout en `/find` → `Unavailable` → `AddressProviderUnavailableException`
  → 503 (mismo criterio que `search`/`reverse`) → `resolveIfNeeded()` devuelve `null` → frontend
  revela fallback manual.
- `/find` responde sin resultados (array vacío) → `Success` con `Data: null` → 200 con
  `suggestion: null` → `resolveIfNeeded()` lo trata igual que un 503 a fines de UX (devuelve `null`,
  revela fallback manual), aunque el status HTTP sea distinto.
- `streetId`/`portal` ≤ 0 → 422 (FluentValidation, `GreaterThan(0)`). `locality`/`type` vacíos → 400,
  no 422 — un `string` no-nullable vacío en un query param dispara la validación automática de
  `[ApiController]` (nullable reference types habilitado en el `.csproj`) antes de que la request
  llegue al pipeline de FluentValidation, mismo comportamiento ya establecido por
  `Search_with_an_empty_q_returns_400` para el parámetro `q` de `search`. Ninguno debería ocurrir en
  uso normal (el frontend siempre los completa desde una sugerencia real) pero cubren el contrato
  público del endpoint.
- Sin sesión → 401 (`[Authorize]`, ya heredado).
- Más de 20 requests/minuto → 429 (policy `"addresses"` compartida a nivel de clase en
  `AddressesController` — no es una policy nueva por endpoint, así que el test ya existente
  `The_21st_request_in_one_minute_from_the_same_IP_against_search_returns_429` ya la ejercita; no
  hace falta un test nuevo específico de `resolve` para esto).

## Tests

- [ ] **Regression test** — `IdeUruguayAddressProviderClientTests`: `ResolveAsync` con los
  parámetros de una dirección real de Montevideo devuelve coordenadas no-cero (reproduce el bug
  ausente hoy: sin este método, no hay forma de resolver un `CALLEyPORTAL`).
- [ ] `IdeUruguayAddressProviderClientTests`: `ResolveAsync` sin resultados → `Success`/`Data: null`.
- [ ] `IdeUruguayAddressProviderClientTests`: `ResolveAsync` con `HttpRequestException`/timeout →
  `Unavailable`.
- [ ] `AddressesControllerTests`: `GET /api/addresses/resolve` con parámetros válidos y proveedor
  con resultado → 200 con coordenadas reales.
- [ ] `AddressesControllerTests`: `resolve` con proveedor `Success` pero sin dato (array vacío en
  `/find`) → 200 con `suggestion: null`, no es un error (mismo patrón que
  `Reverse_with_valid_coordinates_but_no_matches` para `reverse`).
- [ ] `AddressesControllerTests`: `resolve` con proveedor `Unavailable` → 503.
- [ ] `AddressesControllerTests`: `resolve` sin sesión → 401.
- [ ] `AddressesControllerTests`: `resolve` con `streetId`/`portal` ≤ 0 → 422.
- [ ] `AddressesControllerTests`: `resolve` con `locality`/`type` vacíos → 400 (validación
  automática de `[ApiController]`, no FluentValidation).
- [ ] `address.service.spec.ts`: `resolveIfNeeded()` con coordenadas ya reales no llama a la red;
  con 0,0 llama a `resolve()`; `resolve()` sin resultado o con 503 devuelve `null`.
- [ ] `create-mural-form.component.spec.ts`: seleccionar sugerencia `CALLEyPORTAL` con 0,0 llama a
  `resolveIfNeeded()` y fija las coordenadas resueltas.
- [ ] `create-mural-form.component.spec.ts`: `resolveIfNeeded()` devuelve `null` → revela el
  fallback manual (`data-testid="location-fallback-alert"`, mismo testid que AC-19).

## Regression risk

**Bajo.** El cambio es aditivo: 4 campos nuevos opcionales en `AddressSuggestionDto` (no rompen
ningún fixture existente, todos los tests actuales instancian el DTO con los campos que ya usaban),
un método nuevo en la interfaz (implementado en ambas clases que la implementan —
`IdeUruguayAddressProviderClient` y `FakeAddressProviderClient` de test), y `onAddressSuggestionSelected()`
pasa a depender de un Observable en vez de ser 100% síncrono — mitigado porque
`resolveIfNeeded()` devuelve `of(suggestion)` (síncrono) en el caso ya-resuelto, que es el que
ejercitan los 3 tests existentes; no necesitan `fakeAsync`/`tick`. Los 2 endpoints existentes
(`search`/`reverse`) no cambian su lógica, solo ganan 4 campos más en la respuesta.

## Rollback plan

Revertir el commit del fix. `IdeUruguayAddressProviderClient`/`AddressesController` vuelven a su
forma actual (2 métodos/endpoints, sin `ResolveAsync`/`resolve`); el frontend vuelve a usar las
coordenadas de `/candidates` tal cual (regresión conocida, ya presente en producción antes de este
fix — sin riesgo adicional al revertir). Indicador para aplicar el rollback: si el nuevo endpoint
`/api/addresses/resolve` introdujera algún error no contemplado (p. ej. el proveedor cambia la forma
de `/find` sin aviso), revertir no reintroduce ningún bug nuevo, solo restaura el conocido.
