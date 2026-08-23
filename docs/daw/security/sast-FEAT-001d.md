# SAST FEAT-001d: Descubrir murales cercanos

| Field | Value |
|-------|-------|
| Ticket | FEAT-001d |
| Date | 2026-08-23 |
| Scope | Todos los archivos de producción y test tocados por los 8 bloques (38 archivos, ver `git diff --stat main...HEAD`) |

## Secrets (F-SAST-01)

- ✅ Sin patrones de password/api-key/secret/token hardcodeados en el diff de producción. El único
  match de "password" en el diff completo es la propiedad `PasswordHash` (ya hasheada) en el
  snapshot de EF Core, preexistente — no introducido por este ticket.
- ✅ `.env` no aplica a este stack (config vía `appsettings.json`/env vars, sin cambios en este
  ticket).

## Inyección (F-SAST-02, F-SAST-03, F-SAST-05)

- ✅ SQL/NoSQL: `GetNearbyMuralsQuery` usa EF Core LINQ (`Where`, `OrderBy`, `Take`,
  `ToListAsync`) sobre `AppDbContext.Murals` — sin `FromSqlRaw`/`ExecuteSqlRaw` ni concatenación de
  strings. El bounding box (`GeoDistanceCalculator.BoundingBox`) y el Haversine
  (`GeoDistanceCalculator.HaversineKm`, ADR-005) son aritmética pura sobre `double`, sin superficie
  de inyección.
- ✅ Command injection: sin `Process.Start`, sin `child_process`, sin `eval()`.
- ✅ Path traversal: este ticket no toca manejo de archivos/blobs (reutiliza
  `IBlobStorageService.GenerateReadSasUrl` ya existente y ya auditado en FEAT-001b).

## XSS (F-SAST-06)

- ✅ Sin `innerHTML`/`bypassSecurityTrust`/`dangerouslySetInnerHTML` en el diff. Los templates
  nuevos (`discovery-list.component.html`, `discovery-page.component.html`,
  `discovery-map.component.html`) usan binding de Angular estándar (interpolación, `[src]`,
  `[attr.data-testid]`), que escapa por defecto.
- ✅ `DiscoveryMapComponent` (Leaflet) no usa `bindPopup`/`bindTooltip` con HTML de datos del
  servidor — el detalle del mural se maneja vía el `output()` `muralSelected` + el template
  Angular de `discovery-list`, nunca inyectando HTML crudo en el mapa.

## Funciones inseguras / criptografía débil (F-SAST-04, F-SAST-08, F-SAST-17)

- ✅ Sin `eval()`, sin deserialización insegura nueva.
- ✅ Sin MD5/SHA1/DES/ECB en el diff — este ticket no toca hashing de contraseñas ni tokens.

## Resto de categorías obligatorias (F-SAST-07, F-SAST-09, F-SAST-10, F-SAST-11, F-SAST-12)

- ✅ SSRF: sin llamadas salientes nuevas derivadas de input del usuario (`lat`/`lng`/`radiusKm` solo
  se usan en la query EF Core, nunca como URL/host de una petición saliente). El nuevo `img-src` de
  la CSP (`https://*.tile.openstreetmap.org`) es un origen fijo del lado del navegador para los
  tiles de Leaflet, no una URL derivada de input del usuario ni una llamada server-side.
- ✅ Debug mode en producción: sin cambios a la gating de Swagger
  (`app.Environment.IsDevelopment()`, ya corregido en FEAT-001a).
- ✅ Logging de datos sensibles: sin `_logger`/`Console.Write`/`console.log` nuevos en el diff de
  Discovery — ni coordenadas ni URLs firmadas se loguean.
- ✅ Unrestricted upload: este ticket no agrega ni modifica ningún endpoint de upload.
- ✅ CSRF: `GET /api/discovery/nearby-murals` es de solo lectura (sin efectos secundarios), no
  aplica protección CSRF. Es además el único endpoint público (`[AllowAnonymous]`) de este ticket;
  no se agregó ningún `POST`/`PUT`/`DELETE`.

## Medium — validación de input y manejo de errores (F-SAST-14, F-SAST-15)

- ✅ F-SAST-14: `GetNearbyMuralsQueryValidator` (FluentValidation) acota `Latitude` a
  `[-90, 90]`, `Longitude` a `[-180, 180]` y `RadiusKm` a `[0.1, 50]` cuando está presente —
  cubierto por `RadiusKm_out_of_range_throws_validation_exception` y
  `Latitude_or_longitude_out_of_range_throws_validation_exception` en
  `GetNearbyMuralsTests.cs`. `MaxResults = 200` en el Handler acota el costo de ordenar/serializar
  en el peor caso (mitigación R2 del threat model).
- ✅ F-SAST-15: el endpoint no introduce excepciones nuevas con mensajes propios — cualquier fallo
  cae en el mismo `IPipelineBehavior`/`ExceptionHandlingMiddleware` centralizado ya auditado en
  tickets anteriores, que no filtra stack traces ni detalles internos.

## Autorización y exposición de datos (RF-013, `.daw/rules/security.instructions.md`)

- ✅ `Status == Published` es siempre la primera cláusula del `Where` en
  `GetNearbyMuralsQueryHandler` — nunca condicional, mitigación R1 del threat model (un mural
  `Pending`/`Rejected` nunca es alcanzable por este endpoint público). Cubierto por
  `Returns_only_Published_murals_within_radius_excluding_out_of_radius_and_non_Published_inside_radius`.
  RF-013 (no exponer `pending` en búsqueda/mapa) sigue cumplido.
- ✅ `DiscoveryMappingConfig` mapea `Mural → NearbyMuralItemResponse` con una whitelist implícita: la
  respuesta solo expone `Id`, `PhotoUrl` (SAS URL de 5 min), `Latitude`, `Longitude`, `CreatedAt`,
  `DistanceKm` — ningún dato del uploader (email, id de usuario) se filtra al público anónimo.
  Cubierto por `PhotoUrl_is_a_valid_SAS_url`.
- ✅ `[AllowAnonymous]` en `NearbyMurals` es deliberado (FR-07, AC-07) — verificado que no exige
  `Authorization` (`Request_without_an_auth_header_returns_200_not_401`) y que sigue siendo un
  endpoint de solo lectura sin escritura alguna.
- ✅ Rate limiting específico ("discovery", 20 req/min por IP, `Program.cs`) además del límite
  global (100 req/min) — mitigación R3 del threat model contra scraping/DoS ligero del único
  endpoint sin autenticar de la API. Cubierto por
  `The_21st_request_in_one_minute_from_the_same_IP_returns_429`.

## Dependencias (F-SAST-13, F-SAST-16)

- ✅ `dotnet list package --vulnerable --include-transitive` (los 4 proyectos backend) → sin
  paquetes vulnerables.
- ✅ `npm audit --omit=dev` (frontend) → 0 vulnerabilidades.
- ✅ Única dependencia nueva: `leaflet` (Block 4), ya justificada en el spec/PLAN (mapa interactivo,
  no cubierto por ng-zorro/Angular) — sin CVEs conocidas a la fecha del audit.

## Suppressions

Ninguna — no hubo hallazgos Medium que requirieran documentación de supresión.

## Resultado

Total: 20 categorías revisadas, 0 hallazgos Critical/High/Medium, 0 warnings.

---

## Re-scan — 2026-08-23 (post corrective loop de VERIFY)

**Alcance del cambio:** un único archivo, `backend/tests/Paretto.Api.Tests/GeoDistanceCalculatorTests.cs`
— ajuste del test `BoundingBox_near_the_poles_does_not_throw_or_return_NaN_or_Infinity` (usa
`lat=90.0` en vez de `89.9` para ejercitar realmente la guarda de `GeoDistanceCalculator.cs:49`, y
afirma explícitamente `deltaLon == 180.0`). Sin cambios en código de producción, sin dependencias
nuevas, sin superficie de ataque nueva.

- ✅ Secrets (F-SAST-01): sin cambios, no aplica.
- ✅ Inyección/XSS/funciones inseguras: no aplica — el diff es un test unitario puro (`Assert.*`
  sobre valores `double`), sin input externo, sin I/O, sin red.
- ✅ Dependencias (F-SAST-13/16): `dotnet list package --vulnerable --include-transitive` (4
  proyectos backend) → sin paquetes vulnerables. Sin dependencias nuevas.
- ✅ Autorización y exposición de datos: sin cambios — el fix no toca `GetNearbyMuralsQuery`,
  `DiscoveryController` ni ningún endpoint.

**Resultado:** 0 hallazgos Critical/High/Medium, 0 warnings. `gates.sast` se re-otorga.
