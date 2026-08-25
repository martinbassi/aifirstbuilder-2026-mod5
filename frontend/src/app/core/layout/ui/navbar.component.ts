import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { filter, map, startWith } from 'rxjs';
import { LayoutStore } from '../state/layout.store';

/**
 * Top navbar (spec Block 3): shows a text identifying the active screen/route (FR-13) and an
 * expand/collapse control that mirrors the sidebar's own control (FR-14) — both read/write the same
 * `LayoutStore` state, so either one toggles the other.
 */
@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [NzIconModule],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavbarComponent {
  readonly layoutStore = inject(LayoutStore);
  private readonly router = inject(Router);

  /**
   * Derived from `router.events`, per spec Block 3 "Logic". `startWith(this.readActiveTitle())`
   * covers the initial render: the component may be created after the `NavigationEnd` for the
   * currently active route has already fired (nothing left to react to), so the title has to be
   * read eagerly at construction time too, not only on future navigations. `requireSync: true` is
   * safe here because `startWith` guarantees a synchronous first emission on subscribe, which is
   * what `toSignal` needs to avoid an `undefined` initial value.
   */
  readonly title = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.readActiveTitle()),
      startWith(this.readActiveTitle()),
    ),
    { requireSync: true },
  );

  /**
   * Walks the activated route tree down to its deepest child and reads `data['title']` there.
   * Returns an empty string instead of throwing when `title` is missing (spec Block 3 "Error
   * handling") — the navbar must never break because a route forgot to declare a title. `snapshot`
   * itself is read with optional chaining: on the first activation of a lazily-loaded parent route
   * (e.g. `AppShellComponent`) with a lazily-loaded child, this component can be constructed before
   * the router finishes `advanceActivatedRoute` on the child's `ActivatedRoute`, leaving
   * `node.snapshot` transiently `undefined`.
   */
  private readActiveTitle(): string {
    let node = this.router.routerState.root;
    while (node.firstChild) {
      node = node.firstChild;
    }
    return (node.snapshot?.data['title'] as string | undefined) ?? '';
  }
}
