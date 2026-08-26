# PRD FEAT-001b: Crear mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-001b |
| Tracker | none |
| Date | 2026-08-15 |
| PRD loops | 1 |

## Context and Problem

Es el segundo sub-ticket de FEAT-001 (ver `docs/daw/prd/prd-FEAT-001.md`, índice del split), y
depende de FEAT-001a (sesión autenticada + modelo de usuario con rol). Sin este sub-ticket no existe
ningún mural en el sistema: la moderación (FEAT-001c) no tiene nada que revisar y el descubrimiento
(FEAT-001d) no tiene nada que mostrar. Es también donde se resuelve, por primera vez, cómo se
almacena y se sirve una fotografía — una decisión que FEAT-001c y FEAT-001d van a reutilizar sin
volver a definirla.

## Goals

- Que un usuario autenticado pueda registrar un mural con su fotografía y su ubicación, quedando en
  estado "pendiente".
- Que ninguna fotografía subida quede accesible por fuera de los controles de acceso del mural, ni
  siquiera mientras está "pendiente" o "rechazado".
- Que el contenido NSFW quede marcado automáticamente sin depender de que exista todavía un flujo de
  moderación humana.
- Que los casos límite del flujo (permisos denegados, imagen inválida, fallo de guardado) tengan un
  comportamiento explícito y probado.

## Functional Requirements

- FR-01: El sistema debe permitir subir una fotografía de un mural en formato JPEG, PNG o WebP.
  (RF-001)
- FR-02: El sistema debe rechazar una imagen que supere los 10 MB o no esté en formato
  JPEG/PNG/WebP, indicando el motivo del rechazo. (RF-010, RF-017)
- FR-03: El sistema debe impedir que el usuario continúe el registro del mural mientras la imagen
  adjunta no sea válida. (RF-018)
- FR-04: El sistema debe permitir asociar la ubicación GPS actual al mural. (RF-002)
- FR-05: El sistema debe permitir ingresar manualmente las coordenadas de latitud/longitud del
  mural. (RF-003)
- FR-06: El sistema debe detectar cuándo el usuario deniega los permisos de geolocalización y
  ofrecer el ingreso manual de ubicación sin interrumpir el flujo de registro. (RF-009, RF-016)
- FR-07: El sistema debe requerir que el usuario esté autenticado para acceder a la creación de
  murales, redirigiéndolo al flujo de autenticación si no lo está. (RF-026)
- FR-08: El sistema debe ejecutar una validación automática de contenido NSFW sobre cada imagen
  subida antes de guardar el mural. (RF-015)
- FR-09: El sistema debe cambiar el mural al estado "rechazado" cuando la validación NSFW detecta
  contenido inapropiado, impidiendo su publicación. (RF-015)
- FR-10: El sistema debe mantener el mural en estado "pendiente", sin bloquear el flujo de creación,
  cuando la validación NSFW falla o no responde. (RF-053)
- FR-11: El sistema debe almacenar el mural (fotografía, ubicación, fecha de creación y estado
  inicial "pendiente") al completarse el registro exitosamente. (RF-004)
- FR-12: El sistema debe detectar errores ocurridos durante el guardado del mural, sin registrarlo
  como guardado exitosamente, y notificar el error al usuario. (RF-011, RF-019)
- FR-13: El sistema debe preservar la fotografía y la ubicación ya ingresadas cuando ocurre un error
  de guardado, permitiendo reintentar sin volver a cargarlas. (RF-020, RF-024)
- FR-14: El sistema debe mostrar un mensaje de confirmación indicando que el mural quedó pendiente
  de revisión al completarse el guardado exitosamente.
- FR-15: El sistema debe servir cada fotografía de mural mediante una URL de acceso temporal
  firmada, de corta duración, generada en el momento de la respuesta. (RNF-009)
- FR-16: El sistema debe restringir el acceso a la fotografía de un mural en estado "pendiente" o
  "rechazado" únicamente a su dueño o a un usuario con rol Administrador. (RNF-009, en conjunto con
  RF-013)
- FR-17: El sistema debe requerir un título para el mural al momento de crearlo, de hasta 50
  caracteres, rechazando la creación si se omite o si lo excede. *(Agregado en PRD loop 1, a raíz de
  FIX-003: el commit 9cecf21 introdujo este campo como obligatorio sin que el PRD original lo
  documentara.)*

## Non-Functional Requirements

- NFR-01: El sistema debe aceptar imágenes de hasta 10 MB en formato JPEG, PNG o WebP. (RNF-003)
- NFR-02: La ubicación registrada mediante GPS debe tener una precisión igual o mejor a 50 metros.
  (RNF-004)
- NFR-03: El contenedor de almacenamiento de imágenes (Azure Storage) debe configurarse como
  privado, sin acceso público anónimo por URL directa, independientemente del estado del mural.
  (RNF-009)

## Acceptance Criteria

- AC-01: WHEN el usuario selecciona una fotografía válida en la pantalla de creación de mural, THE
  sistema SHALL permitir continuar con el registro. (FR-01)
- AC-02: IF la imagen supera los 10 MB o no está en formato JPEG/PNG/WebP, THEN THE sistema SHALL
  rechazarla, mostrar un mensaje con el motivo del rechazo, e impedir continuar el registro hasta
  que se cargue una imagen válida. (FR-02, FR-03)
- AC-03: WHEN el usuario otorgó permisos de ubicación y crea un mural, THE sistema SHALL asociar la
  ubicación GPS actual al registro. (FR-04)
- AC-04: IF el usuario deniega el permiso de geolocalización, THEN THE sistema SHALL detectar la
  denegación, no interrumpir el flujo de registro, y presentar el ingreso manual de coordenadas como
  alternativa. (FR-06)
- AC-05: WHEN el usuario ingresa manualmente coordenadas de latitud y longitud válidas, THE sistema
  SHALL asociar esa ubicación al mural. (FR-05)
- AC-06: IF el usuario no tiene sesión activa, THEN THE sistema SHALL impedir el acceso a la
  creación de murales y redirigirlo al flujo de autenticación. (FR-07)
- AC-07: WHEN la validación automática detecta contenido NSFW en la imagen subida, THE sistema SHALL
  cambiar el mural al estado "rechazado" e impedir su publicación. (FR-08, FR-09)
- AC-08: IF la validación automática de NSFW falla o no responde, THEN THE sistema SHALL mantener el
  mural en estado "pendiente", disponible para revisión manual futura, sin bloquear el flujo de
  creación. (FR-08, FR-10)
- AC-09: WHEN existe una fotografía válida y una ubicación válida y el usuario presiona "Guardar",
  THE sistema SHALL almacenar el mural con su fotografía, ubicación, fecha de creación y estado
  inicial "pendiente". (FR-11)
- AC-10: IF ocurre un error durante el proceso de guardado del mural, THEN THE sistema SHALL
  detectar el fallo, no registrar el mural como guardado exitosamente, y mostrar un mensaje de error
  visible al usuario. (FR-12)
- AC-11: WHEN ocurrió un error de guardado y el usuario retiene fotografía y ubicación válidas, THE
  sistema SHALL permitir reintentar el guardado sin solicitar nuevamente la fotografía ni la
  ubicación. (FR-13)
- AC-12: WHEN el registro de un mural se completa exitosamente, THE sistema SHALL mostrar un mensaje
  de confirmación indicando que el mural quedó pendiente de revisión. (FR-14)
- AC-13: WHEN el sistema responde con la fotografía de un mural, THE sistema SHALL servirla mediante
  una URL de acceso temporal firmada de corta duración, generada en el momento de la respuesta. (FR-15)
- AC-14: IF un mural está en estado "pendiente" o "rechazado" y quien consulta su fotografía no es
  su dueño ni tiene rol Administrador, THEN THE sistema SHALL rechazar el acceso a esa fotografía. (FR-16)
- AC-15: IF el usuario omite el título del mural o ingresa uno de más de 50 caracteres, THEN THE
  sistema SHALL rechazar la creación del mural con un error de validación indicando el motivo.
  (FR-17)
- AC-16: WHEN el usuario ingresa un título de hasta 50 caracteres junto con una fotografía y
  ubicación válidas, THE sistema SHALL aceptar y persistir el mural con ese título. (FR-17)

## Out of Scope

- **RF-014** Reportar mural.
- **RF-031, RF-032** "Mis murales" (listado propio con estado, eliminación). El usuario solo recibe
  el mensaje de confirmación de AC-12; no hay pantalla para volver a consultar sus murales en este
  sub-ticket.
- **RF-025, RF-027, RF-029** Aprobar, rechazar y listar murales pendientes — es FEAT-001c. Este
  sub-ticket deja el mural en "pendiente" o "rechazado" sin ningún camino de vuelta a "publicado".
- **RF-005 a RF-008, RF-012, RF-021** Búsqueda, mapa, lista, detalle y ordenamiento de murales
  cercanos — es FEAT-001d.
- **RF-013 (mitad pública)** La exclusión de murales "pendiente"/"rechazado" de las búsquedas y el
  mapa públicos se implementa en FEAT-001d, que es donde vive esa consulta. Este sub-ticket
  garantiza el estado inicial correcto (FR-11) y el control de acceso a la foto (FR-16); no
  implementa el endpoint de búsqueda.

## Risks and Mitigations

### Contenido inapropiado cargado por usuarios

**Riesgo:** imágenes NSFW subidas por usuarios, sin un camino de revisión manual todavía disponible
en este sub-ticket (FEAT-001c llega después).

**Mitigación:** validación automática NSFW obligatoria antes de guardar (FR-08/FR-09); todo mural
que no sea rechazado automáticamente queda en "pendiente" (FR-10), nunca accesible públicamente
(FR-16), a la espera de FEAT-001c.

### Exposición de fotografías de murales por URL pública

**Riesgo:** si el contenedor de almacenamiento quedara con acceso público por defecto, la fotografía
de un mural "pendiente" o "rechazado" sería descargable por cualquiera con la URL, sin pasar por
ningún control de acceso.

**Mitigación:** contenedor configurado explícitamente como privado desde este sub-ticket (NFR-03);
fotografías servidas exclusivamente mediante URLs firmadas de corta duración que respetan la
visibilidad del mural y el rol de quien consulta (FR-15, FR-16, AC-13, AC-14).

### Ubicaciones incorrectas

**Riesgo:** murales geolocalizados de forma errónea, dificultando su descubrimiento.

**Mitigación:** GPS como opción por defecto con precisión ≤50 m (NFR-02), con ingreso manual de
coordenadas como alternativa (FR-04, FR-05, FR-06). El reporte de ubicación incorrecta (RF-014)
queda fuera de este sub-ticket.

## Dependencies

- **FEAT-001a**: sesión autenticada (FR-07) y modelo de usuario con rol (FR-16 depende de que el rol
  Administrador exista como valor posible).
- Azure Storage (contenedor privado + generación de URLs SAS) para las fotografías de murales.
- Un servicio de validación NSFW (NsfwSpy o Azure AI Foundry, según defina PLAN) para FR-08/FR-09/FR-10.
- SQL Server 2025 + EF Core para la persistencia de murales.
- Geolocalización del navegador (API estándar) para FR-04/FR-06.
