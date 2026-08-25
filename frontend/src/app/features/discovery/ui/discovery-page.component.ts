import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NearbyMuralItemResponse } from '../../../core/api-client/api-client.generated';
import { ApiError } from '../../../core/http/api-error';
import { GeolocationCoordinates, GeolocationService } from '../../../shared/geolocation.service';
import { DiscoveryService } from '../data/discovery.service';
import { DiscoveryMapComponent, MapCenter } from './discovery-map.component';
import { DiscoveryListComponent } from './discovery-list.component';

const MIN_LATITUDE = -90;
const MAX_LATITUDE = 90;
const MIN_LONGITUDE = -180;
const MAX_LONGITUDE = 180;

/**
 * Public entry point of the `discovery` feature (spec Block 7, FR-03/FR-04/FR-06). Reachable
 * without a session (Block 8 wires the route). On init, asks `GeolocationService` (Block 6) for
 * the visitor's position and, on success, queries `discovery.service.ts` with it. If geolocation
 * rejects (any of its 3 typed `kind`s), the query is NOT fired automatically — a manual lat/lng
 * fallback is shown instead, same spirit as `create-mural-form`'s fallback (FR-06). If the query
 * itself fails, a generic error is shown — never swallowed (AGENTS.md, Frontend → Error handling).
 * Composes `DiscoveryMapComponent` + `DiscoveryListComponent`, passing them the loaded `items`.
 */
@Component({
  selector: 'app-discovery-page',
  standalone: true,
  imports: [
    NzAlertModule,
    NzButtonModule,
    NzFormModule,
    NzInputModule,
    NzIconModule,
    DiscoveryMapComponent,
    DiscoveryListComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './discovery-page.component.html',
  styleUrls: ['./discovery-page.component.css'],
})
export class DiscoveryPageComponent implements OnInit {
  private readonly geolocationService = inject(GeolocationService);
  private readonly discoveryService = inject(DiscoveryService);

  readonly items = signal<NearbyMuralItemResponse[]>([]);
  readonly center = signal<GeolocationCoordinates | null>(null);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  /** True once `GeolocationService` is known to have rejected — reveals the manual lat/lng input
   * without blocking the rest of the page (FR-06), same pattern as `create-mural-form`. */
  readonly manualLocationRequired = signal(false);
  readonly manualLatitude = signal<number | null>(null);
  readonly manualLongitude = signal<number | null>(null);

  /** true una vez que `app-discovery-map` emite `mapMoved` (Block 3) — revela el botón "Buscar en
   * esta área". Se tipa `MapCenter`, no `GeolocationCoordinates` (el tipo de `center` en este mismo
   * archivo): representan conceptos distintos — ubicación del visitante vs. último centro que el
   * usuario dejó el mapa — y unificarlos acoplaría "de dónde vino el centro" con "cuál es el centro
   * actual". */
  readonly showSearchAreaButton = signal(false);
  readonly lastMapCenter = signal<MapCenter | null>(null);

  readonly canSearchManually = computed(() => {
    const latitude = this.manualLatitude();
    const longitude = this.manualLongitude();
    return (
      latitude !== null &&
      latitude >= MIN_LATITUDE &&
      latitude <= MAX_LATITUDE &&
      longitude !== null &&
      longitude >= MIN_LONGITUDE &&
      longitude <= MAX_LONGITUDE &&
      !this.loading()
    );
  });

  ngOnInit(): void {
    this.requestGeolocation();
  }

  onLatitudeChange(event: Event): void {
    this.manualLatitude.set(this.parseNumberInput(event));
  }

  onLongitudeChange(event: Event): void {
    this.manualLongitude.set(this.parseNumberInput(event));
  }

  /** Triggered by the manual fallback form once the visitor typed coordinates themselves. */
  searchManually(): void {
    if (!this.canSearchManually()) {
      return;
    }
    const latitude = this.manualLatitude() as number;
    const longitude = this.manualLongitude() as number;
    this.center.set({ latitude, longitude });
    this.fetchNearbyMurals(latitude, longitude);
  }

  /** Triggered by `app-discovery-map`'s `mapMoved` output (Block 3) — a real user drag/zoom, never
   * a programmatic recenter (the map's own anti-loop guard filters that out). Just tracks the new
   * center and reveals the "Buscar en esta área" button; the actual refetch is wired in Block 4. */
  onMapMoved(center: MapCenter): void {
    this.lastMapCenter.set(center);
    this.showSearchAreaButton.set(true);
  }

  /** Triggered by the "Buscar en esta área" button (Block 3 wired it, Block 4 connects `(click)`).
   * Refetches with the last center the map reported via `mapMoved` — no `radiusKm`, uses the
   * backend's default (5 km, agreed in PLAN). Does not touch `showSearchAreaButton` directly;
   * `fetchNearbyMurals()` owns that once the request settles. No-op if `lastMapCenter()` is `null`
   * (button is only rendered after `onMapMoved()` sets it, but this guards the method itself). */
  searchThisArea(): void {
    const center = this.lastMapCenter();
    if (!center) {
      return;
    }
    this.fetchNearbyMurals(center.latitude, center.longitude);
  }

  /** Currently just tracked for a future "highlight on the map"/"scroll to" behavior — the
   * spec's completion criterion for this block only requires the selection to surface a detail,
   * which `discovery-list` already renders inline on its own selection state; wiring both
   * children's `muralSelected` output here keeps `discovery-page` as the single place that would
   * coordinate cross-component selection if that need ever comes up. */
  onMuralSelected(): void {
    // Intentionally empty for this block — see comment above.
  }

  /** Delegates to `GeolocationService` (Block 6). On any of its 3 typed error cases, the query is
   * NOT fired automatically — falls back to the manual lat/lng form instead (FR-06). */
  private requestGeolocation(): void {
    this.geolocationService.getCurrentPosition().then(
      (coordinates) => {
        this.center.set(coordinates);
        this.fetchNearbyMurals(coordinates.latitude, coordinates.longitude);
      },
      () => {
        this.manualLocationRequired.set(true);
      },
    );
  }

  private fetchNearbyMurals(latitude: number, longitude: number): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.discoveryService.getNearbyMurals(latitude, longitude).subscribe({
      next: (items) => {
        this.loading.set(false);
        this.items.set(items);
        // Oculta "Buscar en esta área" (Block 3) al asentarse la consulta — no-op si el botón ya
        // estaba oculto (geolocalización inicial o búsqueda manual, que no lo muestran).
        this.showSearchAreaButton.set(false);
      },
      error: (error: ApiError) => {
        this.loading.set(false);
        this.errorMessage.set(error.message);
        this.showSearchAreaButton.set(false);
      },
    });
  }

  private parseNumberInput(event: Event): number | null {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    return Number.isNaN(value) ? null : value;
  }
}
