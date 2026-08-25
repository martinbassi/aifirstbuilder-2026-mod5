import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthService } from '../../../features/auth/data/auth.service';
import { SessionStore } from '../../../features/auth/state/session.store';
import { LogoComponent } from '../../../shared/logo/logo.component';
import { LayoutStore } from '../state/layout.store';

/**
 * Global left-hand navigation sidebar (spec Block 2). Menu items and the session footer are
 * derived from `SessionStore` — the same source of truth `authGuard`/`adminGuard` already read —
 * so authorization is never duplicated here (PRD "Risks and Mitigations").
 */
@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NzIconModule, LogoComponent],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SidebarComponent {
  readonly sessionStore = inject(SessionStore);
  readonly layoutStore = inject(LayoutStore);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  /**
   * `AuthService.logout()` already clears `SessionStore` internally in both its `next` and `error`
   * branches (see `auth.service.ts`) — but the spec (Block 2 "Logic"/"Error handling") explicitly
   * requires the sidebar to also call `clearSession()` and navigate to `/login` from both branches
   * itself, so the redirect does not depend on a caller ever inspecting what `AuthService` did
   * internally. The extra `clearSession()` call here is idempotent.
   */
  onLogout(): void {
    this.authService.logout().subscribe({
      next: () => {
        this.sessionStore.clearSession();
        void this.router.navigate(['/login']);
      },
      error: () => {
        this.sessionStore.clearSession();
        void this.router.navigate(['/login']);
      },
    });
  }
}
