# Spec FEAT-005: Geolocalización funcional y refetch de murales según el área del mapa en /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-005 |
| PRD | docs/daw/prd/prd-FEAT-005.md |
| Tier | FEATURE |
| Date | 2026-08-25 |
| Spec loops | 0 |

## Summary

`discovery-map.component.ts` hoy solo resuelve el centro del mapa una vez, en `ngAfterViewInit()`,
y nunca vuelve a reaccionar. Este spec lo hace reactivo: un `effect()` recentra el mapa cada vez que
`center()` cambia (geolocalización asíncrona o coordenadas manuales) y dibuja un marcador `L.divIcon`
distintivo en esa ubicación. El mapa gana un output `mapMoved` que emite el centro actual cuando el
usuario arrastra o hace zoom — con una guarda para no emitirlo cuando el propio componente recentra
el mapa programáticamente. `discovery-page.component.ts`, que ya orquesta `items`/`loading`/
`errorMessage` (decisión de FEAT-001d), escucha ese output para mostrar un botón "Buscar en esta
área" y reutiliza el `fetchNearbyMurals()` existente para volver a consultar — por lo que mantener
los resultados previos visibles durante la carga sale gratis, ya es su comportamiento actual. Sin
cambios de backend ni de contrato de API.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 1 |
| FR-02 | Block 2 |
| FR-03 | Block 3 |
| FR-04 | Block 4 |
| FR-05 | Block 4 |
| FR-06 | Block 4 |
| NFR-01 | Strategy: mismo endpoint que FEAT-001d ya cumple NFR-01 con su índice espacial; este ticket no agrega carga adicional por consulta (sigue siendo centro+radio, sin bounding box), por lo que hereda la misma garantía sin trabajo adicional. |

## Dependencies between blocks

Block 1 → Block 3 (Block 3 reutiliza y extiende el método `applyCenter()`/la guarda anti-loop que
introduce Block 1). Block 3 → Block 4 (necesita el output `mapMoved` y el botón ya wireados). Block 2
es independiente de 3 y 4, pero se apoya en el mismo `effect()` de Block 1. Orden de ejecución:
1 → 2 → 3 → 4.

## Block 1 — Recentrado reactivo del mapa

**Files**
- `frontend/src/app/features/discovery/ui/discovery-map.component.ts` (modified)
- `frontend/src/app/features/discovery/ui/discovery-map.component.spec.ts` (modified)

**Logic**

Agregar un campo privado `lastAppliedCenter: MapCenter | null = null` y un método privado
`applyCenter(center: MapCenter): void` que llama `this.map.setView(this.toLatLng(center),
this.map.getZoom())` y actualiza `lastAppliedCenter`. `ngAfterViewInit()` pasa a usar
`applyCenter(this.resolveCenter())` en vez de construir el mapa directamente con `setView(...)`
inline, y guarda el resultado en `lastAppliedCenter`.

Ampliar el `effect()` del constructor (hoy solo reacciona a `items()`) para que también reaccione a
`center()`: cuando `center()` no es `null` y difiere de `lastAppliedCenter` (comparar `latitude`/
`longitude` con igualdad estricta — no hay redondeo de por medio en ningún punto del flujo), llama
`applyCenter(center)`. Si `center()` es `null` o es igual a `lastAppliedCenter`, no hace nada — esta
comparación es la guarda anti-loop que el PRD pide en su sección de Riesgos.

**Error handling**
- `center()` puede llegar `null` en cualquier momento del ciclo de vida del componente (su tipo ya lo
  permite) — el `effect()` lo ignora sin lanzar, igual que hace hoy `resolveCenter()`.

**Required tests**
- [ ] `discovery-map.component.spec.ts`: dado el mapa ya renderizado con el centro de fallback,
  cuando el input `center` cambia a un valor nuevo, `map.getCenter()` refleja el nuevo centro —
  valida AC-01.
- [ ] `discovery-map.component.spec.ts`: el mismo comportamiento se dispara sin importar si el
  cambio de `center()` viene de geolocalización o de un segundo cambio posterior (simula el caso de
  coordenadas manuales) — valida AC-02.
- [ ] `discovery-map.component.spec.ts`: si `center()` se vuelve a fijar con el mismo valor que ya
  tiene aplicado, `map.setView` no se vuelve a invocar (test de regresión anti-loop).
- [ ] `discovery-map.component.spec.ts`: si `center()` cambia a `null` después de tener un valor, el
  `effect()` no llama `applyCenter()` ni lanza — valida el caso documentado en "Error handling".

**Completion criterion**
Los tres tests pasan: el mapa se recentra cada vez que `center()` cambia a un valor distinto del ya
aplicado, después del render inicial, y no repite `setView` cuando `center()` no cambió.

## Block 2 — Pin distintivo de "tu ubicación"

**Files**
- `frontend/src/app/features/discovery/ui/discovery-map.component.ts` (modified)
- `frontend/src/app/features/discovery/ui/discovery-map.component.spec.ts` (modified)

**Logic**

Definir un `L.divIcon` a nivel de módulo (junto a `FALLBACK_CENTER`/`DEFAULT_ZOOM`), con una
`className` propia (`discovery-visitor-marker`) y un `html` con estilos inline (círculo con color y
borde distintos de los marcadores de murales) — sin archivo de imagen nuevo, consistente con la
decisión del usuario en PLAN de no repetir el incidente de íconos perdidos (ver FIX-002).

Agregar un campo privado `visitorMarker: L.Marker | null = null`. Dentro de `applyCenter()` (Block
1), después de `setView`, crear el marcador si no existe (`L.marker(latLng, { icon:
VISITOR_ICON }).addTo(this.map)`) o reposicionarlo con `.setLatLng(latLng)` si ya existe — nunca se
destruye y recrea en cada recentrado, solo se mueve.

**Error handling**
N/A — no hay entrada externa que pueda fallar. `applyCenter()` (Block 1) solo se invoca con un
`center` ya no-nulo (la guarda del `effect()` lo garantiza), y `L.marker`/`.setLatLng()` no lanzan
con coordenadas numéricas válidas.

**Required tests**
- [ ] `discovery-map.component.spec.ts`: cuando `center()` tiene un valor, aparece en el DOM un
  elemento con la clase `discovery-visitor-marker`, distinguible de `.leaflet-marker-icon` (los
  marcadores de murales) — valida AC-03.
- [ ] `discovery-map.component.spec.ts`: si `center()` cambia dos veces, sigue existiendo un único
  marcador de visitante (no se acumulan) y su posición refleja el último centro.

**Completion criterion**
Los tests pasan: existe un único marcador distintivo del visitante, visualmente distinto de los
marcadores de murales, que se reposiciona (no se duplica) en cada recentrado.

## Block 3 — Output `mapMoved` y botón "Buscar en esta área"

**Files**
- `frontend/src/app/features/discovery/ui/discovery-map.component.ts` (modified)
- `frontend/src/app/features/discovery/ui/discovery-map.component.spec.ts` (modified)
- `frontend/src/app/features/discovery/ui/discovery-page.component.ts` (modified)
- `frontend/src/app/features/discovery/ui/discovery-page.component.html` (modified)
- `frontend/src/app/features/discovery/ui/discovery-page.component.spec.ts` (modified)

**Logic**

En `DiscoveryMapComponent`: nuevo output `readonly mapMoved = output<MapCenter>();` y un campo
privado `suppressNextMapMoved = false`. `applyCenter()` (Block 1) pasa a fijar
`this.suppressNextMapMoved = true` inmediatamente antes de llamar `this.map.setView(...)` — es la
única vía por la que el componente mueve el mapa programáticamente. En `ngAfterViewInit()`, después
de crear el mapa, engancha `this.map.on('moveend', () => this.handleMapMoved())` y
`.on('zoomend', () => this.handleMapMoved())`. `handleMapMoved()` primero chequea
`suppressNextMapMoved`: si es `true`, lo pone en `false` y no emite nada (movimiento propio,
programático); si es `false`, emite `mapMoved` con `this.map.getCenter()` convertido a `MapCenter`
(movimiento real del usuario — arrastre o zoom).

Por qué la guarda es necesaria: `setView()` dispara `moveend` igual que un arrastre del usuario. Sin
la guarda, cada vez que la geolocalización recentra el mapa (Block 1) se emitiría `mapMoved` y
aparecería el botón "Buscar en esta área" solo, sin que el usuario hubiera tocado el mapa — decisión
ya descartada explícitamente en PLAN.

En `DiscoveryPageComponent`: nuevos signals `showSearchAreaButton = signal(false)` y
`lastMapCenter = signal<MapCenter | null>(null)`. Nuevo método `onMapMoved(center: MapCenter): void`
que fija ambos: `lastMapCenter.set(center)` y `showSearchAreaButton.set(true)`. `lastMapCenter` se
tipa `signal<MapCenter | null>`, no `GeolocationCoordinates` (el tipo que ya usa `center` en este
mismo archivo) — son estructuralmente idénticos (`{ latitude, longitude }`) a propósito, mismo
criterio que ya documenta `MapCenter` en `discovery-map.component.ts`: representan conceptos
distintos (ubicación del visitante vs. último centro que el usuario dejó el mapa), y unificarlos
acoplaría "de dónde vino el centro" con "cuál es el centro actual", que son cosas separadas. En
`discovery-page.component.html`, agregar `(mapMoved)="onMapMoved($event)"` al binding de
`<app-discovery-map>`, y un botón nuevo (`nz-button`, `data-testid="search-area-button"`) renderizado
con `@if (showSearchAreaButton())`, con `[nzLoading]="loading()"` (mismo componente `nz-button` que
usa el botón de búsqueda manual ya existente en la misma plantilla, pero **no** la misma condición de
`[disabled]`: el manual usa `[disabled]="!canSearchManually()"` porque valida un rango de lat/lng
tipeado a mano; este botón no tiene input de usuario que validar, así que su única condición es
`[disabled]="loading()"` — evitar doble-click mientras la consulta está en curso). El `(click)` de
este botón se conecta en Block 4.

**Error handling**
N/A — los listeners `moveend`/`zoomend` solo se enganchan después de crear `this.map` en
`ngAfterViewInit()`, así que `handleMapMoved()` nunca corre con `this.map` sin inicializar; no hay
entrada externa que pueda fallar en este bloque.

**Required tests**
- [ ] `discovery-map.component.spec.ts`: simular un `moveend` iniciado por el usuario (ej. llamar
  `component.map.panTo(...)` seguido de disparar el evento, o `map.fire('moveend')` directamente
  tras mover la vista sin pasar por `applyCenter()`) y verificar que `mapMoved` emite con el nuevo
  centro.
- [ ] `discovery-map.component.spec.ts`: cuando el recentrado lo dispara el propio componente (input
  `center` cambia, Block 1), `mapMoved` NO emite — valida la guarda anti-loop.
- [ ] `discovery-map.component.spec.ts`: un `zoomend` iniciado por el usuario también emite
  `mapMoved` con el centro vigente.
- [ ] `discovery-page.component.spec.ts`: el botón "Buscar en esta área" no está presente al cargar
  la página; aparece después de que `app-discovery-map` emite `mapMoved` — valida AC-04.

**Completion criterion**
Los cuatro tests pasan: `mapMoved` distingue movimiento del usuario de recentrado programático, y el
botón en `discovery-page` aparece únicamente ante el primero.

## Block 4 — Wiring del botón: refetch, estado de carga y errores

**Files**
- `frontend/src/app/features/discovery/ui/discovery-page.component.ts` (modified)
- `frontend/src/app/features/discovery/ui/discovery-page.component.html` (modified)
- `frontend/src/app/features/discovery/ui/discovery-page.component.spec.ts` (modified)

**Logic**

Nuevo método `searchThisArea(): void` en `DiscoveryPageComponent`: lee `lastMapCenter()`, y si no es
`null`, llama `this.fetchNearbyMurals(center.latitude, center.longitude)` (el método ya existente,
sin `radiusKm` — usa el default del backend, 5 km, tal como se acordó en PLAN). No toca
`showSearchAreaButton` ni `items` directamente.

Modificar `fetchNearbyMurals()` (ya existente): en sus callbacks `next` y `error`, agregar
`this.showSearchAreaButton.set(false)` — así el botón se mantiene visible y deshabilitado
(`[nzLoading]="loading()"` de Block 3) mientras la consulta está en curso, y desaparece recién cuando
la respuesta (éxito o error) llega, sin importar si la consulta la disparó la geolocalización inicial,
la búsqueda manual o "Buscar en esta área" — en los dos primeros casos el botón ya estaba oculto, así
que la llamada es no-op ahí.

En `discovery-page.component.html`, conectar `(click)="searchThisArea()"` en el botón agregado en
Block 3.

El caso "sin resultados" (FR-06/AC-07) no requiere UI nueva: `fetchNearbyMurals` ya hace
`items.set(items)` con lo que devuelva el backend (incluyendo `[]`), y
`discovery-list.component.html` ya renderiza su `empty-message` existente cuando `items()` está
vacío (comportamiento heredado de FEAT-001d, AC-06 de ese ticket) — Block 4 solo verifica que el
flujo de "Buscar en esta área" llega ahí igual que cualquier otra búsqueda.

**Error handling**
- Si `discoveryService.getNearbyMurals(...)` falla, el `error` callback ya existente de
  `fetchNearbyMurals` fija `errorMessage` con el mensaje del `ApiError` — sin cambios, se hereda tal
  cual. `items()` no se toca en ese callback, así que los marcadores/lista previos siguen visibles —
  valida AC-08.

**Required tests**
- [ ] `discovery-page.component.spec.ts`: al hacer click en "Buscar en esta área",
  `discoveryService.getNearbyMurals` se llama con el `latitude`/`longitude` de `lastMapCenter()` —
  valida AC-05.
- [ ] `discovery-page.component.spec.ts`: mientras la consulta está en vuelo (observable no resuelto
  todavía), `items()` conserva los valores previos y `showSearchAreaButton()` sigue en `true` —
  valida AC-06.
- [ ] `discovery-page.component.spec.ts`: cuando la consulta resuelve con resultados nuevos,
  `items()` se reemplaza por esos resultados y `showSearchAreaButton()` pasa a `false` — valida
  AC-05.
- [ ] `discovery-page.component.spec.ts`: cuando la consulta resuelve con `[]`, `items()` queda
  vacío (sin ampliar el radio) — valida AC-07.
- [ ] `discovery-page.component.spec.ts`: cuando la consulta falla, `errorMessage()` se fija,
  `items()` conserva los valores previos y `showSearchAreaButton()` pasa a `false` — valida AC-08.

**Completion criterion**
Los cinco tests pasan: "Buscar en esta área" consulta el centro actual del mapa, mantiene los
resultados previos visibles mientras está en curso, y termina en el estado correcto tanto para éxito
(con o sin resultados) como para error.

## Final verification

- Los 4 bloques implementados y sus tests en verde.
- `discovery-map.component.spec.ts` y `discovery-page.component.spec.ts` cubren las 8 AC del PRD
  (AC-01 a AC-08).
- Ningún archivo fuera de `frontend/src/app/features/discovery/ui/` se modifica; no hay cambios de
  backend ni de `api-client.generated.ts`.
- `npx tsc --build --noEmit tsconfig.json` y el lint del frontend pasan sin nuevos hallazgos.
