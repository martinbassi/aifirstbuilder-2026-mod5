import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { MuralResponse } from '../../../core/api-client/api-client.generated';
import { ApiError } from '../../../core/http/api-error';
import { ModerationService } from '../data/moderation.service';

/**
 * Admin-only "moderación mínima" screen (spec Block 7). Standalone, signals-based — no NgRx/other
 * state lib, same as `CreateMuralFormComponent`. Consumes `ModerationService` only, never
 * `ModerationClient`/the NSwag client directly (AGENTS.md). Reachable only via the `/moderation`
 * route, gated by `authGuard`/`adminGuard` (`app.routes.ts`) — this component does not check the
 * session/role itself, same division of responsibility as `CreateMuralFormComponent`.
 */
@Component({
  selector: 'app-pending-murals-list',
  standalone: true,
  imports: [NzAlertModule, NzButtonModule, NzCardModule],
  templateUrl: './pending-murals-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PendingMuralsListComponent implements OnInit {
  private readonly moderationService = inject(ModerationService);

  readonly murals = signal<MuralResponse[]>([]);
  readonly page = signal(1);
  /** Read off the backend response — never hardcoded client-side, so the pagination math below
   * stays correct even if the backend's default page size changes. */
  readonly pageSize = signal(0);
  readonly totalCount = signal(0);

  readonly loading = signal(false);
  /** Inline, no-auto-retry error for a failed `getPending()` (sad path). */
  readonly loadError = signal<string | null>(null);
  /** Per-mural inline error for a failed `approve`/`rejectMural` — the item stays in `murals()`. */
  readonly itemErrors = signal<Record<string, string>>({});

  readonly canGoPrevious = computed(() => this.page() > 1);
  readonly canGoNext = computed(() => this.page() * this.pageSize() < this.totalCount());

  ngOnInit(): void {
    this.loadPage(1);
  }

  previousPage(): void {
    if (!this.canGoPrevious()) {
      return;
    }
    this.loadPage(this.page() - 1);
  }

  nextPage(): void {
    if (!this.canGoNext()) {
      return;
    }
    this.loadPage(this.page() + 1);
  }

  approve(id: string): void {
    this.clearItemError(id);
    this.moderationService.approve(id).subscribe({
      next: () => this.removeMural(id),
      error: (error: ApiError) => this.setItemError(id, error.message),
    });
  }

  rejectMural(id: string): void {
    this.clearItemError(id);
    this.moderationService.rejectMural(id).subscribe({
      next: () => this.removeMural(id),
      error: (error: ApiError) => this.setItemError(id, error.message),
    });
  }

  itemError(id: string): string | null {
    return this.itemErrors()[id] ?? null;
  }

  private loadPage(page: number): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.moderationService.getPending(page).subscribe({
      next: (result) => {
        this.murals.set(result.murals);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: (error: ApiError) => {
        this.loading.set(false);
        this.loadError.set(error.message);
      },
    });
  }

  private removeMural(id: string): void {
    this.murals.update((murals) => murals.filter((mural) => mural.id !== id));
    this.clearItemError(id);
  }

  private setItemError(id: string, message: string): void {
    this.itemErrors.update((errors) => ({ ...errors, [id]: message }));
  }

  private clearItemError(id: string): void {
    this.itemErrors.update((errors) => {
      if (!(id in errors)) {
        return errors;
      }
      const next = { ...errors };
      delete next[id];
      return next;
    });
  }
}
