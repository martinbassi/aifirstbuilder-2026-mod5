import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import {
  DiscoveryClient,
  NearbyMuralItemResponse,
} from '../../../core/api-client/api-client.generated';
import { toApiError } from '../../../core/http/api-error';

/**
 * Wraps `DiscoveryClient` (NSwag-generated, see api-client.generated.ts) for the `discovery`
 * feature. Components never call `DiscoveryClient`/`ApiException` directly — only this service,
 * per AGENTS.md. Same pattern as `MuralService`/`ModerationService`.
 */
@Injectable({ providedIn: 'root' })
export class DiscoveryService {
  private readonly discoveryClient = inject(DiscoveryClient);

  /**
   * Fetches `Published` murals within `radiusKm` (backend default: 5 km) of `latitude`/`longitude`,
   * ordered by distance (never reordered here — the backend already sorts, spec Block 2).
   *
   * `lat`/`lng` are typed optional in `DiscoveryClient.getNearbyMurals` even though the backend
   * requires them (NSwag artifact) — always passed explicitly here, never omitted, so a `0`
   * (falsy but valid) latitude/longitude is never dropped.
   */
  getNearbyMurals(
    latitude: number,
    longitude: number,
    radiusKm?: number,
  ): Observable<NearbyMuralItemResponse[]> {
    return this.discoveryClient.getNearbyMurals(latitude, longitude, radiusKm).pipe(
      map((response) => response.items ?? []),
      catchError((error: unknown) => throwError(() => toApiError(error))),
    );
  }
}
