import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Purely presentational — no `@Input()`, no logic, no service calls (spec Block 3). Shared between
 * `auth/login` and `auth/register` (2 features), hence its place under `shared/`.
 */
@Component({
  selector: 'app-logo',
  standalone: true,
  imports: [],
  templateUrl: './logo.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogoComponent {}
