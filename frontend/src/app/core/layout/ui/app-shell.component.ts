import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LayoutStore } from '../state/layout.store';
import { NavbarComponent } from './navbar.component';
import { SidebarComponent } from './sidebar.component';

/**
 * Composition root for the global navigation shell (spec Block 4). Wraps `<router-outlet />` with
 * `SidebarComponent` (Block 2) and `NavbarComponent` (Block 3) in a CSS grid: sidebar spans the left
 * column full height, navbar occupies the top row of the right column, and the routed content fills
 * the rest (FR-01, FR-12). It injects `LayoutStore` itself (not just relying on the child components
 * doing so) to toggle the `.collapsed` class on the grid container, which drives the sidebar column's
 * width via CSS — the single source of truth for "collapsed" stays `LayoutStore`, read here and by
 * Block 2/Block 3 independently.
 *
 * Route wiring (which paths render this shell) is Block 5's responsibility, not this component's.
 */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, NavbarComponent],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppShellComponent {
  readonly layoutStore = inject(LayoutStore);
}
