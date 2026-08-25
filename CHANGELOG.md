# Changelog

Todos los cambios notables de este proyecto se documentan en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.0.0/),
y este proyecto adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [Unreleased]

### Added

- **FEAT-001a — Autenticación básica**: registro y login de usuarios, sesión server-side (token
  opaco de 256 bits, hasheado con SHA-256, sin JWT), logout con invalidación real de la sesión,
  feature `auth` completa en Angular (formularios, guard de rutas, interceptor de sesión) conectada
  al backend vía cliente NSwag generado. Mitigaciones de seguridad incluidas: mensajes genéricos
  anti-enumeración de cuentas, rate limiting básico en `/login`/`/register`, CSP, elevación de
  privilegios prevenida (el rol nunca se toma del payload del cliente).
- **FEAT-001b — Crear mural**: carga de fotos de murales con geolocalización (automática o manual),
  almacenadas en Azure Storage con SAS de solo lectura de corta duración; validación NSFW
  (NsfwSpy) antes de publicar, dejando el mural `Pending`/`Rejected` según el resultado; validación
  de firma de bytes (magic number) en vez de confiar en `Content-Type`/extensión; límite de tamaño
  de request (~11MB) además del límite de 10MB por foto; formulario Angular con reintento sin
  perder los datos ya ingresados; ruta `/murals/new` protegida por sesión. Ningún mural
  `Pending`/`Rejected` es visible para nadie que no sea su dueño (404 genérico anti-enumeración).
- **FEAT-001c — Moderación mínima**: cierra el ciclo de vida del mural agregando el estado
  `Published`. Un Administrador puede listar murales pendientes (paginado server-side), aprobarlos o
  rechazarlos — los tres endpoints gateados con `[Authorize(Roles = "Administrator")]`, sin chequeo
  de rol manual. Pantalla Angular `/moderation` (guard de administrador, listado con paginación
  Anterior/Siguiente, aprobar/rechazar por ítem). El rol del usuario ahora viaja en la respuesta de
  login, únicamente para gatear la UI — la autorización real siempre se re-verifica server-side.
  Corrige además un `NullInjectorError` preexistente de FEAT-001b (`MuralsClient` nunca se había
  registrado en el injector de Angular).
- **FEAT-001d — Descubrir murales cercanos**: nuevo endpoint público
  `GET /api/discovery/nearby-murals` (`[AllowAnonymous]`, rate limit específico de 20 req/min por
  IP) que devuelve los murales `Published` dentro de un radio (default 5 km) ordenados por
  distancia ascendente, calculada con Haversine sobre un bounding box acotado en SQL vía un índice
  compuesto (`IX_Murals_Status_Latitude_Longitude`, ver ADR-005: Haversine en memoria en vez de
  `geography`/NetTopologySuite, decisión para el volumen de un MVP). Feature Angular `discovery/`
  (mapa Leaflet con un marcador por mural, lista con detalle inline al seleccionar, sin necesidad de
  sesión) accesible en `/discover`; ruteo raíz redirige a `/discover` con sesión activa o a `/login`
  sin ella. `GeolocationService` compartido, extraído de `create-mural-form` para reutilizarse en el
  descubrimiento.
- **FEAT-002 — Identidad visual**: tipografía Quicksand self-hosted (WOFF2 variable, sin depender de
  Google Fonts) aplicada globalmente con fallback al stack del sistema; paleta de colores primario
  coral (`#FE6944`) / secundario azul marino (`#0D2348`) vía variables CSS en `:root` sobre el
  theming de ng-zorro (ver ADR-006); logo compartido (`LogoComponent`, presentacional) en `/login` y
  `/register`; favicon reemplazado por el ícono del logo (pin + aerosol, multi-resolución
  16/32/48px). Incluye `frontend/scripts/verify-theme.mjs`, un script Node standalone (fuera del
  pipeline de `ng test`) que verifica por texto plano que la tipografía y las variables de color se
  aplican correctamente — resuelve una limitación real de Vitest/Angular (el CSS global no se
  inyecta en el DOM del test runner).
- **FEAT-003 — Rediseño visual de login/register**: `AuthCardComponent` reconstruido como
  split-screen (panel de marca con wordmark, mensaje e imagen de fondo, a la izquierda; panel de
  formulario a la derecha, ancho máximo ~410px), que colapsa a un solo panel de formulario a 100% de
  ancho por debajo de 700px. `login-form` y `register-form` comparten un único `auth-form.css`
  consolidado. Corrige un bug silencioso donde el ícono de Google no se renderizaba (faltaba
  `NzIconModule` en ambos formularios) y elimina imports muertos (`NzCardModule`, `LogoComponent`).
- **FEAT-004 — Sidebar de navegación global + navbar de contexto**: shell de navegación transversal
  en `core/layout/` — sidebar colapsable (breakpoint 992px) con logo, ítems Descubrir/Cargar
  mural/Moderación (este último condicional al rol `Administrator`), ruta activa resaltada y footer
  sesión-vs-anónimo (username+logout / CTA login-register); navbar superior con el título de la
  pantalla activa y un control de expandir/contraer compartido con el sidebar. Envuelve
  `/discover`, `/murals/new` y `/moderation`; `/login`/`/register` quedan fuera. Reutiliza
  `SessionStore`/`AuthService`/los guards existentes sin modificarlos.
- **FEAT-005 — Geolocalización y refetch de murales por área en /discover**: el mapa ahora se
  recentra reactivamente cuando la ubicación del visitante llega después del primer render
  (geolocalización asíncrona o coordenadas manuales) — antes quedaba fijo en el fallback aunque el
  permiso se concediera. Agrega un pin distintivo (`L.divIcon`, sin PNG nuevo) para "tu ubicación",
  y un botón "Buscar en esta área" que aparece al mover/hacer zoom el mapa (con una guarda para no
  confundir un movimiento del usuario con un recentrado programático) y vuelve a consultar murales
  cercanos usando el centro actual del mapa, manteniendo los resultados previos visibles durante la
  carga. Sin cambios de backend ni de contrato de API.

### Fixed

- **FEAT-001b**: reemplazado `InvariantGlobalization=true` (incompatible con
  `Microsoft.Data.SqlClient`) por `CultureInfo.DefaultThreadCurrentCulture` (ver ADR-004).
  Actualizado `Newtonsoft.Json` a 13.0.4 por una vulnerabilidad High transitiva vía
  `NsfwSpy → Microsoft.ML`.
- **FIX-001**: configurado CORS para desarrollo local — el backend nunca registraba
  `AddCors`/`UseCors`, bloqueando toda llamada del frontend (`http://localhost:4200`) a la API
  (`https://localhost:7126`) al correr ambos por separado. Policy `DevelopmentCors` gateada por
  `IsDevelopment()`, sin efecto en producción.
- **FIX-002**: corregidos 4 defectos que hacían inutilizable `/discover` en local — marcadores de
  Leaflet invisibles (resolución de íconos por defecto incompatible con el bundler de Angular),
  mapa centrado en `(0,0)` sin ubicación (ahora Montevideo), fotos de murales bloqueadas por la CSP
  contra el emulador local de Azure Storage (Azurite), y foto de detalle sin límite de tamaño
  (mismo defecto corregido también en la pantalla de moderación). La CSP relajada para el storage
  local queda acotada a la configuración `development` de Angular (`index.development.html`, vía
  override de `index` en `angular.json`) — nunca llega al build de producción.
