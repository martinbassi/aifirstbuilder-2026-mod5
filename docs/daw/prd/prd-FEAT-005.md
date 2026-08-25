# PRD FEAT-005: Geolocalización funcional y refetch de murales según el área del mapa en /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-005 |
| Tracker | none |
| Date | 2026-08-25 |
| PRD loops | 0 |

## Context and Problem

FEAT-001d (`docs/daw/prd/prd-FEAT-001d.md`) construyó la exploración de murales cercanos: `/discover`
pide la ubicación del visitante, consulta el backend por murales publicados dentro de un radio y los
muestra en mapa (Leaflet) y lista. Esa base funciona, pero dos comportamientos quedaron rotos porque
`DiscoveryMapComponent` solo resuelve el centro del mapa **una vez**, en `ngAfterViewInit()`, y nunca
vuelve a reaccionar después:

1. **La geolocalización no centra el mapa.** `GeolocationService.getCurrentPosition()` es asíncrono
   (permiso del navegador + fix GPS), así que el `center` del mapa llega **después** de que
   `ngAfterViewInit()` ya calculó el centro inicial con el valor que hubiera en ese momento (`null`
   la primera vez). El mapa se dibuja con el fallback fijo (Montevideo) y se queda ahí para siempre,
   aunque el permiso se haya concedido y `center` se actualice más tarde. Tampoco hay ningún marcador
   que indique dónde está el visitante.
2. **El mapa no vuelve a pedir murales al moverlo.** Mover el mapa, hacer zoom o recentrarlo no
   dispara ninguna consulta nueva — los resultados quedan fijos a la carga inicial (o a la última
   búsqueda manual), sin importar qué área esté mirando el usuario en ese momento.

Este ticket corrige ambos comportamientos y agrega la interacción explícita que permite re-consultar
murales según el área que el usuario decide explorar.

## Goals

- Que el mapa de `/discover` se centre de forma confiable en la ubicación real del visitante en
  cuanto esté disponible (geolocalización del navegador o coordenadas manuales), sin importar cuándo
  llegue ese dato respecto del render inicial del mapa.
- Que el visitante pueda ver claramente dónde está él mismo en el mapa, distinguible de los murales.
- Que el visitante pueda explorar murales de un área distinta a la de su ubicación inicial, moviendo
  el mapa y pidiendo explícitamente resultados nuevos para lo que está viendo.

## Functional Requirements

- FR-01: El sistema debe centrar el mapa en la ubicación resuelta (geolocalización del navegador o
  coordenadas manuales) en cuanto esa ubicación esté disponible, incluso si el mapa ya se había
  renderizado antes con un centro provisorio.
- FR-02: El sistema debe mostrar un marcador distintivo (ícono/color propio, distinto del de los
  murales) en la ubicación resuelta del visitante, tanto si proviene de geolocalización del
  navegador como de las coordenadas manuales del formulario de respaldo.
- FR-03: El sistema debe mostrar un botón para volver a buscar murales cercanos usando el centro
  actual del mapa, visible después de que el usuario mueva el mapa (arrastre) o cambie el nivel de
  zoom.
- FR-04: Al presionar ese botón, el sistema debe consultar murales publicados usando el centro
  actual del mapa como punto de referencia y el radio por defecto (5 km, FR-01 de FEAT-001d), y
  reemplazar los marcadores y la lista mostrados con el resultado.
- FR-05: Mientras esa consulta está en curso, el sistema debe mantener visibles los marcadores y la
  lista previos, y mostrar el botón en estado de carga (deshabilitado) hasta que la respuesta
  llegue.
- FR-06: El sistema debe informar al usuario cuando la nueva consulta no encuentre murales
  publicados en el área buscada, con el mismo criterio sin ampliación automática de radio que FR-06
  de FEAT-001d.

## Non-Functional Requirements

- NFR-01: La respuesta a "buscar en esta área" debe mostrarse en menos de 3 segundos para el 95% de
  las consultas (mismo umbral que NFR-01 de FEAT-001d, mismo endpoint).

## Acceptance Criteria

- AC-01: WHEN `GeolocationService` resuelve la ubicación del visitante después de que el mapa ya se
  renderizó con un centro provisorio, THE sistema SHALL recentrar el mapa en la ubicación resuelta.
  (FR-01)
- AC-02: WHEN el visitante envía coordenadas manuales desde el formulario de respaldo, THE sistema
  SHALL centrar el mapa en esas coordenadas, se haya o no renderizado antes con otro centro. (FR-01)
- AC-03: WHEN el mapa tiene una ubicación resuelta del visitante (geolocalización o manual), THE
  sistema SHALL mostrar un marcador distintivo en esa ubicación, visualmente distinguible de los
  marcadores de murales. (FR-02)
- AC-04: WHEN el usuario termina de arrastrar el mapa o de cambiar el zoom, THE sistema SHALL
  mostrar un botón para buscar murales en el área actual. (FR-03)
- AC-05: WHEN el usuario presiona ese botón, THE sistema SHALL consultar murales publicados usando
  el centro actual del mapa y el radio por defecto de 5 km, y reemplazar los marcadores y la lista
  mostrados con el resultado. (FR-04)
- AC-06: WHILE la consulta de "buscar en esta área" está en curso, THE sistema SHALL mantener
  visibles los marcadores y la lista previos y mostrar el botón deshabilitado en estado de carga.
  (FR-05)
- AC-07: IF la consulta de "buscar en esta área" no encuentra murales publicados en el radio
  buscado, THEN THE sistema SHALL informar que no se encontraron resultados, sin ampliar el radio
  automáticamente. (FR-06)
- AC-08: IF la consulta de "buscar en esta área" falla por un error del backend, THEN THE sistema
  SHALL mostrar un mensaje de error genérico sin perder los marcadores y la lista previos. (FR-05,
  AGENTS.md → Frontend → Error handling)

## Out of Scope

- **Refetch automático al mover el mapa.** Se decidió explícitamente un botón manual ("Buscar en
  esta área") en vez de disparar la consulta sola al soltar el mapa — evita consultas excesivas al
  backend por cada micro-movimiento y le da control al usuario sobre cuándo gasta la consulta.
- **Radio de búsqueda dinámico según el zoom.** La consulta de "buscar en esta área" sigue usando el
  radio por defecto de 5 km sin importar el nivel de zoom del mapa; ajustar el radio al área visible
  (bounding box real) queda fuera de este ticket — el endpoint actual es centro+radio, no bbox.
- **RF-021** Ampliar el radio de búsqueda automáticamente en pasos (5→10→20 km) cuando no hay
  resultados — ya excluido en FEAT-001d, sigue excluido acá.
- **Persistir o recordar la última área buscada** entre sesiones o al recargar la página.
- **Mostrar la precisión/exactitud de la geolocalización** (radio de error del GPS) sobre el mapa.

## Risks and Mitigations

### El fix de re-centrado puede generar un loop de renders si no se acota bien

**Riesgo:** si el mapa reacciona a *cualquier* cambio de `center` sin condición, y algo más adelante
llegara a mutar `center` en cada detección de cambios, se podría generar un `setView` repetido
innecesario (no un bug funcional, pero sí trabajo de más en cada ciclo).

**Mitigación:** a definir en PLAN — la reacción a `center` debe compararse contra el centro ya
aplicado antes de llamar `setView`, o limitarse a los casos en que `center` pasa de `null`/distinto
valor a uno nuevo.

### El botón "buscar en esta área" no debe confundirse con una recarga total de la página

**Riesgo:** un usuario que ya scrolleó la lista de resultados podría perder ese contexto si el botón
se comporta como un refresh completo.

**Mitigación:** FR-05/AC-06 ya fijan que los resultados previos se mantienen visibles hasta que
llega la respuesta nueva — no hay pantalla en blanco ni salto de scroll forzado.

## Dependencies

- **FEAT-001d**: base de `/discover` (mapa, lista, `DiscoveryService.getNearbyMurals`,
  `GeolocationService`) que este ticket corrige y extiende. No se modifica el contrato del endpoint
  backend (`lat`/`lng`/`radiusKm`), solo cómo y cuándo el frontend lo vuelve a llamar.
- Leaflet (vía Angular), ya cubierto por el stack declarado en `AGENTS.md` — este ticket usa sus
  eventos `moveend`/`zoomend` y un ícono de marcador adicional para el pin del visitante.
