import { Injectable } from '@angular/core';

/** Resolved coordinates, already unwrapped from the native `GeolocationPosition` shape. */
export interface GeolocationCoordinates {
  latitude: number;
  longitude: number;
}

/**
 * Typed error surfaced by `GeolocationService` — the native `GeolocationPositionError` is never
 * propagated to callers (AGENTS.md, Frontend → Error handling). `kind` discriminates the 3 cases
 * the browser API distinguishes via `error.code`:
 * - `denied`: the user rejected the permission prompt (`PERMISSION_DENIED`).
 * - `unavailable`: the position could not be determined (`POSITION_UNAVAILABLE`), or the browser
 *   has no `navigator.geolocation` at all (unsupported — treated the same, since from a caller's
 *   perspective both mean "no position can be obtained here").
 * - `timeout`: the request took longer than the browser's internal timeout (`TIMEOUT`).
 */
export interface GeolocationError {
  kind: 'denied' | 'unavailable' | 'timeout';
}

/**
 * Wraps `navigator.geolocation.getCurrentPosition` for consumers across features
 * (`create-mural-form`, and `discovery/` — spec Block 7). First tenant of `shared/`.
 *
 * Promise, not Observable: `getCurrentPosition` is a one-shot browser callback (a single
 * success/error, never a stream), so a Promise models it directly without the subscription
 * lifecycle (and potential leak if a component navigates away before the callback fires) an
 * Observable would introduce for no benefit here. This differs from `data/` services like
 * `MuralService`, which wrap the NSwag/HttpClient pipeline and stay Observable-based to match
 * RxJS `catchError` idioms already used there — this service wraps a browser API, not HTTP.
 */
@Injectable({ providedIn: 'root' })
export class GeolocationService {
  getCurrentPosition(): Promise<GeolocationCoordinates> {
    const geolocation = navigator.geolocation;
    if (!geolocation) {
      return Promise.reject<GeolocationCoordinates>({ kind: 'unavailable' } satisfies GeolocationError);
    }

    return new Promise<GeolocationCoordinates>((resolve, reject) => {
      geolocation.getCurrentPosition(
        (position: GeolocationPosition) => {
          resolve({
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
          });
        },
        (error: GeolocationPositionError) => {
          reject(this.toGeolocationError(error));
        },
        { enableHighAccuracy: true },
      );
    });
  }

  /**
   * Maps `error.code` using the standard numeric values from the Geolocation API spec
   * (`PERMISSION_DENIED = 1`, `POSITION_UNAVAILABLE = 2`, `TIMEOUT = 3`) rather than the
   * instance constants (`error.PERMISSION_DENIED`, etc.) — those constants are only guaranteed
   * present on a real `GeolocationPositionError` from the browser, not on the plain objects
   * tests construct to simulate one.
   */
  private toGeolocationError(error: GeolocationPositionError): GeolocationError {
    switch (error.code) {
      case 1:
        return { kind: 'denied' };
      case 3:
        return { kind: 'timeout' };
      case 2:
      default:
        return { kind: 'unavailable' };
    }
  }
}
