import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { AddressesClient, AddressSuggestionDto } from '../../../core/api-client/api-client.generated';
import { toApiError } from '../../../core/http/api-error';

/**
 * Reexport of the type NSwag generates for the backend DTO (Block 1 FEAT-011,
 * `AddressSuggestionDto`) — never hand-redeclared, same pattern as `MuralResponse`/
 * `CreateMuralResponse` in `mural.service.ts`, per AGENTS.md ("Do not hand-write or commit
 * TypeScript classes/interfaces that represent API contracts").
 */
export type AddressSuggestion = AddressSuggestionDto;

/**
 * Wraps `AddressesClient` (NSwag-generated, see api-client.generated.ts) for the `murals` feature.
 * Components never call `AddressesClient`/`ApiException` directly — only this service, per
 * AGENTS.md. All address lookups go through our own backend (Block 1 FEAT-011), which proxies the
 * external provider `direcciones.ide.uy` — this service never talks to that host directly (AC-20).
 */
@Injectable({ providedIn: 'root' })
export class AddressService {
  private readonly addressesClient = inject(AddressesClient);

  /**
   * Autocomplete search (FR-19/AC-17). An empty/no-match result from the provider is a normal `200`
   * with an empty list (AC-18) — it never reaches `catchError`. Only a `503` (provider down,
   * AC-19) does.
   */
  search(query: string): Observable<AddressSuggestion[]> {
    return this.addressesClient.searchAddresses(query).pipe(
      map((response) => response.suggestions ?? []),
      catchError((error: unknown) => throwError(() => toApiError(error))),
    );
  }

  /**
   * Reverse geocoding (AC-03). A `null` `suggestion` (no match for the given coordinates) is a
   * normal `200` value, not an error — only a `503` (provider down, AC-19) goes through
   * `catchError`.
   */
  reverseGeocode(lat: number, lng: number): Observable<AddressSuggestion | null> {
    return this.addressesClient.reverseGeocodeAddress(lat, lng).pipe(
      map((response) => response.suggestion ?? null),
      catchError((error: unknown) => throwError(() => toApiError(error))),
    );
  }
}
