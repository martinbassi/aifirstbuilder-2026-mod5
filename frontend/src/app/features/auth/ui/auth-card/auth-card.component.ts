import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NzCardModule } from 'ng-zorro-antd/card';

/**
 * Purely presentational — no `@Input()`, no logic, no service calls (spec Block 1). Lives under
 * `features/auth/ui/` (not `shared/`): `login` and `register` are 2 screens of the SAME feature
 * (`auth`), not two different features.
 */
@Component({
  selector: 'app-auth-card',
  standalone: true,
  imports: [NzCardModule],
  templateUrl: './auth-card.component.html',
  styleUrl: './auth-card.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthCardComponent {}
