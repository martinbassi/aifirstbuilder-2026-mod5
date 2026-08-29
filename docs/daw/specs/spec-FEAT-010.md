# Spec FEAT-010: Marcador del centro de búsqueda en el mapa de /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-010 |
| PRD | docs/daw/prd/prd-FEAT-005.md (PRD loop 1) |
| Tier | FEATURE |
| Date | 2026-08-29 |
| Spec loops | 0 |

## Summary

Agrega un signal `lastSearchCenter` en `discovery-page.component.ts`, que guarda las coordenadas
exactas usadas en la última consulta de murales cercanos exitosa (carga inicial o "buscar en esta
área"), actualizado dentro del único callback `next` de `fetchNearbyMurals()` — nunca en los 3 call
sites que lo invocan, para no duplicar la asignación. Una función pura nueva
(`shared/geo-distance.util.ts`, Haversine en metros — el backend ya no tiene un equivalente:
`GeoDistanceCalculator` fue eliminado hoy por FEAT-009 al migrar a `geography`) decide, con un
umbral de 50 metros, si el marcador de centro de búsqueda se fusiona con el de "tu ubicación"
existente o se muestra por separado. El marcador nuevo usa una **forma distinta** (no solo color)
del círculo de `VISITOR_ICON`, para no depender únicamente del color como señal distintiva
(accesibilidad, hallazgo del arch-auditor en PLAN). A diferencia de `visitorMarker` (permanente una
vez creado), `searchCenterMarker` tiene visibilidad condicional — se agrega/quita del mapa según el
umbral, no solo se reposiciona.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-07 (marcador refleja el centro de la última consulta) | Block 1 (signal), Block 2 (render) |
| FR-08 (fusión si <50m) | Block 2 |
| FR-09 (ambos si ≥50m) | Block 2 |
| AC-09 (marcador se actualiza tras consulta exitosa) | Block 1, Block 2 |
| AC-10 (un solo marcador si <50m) | Block 2 |
| AC-11 (ambos marcadores si ≥50m) | Block 2 |
| AC-12 (marcador no se mueve si la consulta falla) | Block 1 |

## Dependencies between blocks

Block 2 depende de Block 1 (usa el signal `lastSearchCenter` y la función `haversineDistanceMeters`
ya existentes). Orden de ejecución: Block 1 → Block 2.

## Block 1 — Signal de centro de búsqueda + utilidad de distancia

**Files**
- `frontend/src/app/features/discovery/ui/discovery-page.component.ts` (modified) — agrega
  `lastSearchCenter = signal<MapCenter | null>(null)` (mismo tipo `MapCenter` que ya usa
  `lastMapCenter`, importado desde `discovery-map.component.ts` como ya se hace hoy). Dentro del
  callback `next` de `fetchNearbyMurals(latitude, longitude)` (el único choke point de éxito,
  compartido por `requestGeolocation()`, `searchManually()` y `searchThisArea()`), agrega
  `this.lastSearchCenter.set({ latitude, longitude })` junto a las demás asignaciones que ya ocurren
  ahí (`items.set(...)`, etc.). **No se toca el callback `error`** — `lastSearchCenter` conserva su
  último valor válido si la consulta falla (AC-12), replicando la misma asimetría que ya tiene
  `showSearchAreaButton` en ese mismo bloque.
- `frontend/src/app/shared/geo-distance.util.ts` (new) — primera utilidad de este tipo en el
  frontend (sin precedente de archivo `*.util.ts` en el proyecto; se elige `shared/` porque el
  cálculo es genérico, no específico de `discovery`, siguiendo la letra de AGENTS.md sobre qué va en
  `shared/`). Exporta:
  ```ts
  export interface Coordinates {
    latitude: number;
    longitude: number;
  }

  export function haversineDistanceMeters(a: Coordinates, b: Coordinates): number { ... }
  ```
  `Coordinates` se declara localmente acá (no importa `MapCenter` de `discovery`) para no invertir
  la dependencia `shared/` → `features/` — mismo criterio ya usado en el proyecto para `MapCenter`
  (redeclarada en vez de importar `GeolocationCoordinates`). `MapCenter`/`GeolocationCoordinates` son
  estructuralmente compatibles con `Coordinates` (mismos dos campos), así que se pasan sin
  conversión explícita.
- `frontend/src/app/shared/geo-distance.util.spec.ts` (new).

**Logic**

Fórmula de Haversine estándar (radio de la Tierra 6371000 m, para devolver metros directamente —
sin necesidad de la conversión km→m que sí hacía el backend, porque el consumidor de este util
trabaja en metros desde el principio).

**Input validation**

- N/A — `haversineDistanceMeters` es una función pura sin I/O; recibe siempre coordenadas ya
  resueltas y válidas (geolocalización, coordenadas manuales validadas por el formulario, o el
  centro que el propio mapa reportó al moverse).

**Error handling**

- N/A para la función pura. En `discovery-page.component.ts`, el `error` callback de
  `fetchNearbyMurals` no cambia — sigue mostrando el mensaje genérico existente (AC-08 de
  `prd-FEAT-005.md`) sin tocar `lastSearchCenter`.

**Required tests**

- [ ] `haversineDistanceMeters` devuelve `0` para dos puntos idénticos.
- [ ] `haversineDistanceMeters` devuelve ~1000m (tolerancia <10m) para dos puntos conocidos a ~1km
      de distancia real (mismo par de coordenadas que ya se usó en el test equivalente del backend,
      si sigue disponible en el historial, o uno nuevo con una distancia verificada
      independientemente).
- [ ] `haversineDistanceMeters(a, b) === haversineDistanceMeters(b, a)` (simetría).
- [ ] En `discovery-page.component`, tras una consulta exitosa (mock de `discoveryService`),
      `lastSearchCenter()` queda seteado con las coordenadas exactas usadas en esa consulta — valida
      AC-09 (la mitad del signal, la otra mitad la valida Block 2 con el render).
- [ ] En `discovery-page.component`, si la consulta falla, `lastSearchCenter()` conserva el valor
      que tenía antes de la consulta fallida (o `null` si nunca hubo una exitosa) — valida AC-12.

**Completion criterion**

Los 5 tests de este bloque pasan; `npx tsc --build --noEmit tsconfig.json` sin errores.

## Block 2 — Marcador en discovery-map.component

**Files**
- `frontend/src/app/features/discovery/ui/discovery-map.component.ts` (modified) —
  - Importa `haversineDistanceMeters`, `Coordinates` desde `../../../shared/geo-distance.util`.
  - Agrega `searchCenter = input<MapCenter | null>(null);`.
  - Agrega la constante de módulo `const SEARCH_CENTER_PROXIMITY_THRESHOLD_METERS = 50;` (mismo
    patrón que `DEFAULT_ZOOM`/`FALLBACK_CENTER` ya existentes — nunca un número mágico inline).
  - Agrega la constante de módulo `SEARCH_CENTER_ICON` (`L.divIcon`, estilos inline, mismo patrón
    que `VISITOR_ICON` para evitar el incidente FIX-002 de íconos como archivo perdido): **forma de
    pin/gota** (ej. `border-radius: 50% 50% 50% 0; transform: rotate(-45deg);` sobre un cuadrado de
    16×16px), color coral (`#fe6944`, primario de la paleta adoptada en FEAT-002) con borde blanco,
    clase CSS `discovery-search-center-marker` — silueta deliberadamente distinta del círculo celeste
    de `VISITOR_ICON`, no solo el color, para que la distinción no dependa únicamente de percibir
    color (accesibilidad).
  - Agrega el campo `private searchCenterMarker: L.Marker | null = null;`.
  - Agrega el método `applySearchCenterMarker(): void`, invocado desde el mismo punto del ciclo de
    vida que ya invoca `applyCenter()` (reacciona a cambios de `center()` y `searchCenter()`).
    **Ciclo de vida explícito (máquina de estados — NO es el mismo patrón que `visitorMarker`, que
    es permanente una vez creado):**
    1. Si `searchCenter()` es `null` → si `searchCenterMarker` existe, `.remove()` del mapa y
       asignar `null`. Retornar.
    2. Calcular `farEnough = center() === null || haversineDistanceMeters(center()!, searchCenter()!)
       >= SEARCH_CENTER_PROXIMITY_THRESHOLD_METERS`.
    3. Si `farEnough` (mostrar el marcador):
       - Si `searchCenterMarker` es `null` → crear `L.marker(latLng, { icon: SEARCH_CENTER_ICON })`,
         `.addTo(this.map)`, guardar la referencia.
       - Si ya existe → `.setLatLng(latLng)` (reposicionar, no recrear).
    4. Si NO `farEnough` (ocultar — fusión con el marcador de visitante): si `searchCenterMarker`
       existe → `.remove()` del mapa y asignar `null`. Si no existe, no-op.
- `frontend/src/app/features/discovery/ui/discovery-page.component.html` (modified) — agrega
  `[searchCenter]="lastSearchCenter()"` al `<app-discovery-map>`.
- `frontend/src/app/features/discovery/ui/discovery-map.component.spec.ts` (modified) — actualiza
  las 11 ocurrencias de `.leaflet-marker-icon:not(.discovery-visitor-marker)` a
  `.leaflet-marker-icon:not(.discovery-visitor-marker):not(.discovery-search-center-marker)`, para
  que sigan contando solo marcadores de mural aunque el fixture setee `searchCenter` en el futuro.

**Logic**

`applySearchCenterMarker()` se ejecuta junto con la lógica de recentrado existente, cada vez que
`center()` o `searchCenter()` cambian. Al ser `searchCenter()` inicialmente `null` (antes de
cualquier consulta), no aparece ningún marcador de centro de búsqueda hasta la primera consulta
exitosa — el comportamiento hoy existente (un solo marcador, "tu ubicación") no cambia hasta ese
momento.

**Error handling**

- Sin casos nuevos: si `this.map` todavía no existe (guard ya existente en `applyCenter()` para el
  mismo caso), `applySearchCenterMarker()` debe hacer el mismo no-op seguro.

**Required tests**

- [ ] Con `center` y `searchCenter` a ≥50m de distancia real entre sí, el mapa muestra AMBOS
      marcadores (`.discovery-visitor-marker` y `.discovery-search-center-marker` presentes) —
      valida AC-11.
- [ ] Con `center` y `searchCenter` a <50m de distancia real entre sí, el mapa muestra SOLO el
      marcador de visitante (`.discovery-search-center-marker` ausente) — valida AC-10.
- [ ] Al pasar `searchCenter` de un valor lejano (≥50m) a uno cercano (<50m) en un re-render, el
      marcador de centro de búsqueda que ya estaba en el mapa se remueve (no queda huérfano) —
      valida AC-10/AC-09 (actualización, no solo estado inicial).
- [ ] Sin `searchCenter` (`null`, estado inicial antes de cualquier consulta), no aparece ningún
      marcador de centro de búsqueda, solo el de visitante si `center` está seteado — comportamiento
      preexistente sin regresión.
- [ ] Las 11 aserciones que cuentan marcadores de mural con el selector actualizado siguen pasando
      sin cambios de comportamiento (regresión).

**Completion criterion**

Los tests de este bloque pasan (5 nuevos + 11 aserciones existentes adaptadas sin romperse);
`npx tsc --build --noEmit tsconfig.json` y `npx ng lint` sin errores.

## Final verification

- Los 10 tests nuevos entre los 2 bloques pasan (AC-09 a AC-12 cubiertos).
- `npx tsc --build --noEmit tsconfig.json` sin errores.
- Lint sin nuevos warnings/errores en los 4 archivos tocados/creados.
- Verificación manual: cargar `/discover`, ver el marcador de "tu ubicación"; mover el mapa y usar
  "Buscar en esta área" hacia un punto lejano → aparece un segundo marcador (forma distinta,
  coral) en el nuevo centro; volver a buscar cerca de la ubicación original → el segundo marcador
  desaparece, queda solo el de "tu ubicación".
