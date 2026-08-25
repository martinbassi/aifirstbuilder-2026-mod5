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

  private map: L.Map | null = null;
  private markers: L.Marker[] = [];
  /** Último centro efectivamente aplicado al mapa vía `applyCenter()` — guarda anti-loop del
   * `effect()` de abajo: sin esto, cada emisión de `center()` (incluso con el mismo valor)
   * dispararía un `setView` nuevo. */
  private lastAppliedCenter: MapCenter | null = null;

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
    this.map.setView(this.toLatLng(center), this.map.getZoom());
    this.lastAppliedCenter = center;
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
