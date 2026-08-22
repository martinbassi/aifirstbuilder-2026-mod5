# Spec FEAT-001d: Descubrir murales cercanos

| Field | Value |
|-------|-------|
| Ticket | FEAT-001d |
| PRD | docs/daw/prd/prd-FEAT-001d.md |
| Tier | FEATURE |
| Date | 2026-08-22 |
| Spec loops | 0 |

## Summary

Se agrega un endpoint público (sin sesión) que devuelve los murales `Published` dentro de un radio
configurable alrededor de una ubicación, ordenados por distancia. La proximidad se calcula con un
bounding box aproximado en SQL (acotar el dataset con un índice compuesto sobre
`Status, Latitude, Longitude`) más la distancia exacta (Haversine) calculada en memoria sobre el
subconjunto resultante — sin migrar a `geography`/NetTopologySuite en este sub-ticket (ver ADR-005).
En el frontend se agrega una feature `discovery/` con mapa (Leaflet) y lista, un
`GeolocationService` compartido para pedir la ubicación del visitante, y la lógica de ruteo raíz que
decide entre login y exploración según haya sesión o no.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 2 |
| FR-02 | Block 2, Block 3 |
| FR-03 | Block 7 |
| FR-04 | Block 2, Block 7 |
| FR-05 | Block 2 |
| FR-06 | Block 7 |
| FR-07 | Block 3, Block 8 |
| FR-08 | Block 8 |
| NFR-01 | Strategy: índice compuesto `IX_Murals_Status_Latitude_Longitude` (Block 1) acota el bounding box a nivel SQL antes de traer filas a memoria; `GeoDistanceCalculator` calcula Haversine solo sobre ese subconjunto ya acotado; tope de 200 resultados (Block 2) limita el costo de ordenar/serializar en el peor caso. |

## Dependencies between blocks

Backend: 1 → 2 → 3 (cada uno depende del anterior; Block 1 provee el índice y el cálculo puro que
Block 2 usa, Block 2 provee la Query que Block 3 expone). Frontend: 4 → 5 (necesita el backend con
el endpoint ya expuesto, Block 3, para regenerar el cliente) → 6, 7 (pueden ir en paralelo entre sí,
ambos dependen de 5) → 8 (depende de 7, la ruta de exploración tiene que existir antes de
enrutarla).

## Block 1 — Índice compuesto, cálculo de distancia puro y ADR

**Files**
- `backend/src/Paretto.Infrastructure/Data/Migrations/{timestamp}_AddNearbyMuralsIndex.cs` (new) —
  migración EF Core.
- `backend/src/Paretto.Infrastructure/Data/AppDbContext.cs` (modified) — `HasIndex(m => new {
  m.Status, m.Latitude, m.Longitude })` en la configuración de `Mural`.
- `backend/src/Paretto.Domain/Services/GeoDistanceCalculator.cs` (new) — función pura.
- `backend/tests/Paretto.Api.Tests/GeoDistanceCalculatorTests.cs` (new) — unitarios puros, sin
  `WebApplicationFactory`.
- `docs/adr/adr-005-nearby-murals-haversine-sin-geography.md` (new).

**Logic**

`GeoDistanceCalculator.HaversineKm(double lat1, double lon1, double lat2, double lon2) -> double`:
fórmula de Haversine estándar (radio terrestre 6371 km), sin dependencias de EF Core/MediatR — puro
`Paretto.Domain`, tal como exige AGENTS.md ("Domain never depends on EF Core directly").

También en este archivo (o en un segundo método estático de la misma clase):
`GeoDistanceCalculator.BoundingBox(double lat, double lon, double radiusKm) -> (double MinLat,
double MaxLat, double MinLon, double MaxLon)`. Aproximación: `deltaLat = radiusKm / 111.0`;
`deltaLon = radiusKm / (111.0 * Math.Cos(DegreesToRadians(lat)))`.

El índice `IX_Murals_Status_Latitude_Longitude` no es espacial (SQL Server no ofrece uno para
columnas `float` sueltas) — es un índice B-tree compuesto que permite que el filtro `Status ==
Published` junto con el rango de `Latitude`/`Longitude` del bounding box use seek en vez de scan
completo de la tabla.

`docs/adr/adr-005-...md` documenta, siguiendo el formato de ADR-001..004: Context (tabla `Murals`
guarda `Latitude`/`Longitude` como `float` simple, sin columna `geography` ni índice espacial —
confirmado en el código, no hay `NetTopologySuite` en ningún `.csproj`), Options considered (A:
bounding box SQL + Haversine en memoria, sin dependencias nuevas, suficiente para el volumen de un
MVP; B: migrar a `geography` + `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`, más
correcto y escalable pero requiere una nueva dependencia NuGet, una migración que convierte
`Latitude`/`Longitude` a `geography` y un índice espacial real), Decision (Opción A para este
sub-ticket, decisión del usuario en PLAN), Consequences (NFR-01 se cumple para el volumen esperado
de un MVP; **la Opción B queda anotada explícitamente como mejora futura**, disparador: cuando los
tiempos de respuesta reales en producción dejen de cumplir NFR-01 por volumen de murales por zona).

**Error handling**

- Migración: sin lógica de negocio que pueda fallar más allá de un error DDL estándar de EF Core
  (reportado por la propia herramienta de migraciones); reversible con el `Down()` generado por EF
  Core (rollback: `dotnet ef database update {migración anterior}` — sin pasos manuales).
- `GeoDistanceCalculator.BoundingBox`: guarda explícita contra división por un coseno cercano a 0
  (latitudes cercanas a ±90°) — en ese caso devuelve `deltaLon = 180` en vez de propagar
  `Infinity`/`NaN` silenciosamente.

**Required tests**
- [ ] `GeoDistanceCalculator.HaversineKm` — distancia cero entre un punto y sí mismo.
- [ ] `GeoDistanceCalculator.HaversineKm` — distancia conocida entre dos coordenadas reales
  (ej. dos puntos ~5 km entre sí), con tolerancia razonable (±0.1 km).
- [ ] `GeoDistanceCalculator.BoundingBox` — el resultado contiene el punto de origen y un punto a
  `radiusKm` de distancia real (verificado con `HaversineKm`); no contiene un punto claramente fuera
  (ej. 3× el radio).
- [ ] `GeoDistanceCalculator.BoundingBox` — no lanza excepción ni devuelve `NaN`/`Infinity` en
  latitudes cercanas a ±90 (valida la guarda documentada arriba).

**Completion criterion**
Migración aplicada sin error contra la base local; los 4 tests de `GeoDistanceCalculator` pasan sin
tocar EF Core ni `WebApplicationFactory` (son tests unitarios puros); el ADR existe en `docs/adr/`
con las 4 secciones (Context, Options considered, Decision, Consequences) no vacías.

## Block 2 — `GetNearbyMuralsQuery` (Features/Discovery)

**Files**
- `backend/src/Paretto.Api/Features/Discovery/Queries/GetNearbyMuralsQuery.cs` (new) — Query,
  Handler, Validator, `NearbyMuralItemResponse`, `GetNearbyMuralsResponse` en el mismo archivo,
  siguiendo el patrón de `GetPendingMuralsQuery.cs`.
- `backend/src/Paretto.Api/Features/Discovery/Mappings/DiscoveryMappingConfig.cs` (new) — Mapster.
- `backend/tests/Paretto.Api.Tests/GetNearbyMuralsTests.cs` (new) — mismo patrón que
  `GetMuralByIdTests.cs`/`GetPendingMuralsTests.cs`: `IClassFixture<WebApplicationFactory<Program>>`,
  EF Core InMemory (nombre de DB único por test), `FakeBlobStorageService`.

**Logic**

`GetNearbyMuralsQuery : IRequest<GetNearbyMuralsResponse>` con `Latitude` (double), `Longitude`
(double), `RadiusKm` (double?, null → default 5 aplicado en el Handler, no en el Validator).

Handler (`GetNearbyMuralsQueryHandler`), inyecta `AppDbContext`, `IBlobStorageService`, `IMapper`:
1. `var radiusKm = request.RadiusKm ?? 5.0;`
2. `var (minLat, maxLat, minLon, maxLon) = GeoDistanceCalculator.BoundingBox(request.Latitude,
   request.Longitude, radiusKm);`
3. Query LINQ traducible a SQL (usa el índice del Block 1):
   ```csharp
   var candidates = await _db.Murals
       .Where(m => m.Status == MuralStatus.Published)
       .Where(m => m.Latitude >= minLat && m.Latitude <= maxLat)
       .Where(m => m.Longitude >= minLon && m.Longitude <= maxLon)
       .ToListAsync(cancellationToken);
   ```
4. En memoria (sobre `candidates`, ya acotado por el bounding box): calcular
   `GeoDistanceCalculator.HaversineKm` por cada mural, descartar los que excedan `radiusKm` (el
   bounding box es un rectángulo, no un círculo — hay esquinas dentro del box y fuera del radio
   real), ordenar ascendente por distancia, aplicar `Take(200)` (safety cap — mitigación R2 del
   threat model, ver abajo).
5. Mapear cada mural a `NearbyMuralItemResponse` con Mapster (`DiscoveryMappingConfig`), completar
   `PhotoUrl = _blobStorage.GenerateReadSasUrl(mural.PhotoBlobName, TimeSpan.FromMinutes(5))` y
   `DistanceKm` a mano tras el mapeo (mismo patrón que `PhotoUrl` en `MuralMappingConfig` — Mapster
   los `.Ignore()` porque no son propiedades de la entidad).

**Nota de seguridad (mitigación R1, R2 y R4 del threat model — `docs/daw/security/threat-FEAT-001d.md`):**
- El filtro `Status == Published` es explícito y es la primera cláusula `Where` — nunca se omite ni
  se hace condicional (R1).
- El cap de 200 resultados acota el costo de ordenar/serializar en el peor caso, además del rate
  limit de Block 3 (R2).
- `Latitude`/`Longitude` del request nunca se persisten en ninguna tabla ni se incluyen en logs —
  solo viven en memoria durante el procesamiento de este Handler; `LoggingBehavior` ya solo logea el
  nombre del tipo de request (R4, sin cambios necesarios en ese archivo).

**API contract**

- Method + path: `GET /api/discovery/nearby-murals?lat={lat}&lng={lng}&radiusKm={radiusKm}`
  (ruta y atributos del endpoint definidos en Block 3; el contrato de datos se fija acá y Block 3 lo
  referencia, no lo redefine).
- Request (query params): `lat` (double, requerido), `lng` (double, requerido), `radiusKm` (double,
  opcional, default 5).
- Response 200:
  ```json
  {
    "items": [
      {
        "id": "guid",
        "photoUrl": "string (SAS URL, válida 5 min)",
        "latitude": 0.0,
        "longitude": 0.0,
        "createdAt": "2026-08-22T00:00:00Z",
        "distanceKm": 0.0
      }
    ]
  }
  ```
  `items` puede ser una lista vacía (AC-06) — nunca `null`.
- Error codes: `400` (lat/lng/radiusKm fuera de rango, validado por FluentValidation).
- Auth: ninguna (`[AllowAnonymous]`, ver Block 3).

**Input validation**

`GetNearbyMuralsQueryValidator : AbstractValidator<GetNearbyMuralsQuery>`:
- `Latitude`: `InclusiveBetween(-90, 90)`.
- `Longitude`: `InclusiveBetween(-180, 180)`.
- `RadiusKm`: cuando no es null, `InclusiveBetween(0.1, 50)`.

**Error handling**

Los 3 campos fuera de rango devuelven `400` vía el `ValidationBehavior` ya existente en el pipeline
MediatR (mismo comportamiento que el resto de Commands/Queries del proyecto) — no se agrega manejo
de errores nuevo en el Handler.

**Required tests**
- [ ] Devuelve solo murales `Published` dentro del radio (excluye los que están fuera y los que
  están dentro pero `Pending`/`Rejected`) — valida AC-01, AC-02.
- [ ] Orden ascendente por `DistanceKm` — valida AC-05.
- [ ] Radio no especificado usa 5 km por defecto — valida AC-01.
- [ ] Sin murales `Published` en el radio → `items: []`, no error — valida AC-06.
- [ ] `radiusKm` fuera de `0.1..50` → `400`.
- [ ] `lat`/`lng` fuera de `-90..90`/`-180..180` → `400`.
- [ ] `PhotoUrl` es una SAS válida (mismo assert que `GetMuralByIdTests`/`GetPendingMuralsTests` vía
  `FakeBlobStorageService`) — valida AC-04.

**Completion criterion**
Todos los tests del Handler pasan contra EF Core InMemory + `FakeBlobStorageService`.

## Block 3 — `DiscoveryController` + rate limiting específico

**Files**
- `backend/src/Paretto.Api/Api/Controllers/DiscoveryController.cs` (new).
- `backend/src/Paretto.Api/Program.cs` (modified) — nueva política de rate limiting `"discovery"`.

**Logic**

```csharp
[ApiController]
[Route("api/discovery")]
public class DiscoveryController : ControllerBase
{
    [HttpGet("nearby-murals")]
    [AllowAnonymous]
    [EnableRateLimiting("discovery")]
    [SwaggerOperation(OperationId = "GetNearbyMurals")]
    [ProducesResponseType(typeof(GetNearbyMuralsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> NearbyMurals(
        [FromQuery] double lat, [FromQuery] double lng, [FromQuery] double? radiusKm,
        CancellationToken cancellationToken)
    { /* despacha a IMediator.Send(new GetNearbyMuralsQuery { ... }), sin lógica propia */ }
}
```

Contrato de request/response: el mismo ya fijado en el "API contract" de Block 2 — este bloque solo
lo expone vía HTTP, no lo redefine.

`OperationId = "GetNearbyMurals"` explícito (vía `[SwaggerOperation]`) es obligatorio, no opcional:
con `operationGenerationMode: MultipleClientsFromFirstTagAndOperationId` (ADR-003), un controller
nuevo sin `OperationId` explícito produce nombres poco semánticos en el cliente NSwag generado
(mismo problema que motivó ADR-003 para `MuralsController`). Con el atributo, NSwag genera
`DiscoveryClient.getNearbyMurals(...)` en `api-client.generated.ts` (Block 5).

**Mitigación R3 del threat model (`docs/daw/security/threat-FEAT-001d.md`):**

En `Program.cs`, dentro del `AddRateLimiter`:
```csharp
options.AddPolicy("discovery", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
```
20 req/min por IP, adicional al `GlobalLimiter` (100 req/min) ya existente — ambos aplican sobre el
mismo endpoint, el más estricto (20) es el que en la práctica limita.

**Error handling**

`429 Too Many Requests` cuando se excede la política `"discovery"` (`options.RejectionStatusCode`
global ya configurado, sin cambios). La validación `400` de parámetros ya está documentada y
testeada en Block 2 — no se re-declara como error propio de este bloque.

**Required tests**
- [ ] `GET /api/discovery/nearby-murals` sin header de autenticación devuelve `200` (no `401`) —
  valida AC-07.
- [ ] 21ª request en 1 minuto desde la misma IP devuelve `429`.

**Completion criterion**
El endpoint responde sin sesión; la política de rate limiting rechaza la request 21 dentro de la
ventana de 1 minuto.

## Block 4 — Leaflet: dependencia, estilos y CSP

**Files**
- `frontend/package.json` (modified) — agrega `leaflet` y `@types/leaflet`.
- `frontend/angular.json` (modified) — agrega
  `"./node_modules/leaflet/dist/leaflet.css"` al array `styles` (junto al de ng-zorro ya existente).
- `frontend/src/index.html` (modified) — CSP: `img-src 'self' data: https://*.tile.openstreetmap.org`
  (agrega el host de tiles a `img-src`, sin tocar `script-src`/`connect-src` — mitigación R6 del
  threat model, ver `docs/daw/security/threat-FEAT-001d.md`).

**Logic**

`npm install leaflet @types/leaflet`. Sin lógica de aplicación en este bloque — es la base para
Block 7.

**Error handling**

N/A: cambio de configuración estática (dependencia, hoja de estilos, cabecera CSP), sin lógica de
runtime propia que pueda fallar más allá de un error de build — ya cubierto por el completion
criterion.

**Required tests**
- [ ] `npm run build` (o el comando de build declarado en AGENTS.md) completa sin error tras
  agregar la dependencia.

**Completion criterion**
El build de producción incluye `leaflet.css` y no hay errores de CSP en la consola del navegador al
cargar una página de prueba con un tile de OpenStreetMap (verificable manualmente en Block 7, este
bloque solo deja la infraestructura lista).

## Block 5 — Regenerar cliente NSwag

**Files**
- `frontend/src/app/core/api-client/api-client.generated.ts` (regenerated, nunca editado a mano).

**Logic**

Con el backend de Blocks 2/3 ya compilando y exponiendo `GET /api/discovery/nearby-murals` con
`OperationId = "GetNearbyMurals"`, correr el comando de generación NSwag ya usado en tickets
anteriores (ver `backend/src/Paretto.Api/README.md`) para que `api-client.generated.ts` incluya
`DiscoveryClient.getNearbyMurals(lat, lng, radiusKm)`.

**Error handling**

N/A: regeneración de código a partir del OpenAPI del backend — un contrato inconsistente se
manifiesta como error de compilación TypeScript, ya cubierto por el completion criterion.

**Required tests**
- [ ] `DiscoveryClient` existe en el archivo regenerado con el método `getNearbyMurals` (chequeo de
  compilación TypeScript, no un test de runtime).

**Completion criterion**
`npx tsc --noEmit -p tsconfig.json` pasa sin error tras la regeneración.

## Block 6 — `GeolocationService` compartido

**Files**
- `frontend/src/app/shared/geolocation.service.ts` (new) — primer inquilino de `shared/` (no existía
  la carpeta; justificado porque hay dos consumidores reales: `create-mural-form` y la nueva feature
  `discovery/`, Block 7).
- `frontend/src/app/shared/geolocation.service.spec.ts` (new).
- `frontend/src/app/features/murals/ui/create-mural-form.component.ts` (modified) — usa el servicio
  en vez de `navigator.geolocation` inline (`requestGeolocation()`, líneas ~138-146 hoy).
- `frontend/src/app/features/murals/ui/create-mural-form.component.spec.ts` (modified) — pasa de
  stubear `navigator.geolocation` global a mockear `GeolocationService` inyectado.

**Logic**

`GeolocationService` (`@Injectable({ providedIn: 'root' })`) expone un método que envuelve
`navigator.geolocation.getCurrentPosition` en una `Promise`/`Observable`, resolviendo con
`{ latitude, longitude }` en éxito y rechazando con un error tipado (`GeolocationUnavailable` |
`GeolocationDenied` | `GeolocationTimeout`) en falla — mismo criterio de manejo de errores tipados
del resto del frontend (AGENTS.md, sección Frontend → Error handling). Mantiene el fallback ya
existente en `create-mural-form`: si el servicio rechaza, el componente sigue ofreciendo el input
manual de lat/lng que ya tiene hoy — este bloque extrae la llamada al navegador, no cambia el
comportamiento visible de `create-mural-form`.

**Error handling**

Los 3 casos (`unavailable`/`denied`/`timeout`) se distinguen a partir de `GeolocationPositionError.code`
del navegador (`PERMISSION_DENIED`, `POSITION_UNAVAILABLE`, `TIMEOUT`) y se mapean a un tipo de
error propio del servicio — nunca se propaga el objeto nativo del navegador hacia arriba.

**Required tests**
- [ ] `GeolocationService` resuelve con `{ latitude, longitude }` cuando el navegador concede el
  permiso (mock de `navigator.geolocation.getCurrentPosition`).
- [ ] Rechaza con el error tipado correspondiente en cada uno de los 3 casos (`denied`,
  `unavailable`, `timeout`).
- [ ] `create-mural-form.component.spec.ts` actualizado: los tests existentes de geolocalización
  pasan mockeando `GeolocationService` en vez de `navigator.geolocation` global — mismo
  comportamiento visible verificado (fallback a input manual cuando falla).

**Completion criterion**
Los specs de `GeolocationService` y los de `create-mural-form` (reescritos) pasan; ningún otro test
existente de `create-mural-form` se rompe.

## Block 7 — Feature `discovery/` (mapa + lista + detalle)

**Files**
- `frontend/src/app/features/discovery/data/discovery.service.ts` (new).
- `frontend/src/app/features/discovery/data/discovery.service.spec.ts` (new).
- `frontend/src/app/features/discovery/ui/discovery-map.component.ts` (new) — mapa Leaflet.
- `frontend/src/app/features/discovery/ui/discovery-map.component.spec.ts` (new).
- `frontend/src/app/features/discovery/ui/discovery-list.component.ts` (new) — lista ordenada +
  detalle inline al seleccionar + mensaje "sin resultados".
- `frontend/src/app/features/discovery/ui/discovery-list.component.spec.ts` (new).
- `frontend/src/app/features/discovery/ui/discovery-page.component.ts` (new) — compone mapa + lista,
  pide ubicación vía `GeolocationService`, dispara la consulta vía `discovery.service.ts`.
- `frontend/src/app/features/discovery/ui/discovery-page.component.spec.ts` (new).

**Logic**

`discovery.service.ts` envuelve `DiscoveryClient.getNearbyMurals(...)`, mapea errores tipados igual
que `mural.service.ts` (`catchError(toApiError)`) — nunca `HttpClient` directo desde el componente.

`discovery-page.component.ts`: al iniciar, pide la ubicación vía `GeolocationService` (Block 6); con
la ubicación obtenida (o la ingresada manualmente si el servicio falla), llama a
`discovery.service.ts` y guarda el resultado en un signal. Sin lógica de negocio en el template
(AGENTS.md, Frontend → Layer separation).

`discovery-map.component.ts`: `ViewChild`/`ElementRef` + Leaflet imperativo (decisión de PLAN:
`leaflet` directo, no `ngx-leaflet`) — inicializa el mapa centrado en la ubicación, un
`L.marker(...)` por mural (AC-03), popup/click que emite el mural seleccionado hacia el padre.

`discovery-list.component.ts`: lista ordenada por `distanceKm` (ya viene ordenada del backend, no
reordena), selección de un ítem muestra su detalle inline — foto (`photoUrl`, ya es la SAS URL),
fecha de creación, ubicación (AC-04) — **sin golpear ningún endpoint adicional**: todos los campos
del detalle ya vienen en la respuesta de `discovery.service.ts` (decisión de diseño explícita: evita
tener que exponer o modificar `GET /api/murals/{id}` para el caso anónimo). Si `items` está vacío,
muestra el mensaje de "sin resultados" (AC-06), sin botón ni lógica de ampliar el radio
automáticamente (Out of Scope del PRD, RF-021).

**Error handling**

Si `GeolocationService` rechaza y el usuario no ingresa coordenadas manuales, `discovery-page` no
dispara la consulta y muestra un estado que invita a ingresar la ubicación manualmente (mismo patrón
de fallback que `create-mural-form`). Si `discovery.service.ts` devuelve un `ApiError` (network,
`400`, `429`), `discovery-page` muestra un mensaje de error genérico — nunca se swallea el error
silenciosamente (AGENTS.md, Frontend → Error handling).

**Required tests**
- [ ] `discovery.service.spec.ts`: mapeo de respuesta exitosa y de errores tipados (mismo patrón que
  `mural.service.spec.ts`).
- [ ] `discovery-map.component.spec.ts`: un marcador por mural en `items` (AC-03); seleccionar un
  marcador emite el mural correspondiente.
- [ ] `discovery-list.component.spec.ts`: orden respetado tal como llega (no reordena); selección de
  un ítem muestra foto/fecha/ubicación (AC-04); `items: []` muestra el mensaje de sin resultados sin
  botón de ampliar radio (AC-06).
- [ ] `discovery-page.component.spec.ts`: con `GeolocationService` resolviendo, dispara la consulta
  con esas coordenadas; con `GeolocationService` rechazando, no dispara la consulta y ofrece el
  fallback manual; con `discovery.service.ts` devolviendo un `ApiError`, muestra el mensaje de error
  genérico sin swallear la falla.

**Completion criterion**
Todos los specs de la feature `discovery/` pasan; `discovery-page.component` renderiza sin errores
de consola en una prueba manual con el backend local corriendo (Block 3 ya expone el endpoint).

## Block 8 — Ruteo raíz y ruta pública de exploración

**Files**
- `frontend/src/app/app.routes.ts` (modified) — nueva ruta raíz `/` y ruta explícita
  `/discover` (o el nombre que se defina, pública) para `discovery-page.component`.
- `frontend/src/app/app.routes.spec.ts` (modified) — tests nuevos.

**Logic**

Ruta raíz `/`: no usa un guard de redirección server-driven — usa un `resolver`/componente liviano
(o una función en la config de rutas) que lee `SessionStore.isAuthenticated()` y navega a `/login`
si es `false`, o a `/discover` si es `true` (AC-08, AC-09). No se toca `authGuard`/`adminGuard`
existentes — la raíz es una decisión de presentación, no de autorización.

Ruta `/discover` (pública, sin `authGuard`): renderiza `discovery-page.component` directamente,
accesible sin sesión activa (AC-07, FR-07) — es el mismo componente al que redirige la raíz cuando
hay sesión, pero alcanzable también sin sesión navegando directo a la URL.

**Error handling**

N/A: ruteo puro, sin lógica de negocio ni llamadas externas — un mal cálculo de la ruta destino se
cubre directamente por los tests de este bloque, no hay un estado de error de runtime distinto de
"navega a la ruta incorrecta".

**Required tests**
- [ ] Con `SessionStore.isAuthenticated()` en `false`, navegar a `/` resuelve en `/login` — valida
  AC-08.
- [ ] Con `SessionStore.isAuthenticated()` en `true`, navegar a `/` resuelve en `/discover` — valida
  AC-09.
- [ ] Navegar directo a `/discover` sin sesión activa no redirige a `/login` (a diferencia de
  `/murals/new`, que sí lo hace vía `authGuard`) — valida AC-07.
- [ ] Los tests existentes de `authGuard`/`adminGuard` (`/murals/new`, `/moderation`) siguen pasando
  sin cambios de comportamiento.

**Completion criterion**
Los 4 tests nuevos pasan; ningún test existente de `app.routes.spec.ts` se rompe.

## Final verification

- `dotnet test` (backend) y el runner de frontend (Vitest) pasan con 0 fallos, incluyendo todos los
  tests listados en los 8 bloques.
- `npx tsc --noEmit -p tsconfig.json` pasa sin error.
- Un visitante sin sesión puede navegar a `/discover`, ver el mapa con murales publicados dentro de
  5 km por defecto, seleccionar uno y ver su detalle — sin que se le pida loguearse.
- Un mural `Pending`/`Rejected` sembrado directamente en la base (sin pasar por moderación) no
  aparece nunca en `/discover`, sin importar la ubicación de búsqueda.
- Abrir la app sin sesión activa en `/` muestra login; con sesión activa, muestra `/discover`.
- 21 requests en 1 minuto a `/api/discovery/nearby-murals` desde la misma IP devuelven `429` en la
  request 21.
- `docs/daw/security/threat-FEAT-001d.md` y `docs/adr/adr-005-nearby-murals-haversine-sin-geography.md`
  existen y están referenciados desde este spec.
