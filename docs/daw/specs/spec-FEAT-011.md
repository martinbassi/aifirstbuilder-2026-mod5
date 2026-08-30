# Spec FEAT-011: Autocompletar dirección en formulario de carga de mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-011 |
| PRD | docs/daw/prd/prd-FEAT-001b.md (PRD loop 2) |
| Tier | FEATURE |
| Date | 2026-08-29 |
| Spec loops | 0 |

## Summary

Se agrega un feature slice `Addresses` en el backend que actúa de proxy hacia el proveedor externo
`direcciones.ide.uy`, exponiendo `GET /api/addresses/search` (autocomplete) y
`GET /api/addresses/reverse` (geocodificación inversa). El cliente de infraestructura nunca propaga
excepciones del proveedor externo — devuelve un resultado tipado que el Handler traduce a una
excepción HTTP 503 propia cuando el proveedor no responde. En el frontend, `create-mural-form`
reemplaza los inputs crudos de latitud/longitud por un campo de dirección con autocomplete
(debounce 300ms), reutiliza el mini-mapa Leaflet ya existente para confirmar visualmente la
ubicación resuelta (por GPS o por selección), y conserva los inputs manuales de coordenadas como
fallback cuando el proveedor externo no está disponible.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-04 (modificado) | Block 1, Block 3 |
| FR-05 (modificado) | Block 1, Block 2, Block 3 |
| FR-06 (modificado) | Block 3 |
| FR-18 | Block 1, Block 3 |
| FR-19 | Block 1, Block 2 |
| FR-20 | Block 3 (reutiliza `setCoordinatesInMap()` ya existente) |
| NFR-04 | Strategy: `debounceTime(300)` de RxJS sobre el `Subject` del input de dirección, antes de llamar a `address.service.ts#search` (Block 3) |
| AC-03 | Block 1 (endpoint reverse), Block 3 (llamada desde `requestGeolocation()`) |
| AC-04 | Block 3 |
| AC-05 | Block 3 |
| AC-17 | Block 1 (endpoint search), Block 3 (debounce + render de sugerencias) |
| AC-18 | Block 1 (200 con lista vacía) |
| AC-19 | Block 1 (`AddressProviderUnavailableException` → 503), Block 2 (`address.service.ts` distingue el error), Block 3 (revela fallback manual) |
| AC-20 | Block 1, Block 2 (todo el tráfico pasa por `address.service.ts` → backend, nunca directo al proveedor) |
| AC-21 | Block 3 |

## Dependencies between blocks

Block 1 (backend) → Block 2 (regenera el cliente NSwag a partir del OpenAPI que expone Block 1) →
Block 3 (usa `address.service.ts` de Block 2). Orden estricto: 1 → 2 → 3.

---

## Block 1 — Backend: feature Addresses (proxy al proveedor externo)

**Files**
- `backend/src/Paretto.Infrastructure/Geocoding/IAddressProviderClient.cs` (new) — interfaz +
  `AddressSuggestionDto` + `AddressProviderOutcome` (enum: `Success`/`Unavailable`) +
  `AddressProviderResult<T>`.
- `backend/src/Paretto.Infrastructure/Geocoding/IdeUruguayAddressProviderClient.cs` (new) —
  implementación HTTP contra `direcciones.ide.uy`.
- `backend/src/Paretto.Api/Features/Addresses/AddressProviderUnavailableException.cs` (new) —
  excepción compartida por ambos Handlers de este bloque.
- `backend/src/Paretto.Api/Features/Addresses/Queries/SearchAddressesQuery.cs` (new) — Query +
  Handler + Validator + `SearchAddressesResponse`.
- `backend/src/Paretto.Api/Features/Addresses/Queries/ReverseGeocodeQuery.cs` (new) — Query +
  Handler + Validator + `ReverseGeocodeResponse`.
- `backend/src/Paretto.Api/Api/Controllers/AddressesController.cs` (new).
- `backend/src/Paretto.Api/Program.cs` (modified) — registra `AddHttpClient<IAddressProviderClient,
  IdeUruguayAddressProviderClient>` y la policy de rate limiting `"addresses"`.
- `backend/src/Paretto.Api/appsettings.json` (modified) — nueva sección `AddressProvider:BaseUrl`.
- `backend/tests/Paretto.Api.Tests/AddressesControllerTests.cs` (new) — tests HTTP vía
  `WebApplicationFactory<Program>`, con un `FakeAddressProviderClient : IAddressProviderClient`
  registrado en la factory de test (mismo patrón que `FakeBlobStorageService` en
  `DiscoveryControllerTests.cs`) para simular `Success`/`Unavailable` de forma determinística, sin
  golpear la API externa real.
- `backend/tests/Paretto.Api.Tests/IdeUruguayAddressProviderClientTests.cs` (new) — unit tests del
  cliente de infraestructura contra un `HttpMessageHandler` fake, mismo patrón de
  `NsfwSpyContentScannerTests.cs` (nunca propaga excepción, timeout acotado).

**Logic**

`IAddressProviderClient` expone:
```csharp
Task<AddressProviderResult<IReadOnlyList<AddressSuggestionDto>>> SearchAsync(string query, CancellationToken ct);
Task<AddressProviderResult<AddressSuggestionDto?>> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct);
```
`AddressProviderResult<T> { AddressProviderOutcome Outcome; T? Data; }`.

`IdeUruguayAddressProviderClient` llama a `GET {BaseUrl}/api/v1/geocode/candidates?q={query}` y
`GET {BaseUrl}/api/v1/geocode/reverse?latitud={lat}&longitud={lng}`, con `HttpClient.Timeout = 5s`
(mismo valor que `NsfwSpyContentScanner.DefaultScanTimeout`, aunque el mecanismo difiere: acá se usa
el `Timeout` nativo de `HttpClient` en vez de una carrera manual con `Task.WhenAny`, porque es una
llamada HTTP real, no una operación CPU-bound). Captura `HttpRequestException`,
`TaskCanceledException` (timeout) y cualquier error de deserialización con un `catch` que **loguea
`Warning`** (nunca vacío, mismo criterio que `NsfwSpyContentScanner`) y devuelve
`AddressProviderOutcome.Unavailable` — nunca propaga la excepción al Handler.

`SearchAddressesQueryHandler` / `ReverseGeocodeQueryHandler`: si `Outcome == Unavailable` →
`throw new AddressProviderUnavailableException()`. Si `Outcome == Success` → mapean `Data`
directamente al Response DTO (sin Mapster: son DTOs planos sin lógica de dominio, se reutiliza
`AddressSuggestionDto` de Infrastructure como el tipo interno del Response). `Data` vacío/nulo no es
un error: se serializa tal cual (lista vacía en `search`, `null` en `reverse`).

`AddressProviderUnavailableException : AppException` — mismo patrón que `MuralAccessDeniedException`
(`GetMuralByIdQuery.cs`): constructor sin parámetros, mensaje genérico fijo, `statusCode = 503`.
`ExceptionHandlingMiddleware` ya la traduce a `ProblemDetails` sin cambios adicionales (funciona para
cualquier `AppException`).

`AddressesController`: **solo** despacha a `IMediator.Send` — la traducción a 503 vive en el Handler
vía la excepción, nunca inspeccionando el resultado a mano en el Controller (hallazgo del
arch-auditor). Dos acciones `[HttpGet]`, cada una con `Name=` explícito (mismo motivo documentado en
`DiscoveryController.cs`/ADR-003: sin esto, dos GET en el mismo controller/tag arriesgan un nombre de
método generado no semántico o colisión en `AddressesClient`):

```csharp
[HttpGet("search", Name = "SearchAddresses")]
[HttpGet("reverse", Name = "ReverseGeocodeAddress")]
```

`Program.cs`:
```csharp
builder.Services.AddHttpClient<IAddressProviderClient, IdeUruguayAddressProviderClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AddressProvider:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(5);
});
```
Rate limiting: nueva policy `"addresses"` en `AddRateLimiter`, mismo esquema que `"discovery"`
(`FixedWindowLimiter` por IP, `PermitLimit = 20`, `Window = 1 minuto`) — el proveedor externo es
gratuito y sin key, así que limitar el abuso desde nuestro backend hacia él es tan importante como
limitar el abuso hacia nuestro propio backend.

`appsettings.json`: `"AddressProvider": { "BaseUrl": "https://direcciones.ide.uy" }` — **siempre
`https://`** (mitigación de threat modeling: tampering en tránsito hacia el proveedor externo).

**Mitigaciones de threat modeling incorporadas al diseño:**
- El `HttpClient` registrado para `IAddressProviderClient` es **dedicado** (vía
  `AddHttpClient<IAddressProviderClient, IdeUruguayAddressProviderClient>`), sin compartir ningún
  `DelegatingHandler` de autenticación con el resto de la API — ningún cookie/token de sesión del
  usuario sale hacia el proveedor externo, únicamente `q`/`lat`/`lng` (mitiga disclosure de
  credenciales a un tercero).
- El host del proveedor (`AddressProvider:BaseUrl`) es **fijo por configuración, nunca derivado de
  input del usuario** — el Handler/Cliente solo interpola `q`/`lat`/`lng` en la query string
  (`Uri.EscapeDataString`, nunca concatenación cruda), nunca el host o el path. Esto es lo que
  descarta SSRF clásico: un atacante no puede redirigir el destino de la llamada saliente.

**API contract**

- `GET /api/addresses/search?q={string}`
  - Auth: `[Authorize]` (requiere sesión, FR-07).
  - Request: `q` (string, query param, requerido).
  - Response 200: `{ "suggestions": [{ "address": string, "latitude": number, "longitude": number }] }` (puede ser `[]`).
  - Errores: `400` (validación), `401` (sin sesión), `503` (proveedor no disponible).

- `GET /api/addresses/reverse?lat={double}&lng={double}`
  - Auth: `[Authorize]`.
  - Request: `lat`, `lng` (query params, requeridos, double).
  - Response 200: `{ "suggestion": { "address": string, "latitude": number, "longitude": number } | null }`.
  - Errores: `400` (validación), `401` (sin sesión), `503` (proveedor no disponible).

**Data model**

Ninguno — este bloque no persiste nada ni modifica el esquema. Es un proxy sin estado.

**Input validation**

- `SearchAddressesQuery.Q`: `NotEmpty()`, `MaximumLength(200)`.
- `ReverseGeocodeQuery.Latitude`: `InclusiveBetween(-90, 90)` (mismo rango que
  `CreateMuralCommandValidator`).
- `ReverseGeocodeQuery.Longitude`: `InclusiveBetween(-180, 180)`.

**Error handling**

| Error | Manejo |
|---|---|
| `q` vacío o > 200 caracteres | `400` vía el pipeline de FluentValidation existente |
| `lat`/`lng` fuera de rango | `400` vía FluentValidation |
| Sin sesión activa | `401` vía `[Authorize]` (comportamiento estándar ya usado en `MuralsController`) |
| Proveedor externo no responde, timeout, o error de red | Cliente de infraestructura nunca lanza; Handler lanza `AddressProviderUnavailableException` → `503` vía `ExceptionHandlingMiddleware` (AC-19) |
| Proveedor externo responde sin coincidencias | No es un error: `200` con lista vacía (`search`) o `suggestion: null` (`reverse`) (AC-18) |
| Más de 20 requests/minuto desde la misma IP | `429` vía la policy `"addresses"` |

**Required tests**

- [ ] `AddressesControllerTests`: `search` con query válida y proveedor con resultados → `200` con lista no vacía (AC-17)
- [ ] `AddressesControllerTests`: `search` sin coincidencias → `200` con lista vacía (AC-18)
- [ ] `AddressesControllerTests`: `search` con `FakeAddressProviderClient` devolviendo `Unavailable` → `503`, nunca `500` (AC-19)
- [ ] `AddressesControllerTests`: `search` con `q` vacío → `400`
- [ ] `AddressesControllerTests`: `search` sin sesión → `401`
- [ ] `AddressesControllerTests`: `reverse` con coordenadas válidas y proveedor con resultado → `200` con `suggestion` (AC-03)
- [ ] `AddressesControllerTests`: `reverse` con coordenadas válidas pero sin coincidencias (proveedor `Success` con dato nulo) → `200` con `suggestion: null`, no es un error
- [ ] `AddressesControllerTests`: `reverse` con `Unavailable` → `503` (AC-19)
- [ ] `AddressesControllerTests`: `reverse` con `lat`/`lng` fuera de rango → `400`
- [ ] `IdeUruguayAddressProviderClientTests`: `HttpRequestException` del `HttpMessageHandler` fake → `Unavailable`, nunca propaga (sad path)
- [ ] `IdeUruguayAddressProviderClientTests`: respuesta tardía que excede el timeout de 5s → `Unavailable` (usa un timeout inyectable en el constructor para no esperar 5s reales en el test, mismo truco que `NsfwSpyContentScanner`)
- [ ] `IdeUruguayAddressProviderClientTests`: respuesta 200 válida → `Success` con los datos deserializados
- [ ] `AddressesControllerTests`: la request 21 en un minuto desde la misma IP contra `search` devuelve `429` (mismo patrón que `DiscoveryControllerTests.The_21st_request_in_one_minute_from_the_same_IP_returns_429`)

**Completion criterion**

Los 13 tests listados pasan; `dotnet build` sin warnings nuevos; `GET /api/addresses/search` y
`GET /api/addresses/reverse` responden según el contrato anterior verificado manualmente contra
Swagger UI en desarrollo.

---

## Block 2 — Frontend: cliente NSwag regenerado + `address.service.ts`

**Files**
- `frontend/src/app/core/api-client/api-client.generated.ts` (regenerated — **nunca editado a
  mano**, se regenera con `nswag run nswag.json` desde `backend/src/Paretto.Api/` con la API
  corriendo, según `backend/src/Paretto.Api/README.md`).
- `frontend/src/app/features/murals/data/address.service.ts` (new).
- `frontend/src/app/features/murals/data/address.service.spec.ts` (new).

**Logic**

Tras regenerar, verificar (igual que documentó ADR-003 para `MuralsClient`) que
`AddressesController` produce una única clase `AddressesClient` con dos métodos — gracias a
`Name=` explícito en Block 1 y a `operationGenerationMode: MultipleClientsFromFirstTagAndOperationId`
ya configurado, no debería hacer falta tocar `nswag.json`. Si el nombre generado no fuera semántico,
es una señal para revisar el `Name=` de Block 1, nunca para tocar `nswag.json` de nuevo (mismo
principio que cierra ADR-003).

`address.service.ts` envuelve `AddressesClient` (los componentes nunca lo llaman directo, AGENTS.md).
`AddressSuggestion` **no se redeclara a mano** — es un alias del tipo que NSwag genera para el DTO
del backend (mismo patrón que `MuralResponse`/`CreateMuralResponse` en `mural.service.ts`).

```typescript
export type AddressSuggestion = AddressSuggestionDto; // reexport del tipo generado

search(query: string): Observable<AddressSuggestion[]>   // 200 con [] no pasa por catchError
reverseGeocode(lat: number, lng: number): Observable<AddressSuggestion | null>
```

Ambos métodos usan `catchError((error: unknown) => throwError(() => toApiError(error)))` — mismo
patrón que `mural.service.ts` — para el caso `503` (AC-19). El caso "sin resultados" nunca llega a
`catchError`: es un valor 200 normal (lista vacía / `null`), que el componente (Block 3) distingue
por el propio valor, no por un error.

**API contract**

No agrega un endpoint nuevo — consume el de Block 1. N/A.

**Data model**

N/A.

**Input validation**

N/A — la validación vive en el backend (Block 1); este servicio solo transporta.

**Error handling**

| Caso | Manejo |
|---|---|
| Backend responde `503` | `catchError` → `toApiError` → `Observable` error tipado `ApiError`, propagado al componente |
| Backend responde `200` con lista/`suggestion` vacía | Valor normal emitido, no es un error |

**Required tests**

- [ ] `address.service.spec.ts`: `search()` con respuesta con resultados devuelve el array mapeado
- [ ] `address.service.spec.ts`: `search()` con respuesta `503` propaga un `ApiError` (sad path, AC-19)
- [ ] `address.service.spec.ts`: `search()` con lista vacía devuelve `[]` sin error (AC-18)
- [ ] `address.service.spec.ts`: `reverseGeocode()` con resultado devuelve la sugerencia
- [ ] `address.service.spec.ts`: `reverseGeocode()` con `suggestion: null` devuelve `null` sin error
- [ ] `address.service.spec.ts`: `reverseGeocode()` con respuesta `503` propaga un `ApiError` (sad path)
- [ ] `address.service.spec.ts`: `search()`/`reverseGeocode()` invocan `AddressesClient` (el cliente
  generado contra el backend propio) — nunca `HttpClient`/`fetch` directo a un host externo (AC-20)

**Completion criterion**

`AddressesClient` existe en `api-client.generated.ts` con las dos operaciones; los 7 tests de
`address.service.spec.ts` pasan; `npx tsc --build --noEmit tsconfig.json` sin errores.

---

## Block 3 — Frontend: `create-mural-form` — campo de dirección con autocomplete

**Files**
- `frontend/src/app/features/murals/ui/create-mural-form.component.ts` (modified).
- `frontend/src/app/features/murals/ui/create-mural-form.component.html` (modified).
- `frontend/src/app/features/murals/ui/create-mural-form.component.spec.ts` (modified).

**Logic**

- Nuevo import: `NzAutocompleteModule` (ng-zorro) y `address.service.ts` (Block 2), inyectado junto
  a los servicios existentes.
- Nuevo signal `addressQuery = signal<string>('')` (texto del input), `addressSuggestions =
  signal<AddressSuggestion[]>([])`, `addressProviderUnavailable = signal(false)` — **señal separada
  de `manualLocationRequired`** (hallazgo del arch-auditor: colapsarlas pierde la distinción
  semántica entre "GPS denegado" y "proveedor de direcciones caído", y el template necesita mensajes
  distintos para cada una).
- Un `Subject<string>` (`addressQuery$`) alimentado por el `(input)` del campo de dirección, con
  `debounceTime(300)` (NFR-04) + `distinctUntilChanged()` + `switchMap(query => query.trim().length
  === 0 ? of([]) : this.addressService.search(query))`, suscripto en el constructor/`ngOnInit` y
  desuscripto en `ngOnDestroy` (agregar a los `Subscription`/`takeUntilDestroyed()` ya usados en el
  proyecto para este tipo de flujo). El resultado alimenta `addressSuggestions`; un error (`ApiError`
  de 503) setea `addressProviderUnavailable.set(true)` en vez de propagarse como excepción no
  manejada (AC-19).
- `onAddressSuggestionSelected(suggestion: AddressSuggestion)`: setea `latitude`/`longitude`
  (signals ya existentes, sin cambiar lo que `submit()` envía a `MuralService.create()`) y llama a
  `setCoordinatesInMap({ latitude: suggestion.latitude, longitude: suggestion.longitude })` — método
  privado **ya existente**, hoy solo invocado desde `requestGeolocation()` (FR-20/AC-21).
- `requestGeolocation()` (existente): en el `then` de éxito, además de lo que ya hace, llama a
  `this.addressService.reverseGeocode(coordinates.latitude, coordinates.longitude)` y, si resuelve
  con una sugerencia no nula, setea `addressQuery.set(suggestion.address)` para precompletar el campo
  de texto (FR-04/AC-03). Si `reverseGeocode` falla con el error de proveedor caído, NO bloquea el
  flujo: el usuario ya tiene lat/lng por GPS y puede seguir sin dirección legible (el mapa igual
  muestra el pin).
- El bloque de inputs manuales de lat/lng (ya existente en el template) se muestra cuando
  `manualLocationRequired()` (GPS denegado, sin cambios) **o** `addressProviderUnavailable()`
  (proveedor caído, nuevo) — el template usa el signal correspondiente para elegir el mensaje
  (`"No pudimos obtener tu ubicación"` vs. `"El servicio de direcciones no está disponible"`).
- `canSubmit()` no cambia su lógica (sigue validando `latitude`/`longitude` con los mismos rangos) —
  el origen de esos valores (GPS, dirección seleccionada, o manual) es indistinto para el submit.
- **Mitigación de threat modeling:** los textos de dirección devueltos por el proveedor externo
  (sugerencias del autocomplete, dirección precompletada por reverse geocoding) se renderizan
  **siempre por interpolación/binding de Angular** (`{{ }}`, `[value]`, `[ngModel]`), **nunca**
  `[innerHTML]` ni `bypassSecurityTrustHtml` — mitiga una posible inyección si el proveedor externo
  (o un MITM) devolviera contenido malicioso en el campo `address`. Angular escapa por defecto
  cualquier interpolación; esta restricción solo prohíbe explícitamente la única vía que rompería esa
  garantía.

**API contract**

N/A — este bloque no agrega endpoints, consume `address.service.ts` (Block 2).

**Data model**

N/A.

**Input validation**

- El campo de dirección no tiene validación propia más allá de "no vacío para disparar la búsqueda"
  (`query.trim().length === 0` corta antes de llamar al servicio) — la validación real de la
  dirección ocurre implícitamente al requerir que el usuario seleccione una sugerencia (que ya trae
  lat/lng validados por el backend) o, en el fallback, al validar lat/lng como hoy.

**Error handling**

| Caso | Manejo |
|---|---|
| `address.service.ts#search` devuelve `ApiError` (503) | `addressProviderUnavailable.set(true)`, revela inputs manuales, no interrumpe el formulario (AC-19) |
| `address.service.ts#search` devuelve `[]` | `addressSuggestions.set([])`, el autocomplete muestra "sin resultados" (AC-18) |
| `address.service.ts#reverseGeocode` devuelve `ApiError` (503) durante el flujo GPS | No bloquea: el usuario sigue con lat/lng de GPS, sin dirección legible precompletada |
| `address.service.ts#reverseGeocode` devuelve `null` (sin match) | El campo de dirección queda vacío, el mapa igual muestra el pin de GPS |

**Required tests**

- [ ] `create-mural-form.component.spec.ts`: escribir en el campo de dirección dispara `search()`
  tras el debounce de 300ms, no antes (NFR-04/AC-17)
- [ ] Seleccionar una sugerencia setea `latitude`/`longitude` y llama a `setCoordinatesInMap()`
  (AC-05/AC-21)
- [ ] `search()` sin coincidencias muestra el estado "sin resultados" sin marcar
  `addressProviderUnavailable` (AC-18)
- [ ] `search()` con error 503 setea `addressProviderUnavailable` y revela los inputs manuales de
  lat/lng, sin bloquear el resto del formulario (AC-19, sad path)
- [ ] Con permiso de geolocalización otorgado, `reverseGeocode()` exitoso precompleta el campo de
  dirección (AC-03)
- [ ] Con permiso de geolocalización otorgado pero `reverseGeocode()` con error 503, el flujo GPS
  sigue funcionando (lat/lng seteados, mapa con pin) sin precompletar el texto (sad path)
- [ ] Con permiso de geolocalización otorgado pero `reverseGeocode()` devuelve `null` (sin match), el
  campo de dirección queda vacío y el mapa igual muestra el pin de GPS (sad path)
- [ ] Con permiso de geolocalización denegado, se muestra el fallback manual por
  `manualLocationRequired` (comportamiento existente, regresión)
- [ ] `canSubmit()` sigue validando los rangos de lat/lng sin importar el origen del valor
  (regresión)

**Completion criterion**

Los 9 tests anteriores pasan (más los ya existentes del componente, sin regresiones); el formulario
renderiza el campo de dirección con autocomplete y el mini-mapa se actualiza tanto por GPS como por
selección de sugerencia, verificado manualmente en el navegador.

---

## Final verification

- Los 3 bloques completos, sus tests en verde (backend + frontend).
- `dotnet build` y `npx tsc --build --noEmit tsconfig.json` sin errores.
- Lint (ESLint) sin errores nuevos.
- SAST limpio, en particular sobre `IdeUruguayAddressProviderClient` (URL externa fija, sin
  interpolar input de usuario en el host — solo en query params) y sobre la policy de rate limiting
  nueva.
- Verificación manual: crear un mural escribiendo una dirección (autocomplete), crear un mural
  aceptando la ubicación por GPS (dirección precompletada), y crear un mural con el proveedor externo
  inalcanzable (cortando red o apuntando `AddressProvider:BaseUrl` a un host inválido) para confirmar
  que el fallback manual aparece y el registro no queda bloqueado.
