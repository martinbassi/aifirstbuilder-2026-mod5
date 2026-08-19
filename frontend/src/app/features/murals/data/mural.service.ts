import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import {
  CreateMuralResponse,
  MuralResponse,
  MuralsClient,
} from '../../../core/api-client/api-client.generated';
import { toApiError } from '../../../core/http/api-error';

export interface CreateMuralRequest {
  photo: File;
  latitude: number;
  longitude: number;
}

/**
 * Wraps `MuralsClient` (NSwag-generated, see api-client.generated.ts) for the `murals` feature.
 * Components never call `MuralsClient`/`ApiException` directly — only this service, per AGENTS.md.
 */
@Injectable({ providedIn: 'root' })
export class MuralService {
  private readonly muralsClient = inject(MuralsClient);

  /**
   * Uploads a new mural (photo + location). The backend decides the resulting `Status`
   * (`Pending`/`Rejected`) based on its own NSFW scan — this service only transports the request
   * and the response, it does not interpret or filter the result.
   */
  create(request: CreateMuralRequest): Observable<CreateMuralResponse> {
    return this.muralsClient
      .muralsPOST(
        { data: request.photo, fileName: request.photo.name },
        request.latitude,
        request.longitude,
      )
      .pipe(catchError((error: unknown) => throwError(() => toApiError(error))));
  }

  /**
   * Fetches a single mural by Id, including its short-lived signed photo URL. A `404` covers both
   * "does not exist" and "exists but the caller has no access" (backend anti-enumeration
   * mitigation, see spec Block 5) — this service does not distinguish either, it just surfaces
   * whatever `ApiError` the backend produced.
   */
  getById(id: string): Observable<MuralResponse> {
    return this.muralsClient
      .muralsGET(id)
      .pipe(catchError((error: unknown) => throwError(() => toApiError(error))));
  }
}
