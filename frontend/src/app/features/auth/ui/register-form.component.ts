import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { ApiError } from '../../../core/http/api-error';
import { AuthService } from '../data/auth.service';
import { AuthCardComponent } from './auth-card/auth-card.component';

interface RegisterFormControls {
  username: FormControl<string>;
  email: FormControl<string>;
  password: FormControl<string>;
}

/**
 * Client-side mirror of the backend's password rule (FR-03, RegisterUserCommandValidator): 8-128
 * chars, at least one letter and one digit. Server-side validation remains the authority — this is
 * only first-layer feedback (spec Block 8, Input validation).
 */
function passwordComplexityValidator(control: FormControl<string>): ValidationErrors | null {
  const value = control.value ?? '';
  const hasLetter = /[a-zA-Z]/.test(value);
  const hasDigit = /\d/.test(value);
  return hasLetter && hasDigit ? null : { passwordComplexity: true };
}

@Component({
  selector: 'app-register-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AuthCardComponent,
    NzAlertModule,
    NzButtonModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
  ],
  templateUrl: './register-form.component.html',
  styleUrls: ['./auth-form.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterFormComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = new FormGroup<RegisterFormControls>({
    username: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)],
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.minLength(8),
        Validators.maxLength(128),
        passwordComplexityValidator,
      ],
    }),
  });

  readonly submitting = signal(false);
  /** Shown verbatim — never distinguished by field, so it doesn't repeat FR-02's leak client-side. */
  readonly errorMessage = signal<string | null>(null);

  registerWithGoogle(): void {
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.submitting.set(true);

    const { username, email, password } = this.form.getRawValue();
    this.authService.register({ username, email, password }).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigate(['/login']);
      },
      error: (error: ApiError) => {
        this.submitting.set(false);
        this.errorMessage.set(error.message);
      },
    });
  }
}
