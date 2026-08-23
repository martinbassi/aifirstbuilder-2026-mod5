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

/** Coordinates used to center the map — same shape as `GeolocationCoordinates`
 * (`shared/geolocation.service.ts`), redeclared here so this component does not have to import
 * that service just for a structural type. */
export interface MapCenter {
  latitude: number;
  longitude: number;
}

const DEFAULT_ZOOM = 14;
const FALLBACK_CENTER: MapCenter = { latitude: 0, longitude: 0 };
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
  }

  ngAfterViewInit(): void {
    this.map = L.map(this.mapContainer().nativeElement).setView(
      this.toLatLng(this.resolveCenter()),
      DEFAULT_ZOOM,
    );
    L.tileLayer(TILE_LAYER_URL, { attribution: TILE_LAYER_ATTRIBUTION }).addTo(this.map);
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
   * 3) un fallback fijo (0,0) para que `ngAfterViewInit` nunca falle por falta de datos. */
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
