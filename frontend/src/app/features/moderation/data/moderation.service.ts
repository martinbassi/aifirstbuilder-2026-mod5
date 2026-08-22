import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import {
  ModerationActionResponse,
  ModerationClient,
  MuralResponse,
} from '../../../core/api-client/api-client.generated';
import { toApiError } from '../../../core/http/api-error';

export interface PendingMuralsPage {
  murals: MuralResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/**
 * Wraps `ModerationClient` (NSwag-generated, see api-client.generated.ts) for the `moderation`
 * feature. Components never call `ModerationClient`/`ApiException` directly — only this service,
 * per AGENTS.md. Same pattern as `MuralService`.
 */
@Injectable({ providedIn: 'root' })
export class ModerationService {
  private readonly moderationClient = inject(ModerationClient);

  /**
   * Fetches a page of `Pending` murals for an Administrator. Server-side authorization
   * (`[Authorize(Roles = "Administrator")]`) is the real gate — this service just transports the
   * request/response.
   */
  getPending(page?: number, pageSize?: number): Observable<PendingMuralsPage> {
    return this.moderationClient.pending(page, pageSize).pipe(
      map((response) => ({
        murals: response.murals ?? [],
        page: response.page ?? 1,
        pageSize: response.pageSize ?? 0,
        totalCount: response.totalCount ?? 0,
      })),
      catchError((error: unknown) => throwError(() => toApiError(error))),
    );
  }

  /** Approves a `Pending` mural, moving it to `Published`. */
  approve(id: string): Observable<ModerationActionResponse> {
    return this.moderationClient
      .approve(id)
      .pipe(catchError((error: unknown) => throwError(() => toApiError(error))));
  }

  /**
   * Rejects a `Pending` mural, moving it to `Rejected`. Named `rejectMural` (not `reject`) to keep
   * the service's public API descriptive, independent of the generated client's method name.
   */
  rejectMural(id: string): Observable<ModerationActionResponse> {
    return this.moderationClient
      .reject(id)
      .pipe(catchError((error: unknown) => throwError(() => toApiError(error))));
  }
}
