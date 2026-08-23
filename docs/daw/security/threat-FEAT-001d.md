# Threat Model FEAT-001d: Descubrir murales cercanos

| Field | Value |
|-------|-------|
| Ticket | FEAT-001d |
| Spec | docs/daw/specs/spec-FEAT-001d.md |
| Date | 2026-08-22 |

## Componentes analizados

1. `GeoDistanceCalculator` (backend, nuevo — Block 1) — Domain, función pura.
2. `GetNearbyMuralsQuery`/Handler/Validator/`NearbyMuralResponse` (backend, nuevo — Block 2) —
   `Features/Discovery/`.
3. `DiscoveryController` (backend, nuevo — Block 3) — `GET /api/discovery/nearby-murals`,
   `[AllowAnonymous]`. **Primer endpoint público de todo el dominio Murals/Discovery** — hasta este
   ticket, todo lo relacionado a murales exigía `[Authorize]`.
4. `leaflet` + tiles de OpenStreetMap (frontend, nueva dependencia externa — Block 6).
5. `GeolocationService` (frontend, nuevo — Block 8) — pide `navigator.geolocation` al visitante.
6. `discovery.service.ts` + UI de exploración (frontend, nuevo — Block 9).
7. Ruteo raíz `/` (frontend, modificado — Block 10) — decide login vs. exploración según sesión.

## Trust boundaries (F-TM-02)

| Boundary | Ya existía / es nuevo |
|---|---|
| Internet (no confiable) ↔ `DiscoveryController` — **sin token, sin sesión** | **Nueva.** Es la primera vez que el dominio Murals expone datos sin `[Authorize]`. Decisión de producto ya tomada en el PRD (FR-07/AC-07: exploración pública sin sesión), no una omisión de este PLAN. |
| API ↔ SQL Server (EF Core, queries parametrizadas) | Ya existía. `GetNearbyMuralsQuery` sigue el mismo patrón (LINQ parametrizado, sin SQL crudo). |
| API ↔ Azure Storage (SAS de lectura, 5 min) | Ya existía (FEAT-001b/c), reutilizado sin cambios para `PhotoUrl` de murales publicados. |
| Navegador del visitante (no confiable) ↔ `tile.openstreetmap.org` (tercero externo, fuera del control del proyecto) | **Nueva.** Cada tile cargado revela la IP (y aproximadamente la ubicación) del visitante a un proveedor externo. Inherente al uso de cualquier servicio de mapas de terceros estándar (Google Maps, Mapbox, OSM comparten esta propiedad). |
| Navegador del visitante ↔ `navigator.geolocation` (API del propio navegador, con permiso explícito del usuario) | Nueva en el backend (nunca se pidió antes fuera de `create-mural-form`, que ya la usa de forma inline hoy). El dato nunca cruza al backend salvo como parámetro `lat`/`lng` del request. |

## Datos sensibles (F-TM-05 / F-TM-07)

- **Ubicación del visitante (lat/lng, vía geolocalización del navegador):** PII. **Nunca se
  persiste server-side** — se usa únicamente en memoria durante el procesamiento del request de
  `GetNearbyMuralsQuery` (bounding box + Haversine) y se descarta. `LoggingBehavior`
  (`backend/src/Paretto.Api/Common/Behaviors/LoggingBehavior.cs:9`) ya logea solo el nombre del
  tipo de request, nunca el contenido de sus campos — garantía de diseño ya existente, sin cambios
  necesarios. En tránsito viaja por HTTPS (`app.UseHttpsRedirection()` ya activo). F-TM-07: sin
  "reposo" para este dato (no se guarda en ninguna tabla ni log) → cifrado en reposo no aplica.
- **Ubicación y foto de murales publicados:** contenido deliberadamente público por diseño de
  producto (ese es el objetivo del sub-ticket: que cualquiera descubra murales publicados sin
  sesión). No es PII del visitante ni de un tercero — es contenido ya sujeto a moderación
  (FEAT-001c) antes de llegar a este endpoint.
- **`PhotoUrl` (SAS, 5 min TTL, solo lectura):** mismo patrón ya aceptado en FEAT-001b/c, sin
  cambios de clasificación.

## Riesgos (STRIDE)

| # | Riesgo | STRIDE | Likelihood | Impact | Mitigación |
|---|---|---|---|---|---|
| R1 | Un bug en el filtro de la query expone murales `Pending`/`Rejected` a un visitante sin sesión, violando RF-013 | Information Disclosure | Low | Critical | **Mitigado.** `GetNearbyMuralsQuery`/Handler filtra `Status == Published` de forma explícita (Block 2), y el spec incluye un test dedicado que verifica exclusión de `Pending`/`Rejected` sin importar la ubicación de quien consulta (Block 5), replicando el mismo patrón ya verificado en FEAT-001c. |
| R2 | Un actor sin autenticar satura `DiscoveryController` con requests concurrentes (endpoint público, sin costo de login) para degradar el servicio | Denial of Service | Medium | High | **Ya mitigado por diseño existente, sin trabajo nuevo.** El rate limiter global (`Program.cs:103-115`, `FixedWindowLimiter`, 100 req/min por IP, aplicado vía `app.UseRateLimiter()` antes de cualquier endpoint) cubre `DiscoveryController` automáticamente. Se suma el safety cap de resultados y el límite de `RadiusKm` (0.1–50 km, Block 2), que acotan el costo computacional de cada request individual. |
| R3 | Scraping geográfico exhaustivo: sin autenticación, un actor barre sistemáticamente coordenadas para reconstruir el dataset completo de murales publicados (ubicaciones exactas de todos ellos) | Information Disclosure | Medium | Low | **Mitigado (decisión del usuario, no aceptado).** Política de rate limiting nombrada `"discovery"` (`AddPolicy` en `Program.cs`, `[EnableRateLimiting("discovery")]` en `DiscoveryController`, Block 3), más estricta que el límite global: 20 req/min por IP (`FixedWindowLimiter`, misma partición por IP que el global). El global (100 req/min) sigue aplicando como piso general; esta política se le suma sobre el mismo endpoint. 20 req/min deja margen holgado para uso legítimo (recentrar el mapa, cambiar el radio unas pocas veces por minuto) sin permitir un barrido rápido de coordenadas. Si en producción igual se detecta abuso distribuido en múltiples IPs, evaluar CAPTCHA o un límite por sesión de navegador — fuera de alcance de este ticket. |
| R4 | La ubicación del visitante (geolocalización) queda registrada en algún log o tabla, filtrando un dato de PII | Information Disclosure | Low | Medium | **Ya mitigado por diseño existente, sin trabajo nuevo.** Ver "Datos sensibles" arriba: `LoggingBehavior` no logea contenido de campos, y `GetNearbyMuralsQuery`/Handler no persiste `Latitude`/`Longitude` del request en ninguna tabla — solo los usa en memoria para el cálculo. |
| R5 | Exposición de la IP/ubicación aproximada del visitante a OpenStreetMap (tercero) al cargar tiles del mapa | Information Disclosure | High | Low | **Riesgo aceptado, sin mitigación técnica del lado del backend.** Aceptado por: el usuario (esta conversación). Justificación: es una propiedad inherente al uso de cualquier proveedor de tiles de mapas estándar de la industria (Google Maps, Mapbox, OSM comparten esta propiedad); OpenStreetMap fue la opción elegida explícitamente por su costo cero y falta de API key. Condición de revisión: si el proyecto necesita cumplir un requisito de privacidad más estricto (p. ej. GDPR con opt-in explícito), evaluar un proxy de tiles propio o un proveedor con acuerdo de procesamiento de datos — fuera de alcance de este sub-ticket. |
| R6 | Ampliar el CSP (`img-src`) a `tile.openstreetmap.org` habilita un nuevo vector si ese host fuera comprometido o sufriera DNS hijacking | Tampering / Supply chain | Low | Low | **Mitigado por el alcance del cambio.** El host se agrega únicamente a `img-src` (Block 6) — nunca a `script-src` ni `connect-src`. Un `<img>` no ejecuta código; el CSP existente ya bloquea scripts inline y de terceros (`script-src 'self'`). |

Riesgos: 🔴 CRITICAL: 0 (R1 mitigado, no queda abierto) · 🟠 HIGH: 0 (R2 mitigado, no queda
abierto) · 🟡 MEDIUM: 1 (R4; R3 mitigado, no queda abierto) · 🟢 LOW: 2 (R5, R6).

Ningún riesgo CRITICAL o HIGH queda sin mitigar. No se requiere ningún cambio de arquitectura
adicional al ya definido en PLAN.

## Mitigaciones plegadas al spec

1. Test dedicado en Block 5 (`spec-FEAT-001d.md`): `GetNearbyMuralsQuery` excluye murales
   `Pending`/`Rejected` de los resultados, sin importar la ubicación de quien consulta (R1).
2. Nota explícita en Block 2: confirmar en el spec que el rate limiter global de `Program.cs`
   aplica sin cambios a `DiscoveryController` — no requiere código nuevo (R2).
3. Nueva política de rate limiting `"discovery"` en Block 3 (`Program.cs` +
   `[EnableRateLimiting("discovery")]` en `DiscoveryController`): 20 req/min por IP, sobre el
   límite global de 100 req/min — decisión explícita del usuario de mitigar R3 en vez de aceptarlo.
4. Nota explícita en Block 2: `GetNearbyMuralsQuery`/Handler nunca persiste `Latitude`/`Longitude`
   del visitante en ninguna tabla ni las incluye en logs más allá del nombre del tipo de request
   (R4, ya garantizado por `LoggingBehavior` existente).
5. R5 queda documentado como riesgo aceptado con las 3 condiciones de F-TM-04 (aceptado por,
   justificación, condición de revisión) — sin cambio de diseño.

Sin cambios de arquitectura respecto del diseño presentado en PLAN — R2, R4 y R6 ya estaban
cubiertos por controles existentes o por el alcance acotado del cambio; R3 queda mitigado con la
política de rate limiting específica; R5 queda como riesgo aceptado, documentado arriba.
