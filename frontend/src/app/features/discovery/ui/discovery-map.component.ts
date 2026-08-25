import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  effect,
  input,
  output,
  viewChild,
} from '@angular/core';
import * as L from 'leaflet';
import { NearbyMuralItemResponse } from '../../../core/api-client/api-client.generated';

// `_getIconUrl` no está en las definiciones de tipos públicas de Leaflet — es el workaround
// documentado de la propia librería para bundlers ESM (esbuild/Angular 21), que sin esto resuelven
// mal la URL de los íconos por defecto y dejan los marcadores del mapa invisibles (FIX-002).
// eslint-disable-next-line @typescript-eslint/no-explicit-any
delete (L.Icon.Default.prototype as any)._getIconUrl;

L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'images/leaflet/marker-icon-2x.png',
  iconUrl: 'images/leaflet/marker-icon.png',
  shadowUrl: 'images/leaflet/marker-shadow.png',
});

/** Coordinates used to center the map — same shape as `GeolocationCoordinates`
 * (`shared/geolocation.service.ts`), redeclared here so this component does not have to import
 * that service just for a structural type. */
export interface MapCenter {
  latitude: number;
  longitude: number;
}

const DEFAULT_ZOOM = 14;
const FALLBACK_CENTER: MapCenter = { latitude: -34.905830, longitude: -56.191388 };
const TILE_LAYER_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const TILE_LAYER_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

/** Ícono distintivo del marcador de "tu ubicación" (Block 2) — `L.divIcon` con estilos inline en
 * vez de un archivo de imagen nuevo, decisión de PLAN para no repetir el incidente de íconos
 * perdidos por un `git checkout` accidental (FIX-002). Círculo celeste con borde blanco, distinto
 * del pin por defecto de Leaflet que usan los marcadores de murales. */
const VISITOR_ICON = L.divIcon({
  className: 'discovery-visitor-marker',
  html: '<div style="width: 16px; height: 16px; border-radius: 50%; background-color: #1890ff; border: 3px solid #ffffff; box-shadow: 0 0 4px rgba(0, 0, 0, 0.5);"></div>',
  iconSize: [16, 16],
  iconAnchor: [8, 8],
});

/**
 * Leaflet map for the `discovery` feature (spec Block 7). Imperative `leaflet` usage — decision
 * from PLAN: `leaflet` directly, not `ngx-leaflet` (no new dependency beyond Block 4's).
 * `ViewChild`/`ElementRef` (via the signal-based `viewChild.required`, consistent with the rest
 * of this Angular 21 signals-first codebase) drive the imperative Leaflet API; Angular's own
 * change detection never touches the map's DOM directly.
 */
@Component({
  selector: 'app-discovery-map',
  standalone: true,
  templateUrl: './discovery-map.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DiscoveryMapComponent implements AfterViewInit, OnDestroy {
  private readonly mapContainer = viewChild.required<ElementRef<HTMLDivElement>>('mapContainer');

  readonly items = input<NearbyMuralItemResponse[]>([]);
  /** Ubicación del visitante (Block 6) — cuando está disponible, centra el mapa ahí. Si no
   * (fallback manual pendiente, o el padre aún no la resolvió), `resolveCenter()` cae al primer
   * mural recibido y, en último caso, a un centro fijo — nunca lanza (decisión de este bloque,
   * documentada porque el spec la dejó abierta). */
  readonly center = input<MapCenter | null>(null);

  readonly muralSelected = output<NearbyMuralItemResponse>();
  /** Emite el centro vigente del mapa cuando el USUARIO lo mueve o hace zoom (arrastre/scroll) —
   * nunca cuando el propio componente lo recentra programáticamente (Block 1). `discovery-page`
   * escucha este output para mostrar el botón "Buscar en esta área" (Block 3). */
  readonly mapMoved = output<MapCenter>();

  private map: L.Map | null = null;
  private markers: L.Marker[] = [];
  /** Último centro efectivamente aplicado al mapa vía `applyCenter()` — guarda anti-loop del
   * `effect()` de abajo: sin esto, cada emisión de `center()` (incluso con el mismo valor)
   * dispararía un `setView` nuevo. */
  private lastAppliedCenter: MapCenter | null = null;
  /** Marcador distintivo de "tu ubicación" (Block 2) — se crea una única vez en `applyCenter()` y
   * a partir de ahí solo se reposiciona con `.setLatLng()`, nunca se destruye y recrea. */
  private visitorMarker: L.Marker | null = null;
  /** Guarda anti-loop de `mapMoved` (Block 3): `applyCenter()` la fija en `true` justo antes de
   * `map.setView(...)`, la única vía por la que este componente mueve el mapa. `setView()` dispara
   * `moveend` igual que un arrastre real del usuario — sin esta guarda, cada recentrado
   * programático (geolocalización, coordenadas manuales) emitiría `mapMoved` y dispararía el botón
   * "Buscar en esta área" sin que el usuario tocara el mapa. */
  private suppressNextMapMoved = false;

  constructor() {
    // Re-renderiza los marcadores cuando `items` cambia DESPUÉS del render inicial (p. ej.
    // `discovery-page` monta el mapa con `items: []` mientras la consulta está en vuelo, y lo
    // actualiza cuando el resultado llega). El render inicial no depende de este efecto —
    // `ngAfterViewInit` ya lee el valor vigente de `items()` al crear el mapa.
    effect(() => {
      const items = this.items();
      if (this.map) {
        this.renderMarkers(items);
      }
    });

    // Recentra el mapa cuando `center()` cambia DESPUÉS del render inicial (geolocalización
    // asíncrona o coordenadas manuales). Ignora `null` (el visitante puede perder la ubicación en
    // cualquier momento del ciclo de vida, `center()` ya lo permite) y valores iguales al último
    // aplicado — la guarda anti-loop que documenta el spec.
    effect(() => {
      const center = this.center();
      if (!center || !this.map) {
        return;
      }
      if (
        this.lastAppliedCenter &&
        this.lastAppliedCenter.latitude === center.latitude &&
        this.lastAppliedCenter.longitude === center.longitude
      ) {
        return;
      }
      this.applyCenter(center);
    });
  }

  ngAfterViewInit(): void {
    // Zoom fijo pasado como opción (no via `setView`, eso lo hace `applyCenter()` a continuación):
    // `applyCenter()` depende de `this.map.getZoom()` ya devolviendo un valor válido, y Leaflet solo
    // lo inicializa desde `options.zoom` — no requiere `options.center` para hacerlo.
    this.map = L.map(this.mapContainer().nativeElement, { zoom: DEFAULT_ZOOM });
    // Enganchado ANTES de la primera llamada a `applyCenter()` más abajo: su `setView()` inicial
    // también dispara `moveend`, y necesita que el listener ya esté activo para consumir la guarda
    // `suppressNextMapMoved` que `applyCenter()` fija — si no, quedaría en `true` para siempre y el
    // primer movimiento real del usuario se perdería en silencio.
    this.map.on('moveend', () => this.handleMapMoved());
    this.map.on('zoomend', () => this.handleMapMoved());
    // `tileLayer.addTo()` corre ANTES de tener un centro real (`applyCenter()` todavía no se llamó).
    // Es intencional y seguro: `Map.addLayer()` difiere el `onAdd()` del layer con `whenReady()`
    // hasta el evento `'load'` del mapa cuando este todavía no está cargado, así que `GridLayer`
    // nunca intenta leer `getCenter()` sobre un mapa sin centro/zoom completos (eso sí lanzaría).
    // El orden real que importa es `applyCenter()` antes del primer render visible, no antes de este
    // `addTo`. No reordenar sin volver a confirmar ese comportamiento de Leaflet.
    L.tileLayer(TILE_LAYER_URL, { attribution: TILE_LAYER_ATTRIBUTION }).addTo(this.map);
    this.applyCenter(this.resolveCenter());
    this.renderMarkers(this.items());
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = null;
  }

  /** Orden de resolución del centro inicial (a criterio de este bloque, spec lo dejó abierto):
   * 1) `center` input (ubicación real del visitante, la más precisa cuando está disponible);
   * 2) el primer mural de `items` (aproxima "centrar donde hay contenido" si aún no hay
   *    ubicación del visitante pero sí resultados);
   * 3) un fallback fijo (Montevideo) para que `ngAfterViewInit` nunca falle por falta de datos. */
  private resolveCenter(): MapCenter {
    const center = this.center();
    if (center) {
      return center;
    }
    const [first] = this.items();
    if (first?.latitude !== undefined && first?.longitude !== undefined) {
      return { latitude: first.latitude, longitude: first.longitude };
    }
    return FALLBACK_CENTER;
  }

  private toLatLng(point: MapCenter): L.LatLngExpression {
    return [point.latitude, point.longitude];
  }

  /** Única vía por la que el componente mueve el mapa programáticamente (recentrado inicial y
   * reactivo ante cambios de `center()`). Requiere que `this.map` ya exista con un zoom válido —
   * `ngAfterViewInit()` lo garantiza al construir el mapa con `{ zoom: DEFAULT_ZOOM }`. */
  private applyCenter(center: MapCenter): void {
    if (!this.map) {
      return;
    }
    this.suppressNextMapMoved = true;
    this.map.setView(this.toLatLng(center), this.map.getZoom());
    this.lastAppliedCenter = center;

    const latLng = this.toLatLng(center);
    if (this.visitorMarker) {
      this.visitorMarker.setLatLng(latLng);
    } else {
      // Leaflet agrega la clase base `leaflet-marker-icon` a CUALQUIER icono de marcador
      // (incluidos los `L.divIcon`) en `Icon._setIconStyles`, sin importar `className` — así los
      // marcadores de murales y el de visitante comparten esa clase por diseño de la librería. NO
      // se quita: `leaflet-marker-icon` trae `position: absolute` desde los "required styles" de
      // `leaflet.css`, y Leaflet posiciona el ícono únicamente vía `transform` inline — nunca fija
      // `position` inline. Sin esa clase el marcador queda mal posicionado en un navegador real
      // (jsdom no lo detecta porque no calcula layout CSS). El marcador de visitante ya es
      // distinguible por su propia clase (`discovery-visitor-marker`, que Leaflet concatena, no
      // reemplaza) — los selectores que necesiten excluirlo usan `:not(.discovery-visitor-marker)`.
      this.visitorMarker = L.marker(latLng, { icon: VISITOR_ICON }).addTo(this.map);
    }
  }

  /** Handler único de `moveend`/`zoomend` (Block 3). Primero chequea la guarda anti-loop: si el
   * movimiento lo disparó el propio componente (`applyCenter()`), la consume y no emite nada; si
   * es un movimiento real del usuario (arrastre o zoom), emite `mapMoved` con el centro vigente. */
  private handleMapMoved(): void {
    if (this.suppressNextMapMoved) {
      this.suppressNextMapMoved = false;
      return;
    }
    if (!this.map) {
      return;
    }
    const center = this.map.getCenter();
    this.mapMoved.emit({ latitude: center.lat, longitude: center.lng });
  }

  private renderMarkers(items: NearbyMuralItemResponse[]): void {
    const map = this.map;
    if (!map) {
      return;
    }
    for (const marker of this.markers) {
      marker.remove();
    }
    this.markers = items
      .filter((item) => item.latitude !== undefined && item.longitude !== undefined)
      .map((item) => {
        const marker = L.marker([item.latitude as number, item.longitude as number]).addTo(map);
        marker.on('click', () => this.muralSelected.emit(item));
        return marker;
      });
  }
}
