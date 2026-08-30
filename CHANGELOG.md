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
- **FEAT-006 — Popup de mural en el mapa de /discover**: al hacer click en un marcador del mapa
  ahora se abre un popup de Leaflet con el título del mural y su fecha de creación (antes el click
  no producía ningún feedback visual). El contenido se construye exclusivamente vía DOM API
  (`createElement`/`textContent`, nunca interpolación de string HTML) porque el título es texto
  libre de usuario sin sanitización HTML en el backend — mitiga un XSS almacenado de riesgo HIGH
  (ver threat model del ticket). La lista completa su AC-04 pendiente: título, foto, distancia,
  ubicación y fecha ahora se muestran siempre por fila, sin necesidad de click (el panel de detalle
  al seleccionar, mencionado en la entrada de FEAT-001d arriba, quedó reemplazado desde el rediseño
  Card→NzList).
- **FEAT-007 — Rehidratar sesión al recargar la página**: el rol del usuario (y por lo tanto el
  ítem de menú "Moderación" para administradores) ya no desaparece al hacer F5. El token de sesión
  sobrevivía al refresh, pero el usuario/rol solo vivía en memoria; ahora un nuevo endpoint
  `GET /api/auth/session` (`[Authorize]`, sin restricción de rol) devuelve `{username, role}` de la
  sesión actual, y `provideAppInitializer` lo consulta al arrancar la app (solo si hay un token
  guardado) antes de resolver cualquier ruta protegida. Un token inválido/expirado sigue
  redirigiendo a `/login` exactamente como antes, sin duplicar esa lógica.
- **FEAT-008 — NzFileUpload con preview en creación de mural**: el formulario de creación de mural
  reemplaza el `<input type="file">` nativo por `nz-upload` de ng-zorro (primer uso de este
  componente en el proyecto), con preview inmediato en miniatura, reemplazo y eliminación del
  archivo elegido, y las mismas validaciones de tipo (JPEG/PNG/WebP) y tamaño (≤10MB) de siempre —
  UX-only, el backend sigue siendo la autoridad real. Como `nzBeforeUpload` devolviendo `false` de
  forma síncrona impide que `nz-upload` dispare su propio evento de alta, el componente arma y
  revoca (`URL.revokeObjectURL`) el preview a mano, evitando memory leaks al reemplazar, quitar o
  destruir el formulario. Corrige además un bug de casing preexistente (`'pending'` vs `'Pending'`)
  que impedía mostrar el mensaje de confirmación tras un envío exitoso.
- **FEAT-009 — Migrar búsqueda de murales cercanos a geography + NetTopologySuite**: la ubicación
  del mural pasa de columnas `Latitude`/`Longitude` sueltas a una columna `geography`
  (`Point` de NetTopologySuite, SRID 4326), y el cálculo de cercanía pasa de Haversine + bounding
  box en memoria (ver ADR-005 original) a una consulta espacial nativa de SQL Server acelerada por
  un índice espacial. ADR-005 actualizado in-place: la migración se adopta ahora como mejora técnica
  proactiva, no porque se haya medido una degradación real de NFR-01. Contrato público de
  `GET /api/discovery/nearby-murals` y de creación de mural sin ningún cambio visible (mismos
  campos, mismos valores, mismo orden) — `Mural` gana propiedades computadas
  (`Latitude => Location.Y`, `Longitude => Location.X`) para que el mapeo siga funcionando sin
  tocarlo. Elimina `GeoDistanceCalculator` (Haversine + bounding box) y el índice B-tree
  `IX_Murals_Status_Latitude_Longitude`, ya reemplazados.
- **FEAT-010 — Marcador del centro de búsqueda en el mapa de /discover**: al usar "Buscar en esta
  área" se perdía la referencia visual de qué punto usaban las distancias mostradas en la lista — el
  marcador de "tu ubicación" (FEAT-005) quedaba fijo en el punto inicial. Ahora ese marcador refleja
  el centro de la última búsqueda exitosa; si el centro de búsqueda y la ubicación real del
  visitante están a menos de 50 metros, se muestra un solo marcador (evita duplicados casi
  superpuestos), y si están más lejos se muestran ambos, distinguibles por forma además de color
  (pin coral vs. círculo celeste) por accesibilidad. Sin cambios de backend.
- **FEAT-011 — Autocompletar dirección en el formulario de carga de mural**: reemplaza los inputs
  crudos de latitud/longitud como entrada primaria por un campo de dirección con autocomplete
  (debounce 300ms) contra `direcciones.ide.uy` (Uruguay), sin API key. El backend actúa de proxy
  dedicado (`AddressesController`, `[Authorize]`, rate limit propio de 20 req/min por IP) — el
  frontend nunca llama al proveedor externo directo, y el `HttpClient` que sí lo hace no comparte
  `DelegatingHandler` con el resto de la API (sin fuga de sesión). GPS exitoso precompleta la
  dirección vía reverse geocoding; seleccionar una sugerencia reutiliza el mini-mapa Leaflet ya
  existente. Fallback a los inputs manuales de lat/lng cuando el proveedor externo no responde
  (503), con un mensaje visible de "sin resultados" cuando la búsqueda no encuentra coincidencias.
  Corrige de paso una regresión no relacionada: el botón de reintentar tras un guardado fallido
  (AC-11) había quedado sin disparador en el HTML.

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
- **FIX-003**: corregidos los tests rotos por un commit directo a `main` que agregó `Title`
  obligatorio a `Mural` y dos converters de fecha UTC sin actualizar la suite. El helper de tests de
  creación de murales (backend) nunca enviaba `Title`, rompiendo 8 tests; los tests de formulario
  (frontend) tampoco lo enviaban, dejando de compilar. Corregido además el wiring del converter de
  fecha: estaba registrado en el `JsonOptions` de Minimal API, sin efecto sobre los controllers MVC
  reales que sirven la API — movido a `AddControllers().AddJsonOptions`. `prd-FEAT-001b.md`
  actualizado (FR-17/AC-15/AC-16) para documentar el campo `Title`, que había quedado sin
  trazabilidad en el PRD original.
- **FEAT-006**: `CalendarOutline` nunca se había registrado en `app.config.ts` pese a usarse desde
  antes del rediseño Card→NzList — el ícono de fecha estaba roto en producción (`IconNotFoundError`
  en tiempo de ejecución, mismo patrón que los gaps de registro de `MuralsClient`/`DiscoveryClient`
  corregidos en tickets anteriores). Encontrado al escribir los tests de la lista para este ticket.
- **FIX-004**: la validación NSFW nunca clasificaba de verdad una foto WebP — `NsfwSpy` reencoda
  WebP internamente con un entero (`(MagickFormat)179`) compilado contra `Magick.NET-Q16-AnyCPU`
  11.1.2 (donde esa posición del enum era `Png`), pero el proyecto pinnea esa dependencia a
  14.16.0 por seguridad, versión en la que la misma posición pasó a ser `Phm`; el WebP se
  reencodaba mal y la clasificación siempre fallaba, cayendo en silencio a `Pending` sin haber sido
  evaluada. `NsfwSpyClassifier` ahora reencoda WebP a PNG por nombre de enum (`MagickFormat.Png`)
  antes de llamar a NsfwSpy, evitando su branch interno roto.
- **QUICK-FIX-002**: `IdeUruguayAddressProviderClient` (FEAT-011) rompía en runtime con
  `InvalidOperationException` al construirse — `AddHttpClient<TClient,TImplementation>` no lograba
  desambiguar entre sus dos constructores, dejando `/api/addresses/search` y
  `/api/addresses/reverse` inutilizables. Corregido con `[ActivatorUtilitiesConstructor]`. Encontrado
  en prueba manual tras cerrar FEAT-011.
- **FIX-005**: las sugerencias de calle+número del autocomplete de direcciones (FEAT-011) quedaban
  en `lat: 0, lng: 0` al seleccionarlas — `/api/v1/geocode/candidates` del proveedor externo
  `direcciones.ide.uy` nunca resuelve coordenadas para ese tipo de resultado, incluso para
  direcciones reales de Montevideo. Se agregó un segundo llamado (`GET /api/addresses/resolve` →
  `/api/v1/geocode/find` del proveedor) disparado solo al seleccionar una sugerencia con
  coordenadas en 0 — no en cada tecleo de búsqueda, para no multiplicar las llamadas salientes al
  proveedor gratuito. Si tampoco puede resolverse ahí, revela el mismo fallback manual de
  latitud/longitud que ya existía para un proveedor caído.
