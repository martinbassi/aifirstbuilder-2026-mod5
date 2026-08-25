import { Injectable, signal } from '@angular/core';

/**
 * Global layout shell state: whether the navigation sidebar is collapsed or expanded.
 *
 * The initial value is derived from `window.innerWidth` at construction time (FR-10/FR-11): wide
 * viewports (≥ 992px) start expanded, narrow ones (< 992px) start collapsed. There is no listener
 * on `resize` — the PRD only requires the initial state to depend on the width at load time, not a
 * continuous reaction to resizing (see "Out of Scope" in the PRD). There is no persistence either
 * (unlike `SessionStore`, which persists its token in `sessionStorage`): the PRD explicitly
 * excludes persisting the expanded/collapsed state between page loads — it is recalculated from the
 * breakpoint on every load.
 */
@Injectable({ providedIn: 'root' })
export class LayoutStore {
  private readonly collapsedSignal = signal<boolean>(window.innerWidth < 992);

  readonly collapsed = this.collapsedSignal.asReadonly();

  /** Flips the sidebar between expanded and collapsed. Called from the sidebar or the navbar. */
  toggle(): void {
    this.collapsedSignal.update((v) => !v);
  }
}
