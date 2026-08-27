import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { ApiError } from '../../../core/http/api-error';
import { GeolocationService } from '../../../shared/geolocation.service';
import { CreateMuralRequest, MuralService } from '../data/mural.service';
import { FormsModule } from '@angular/forms';

/** Same allowlist the backend accepts (Block 4) — this check is UX-only feedback, never the
 * authority: `file.type` is client-controlled and trivially spoofable. The real gate is the
 * backend's byte-signature (magic number) validation, not duplicated here (spec Block 7). */
const ALLOWED_PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
/** Mirrors NFR-01 (backend `IFormFile.Length <= 10MB`) — same UX-only caveat as above. */
const MAX_PHOTO_SIZE_BYTES = 10 * 1024 * 1024;
const MIN_LATITUDE = -90;
const MAX_LATITUDE = 90;
const MIN_LONGITUDE = -180;
const MAX_LONGITUDE = 180;

/**
 * Standalone form to create a mural (photo + location). Consumes `MuralService` only — never the
 * NSwag-generated client directly (AGENTS.md). Does not check the session itself; the protected
 * route added by Block 8 handles that structurally (FR-07).
 */
@Component({
  selector: 'app-create-mural-form',
  standalone: true,
  imports: [FormsModule, NzAlertModule, NzButtonModule, NzFormModule, NzInputModule],
  templateUrl: './create-mural-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateMuralFormComponent implements OnInit {
  private readonly muralService = inject(MuralService);
  private readonly geolocationService = inject(GeolocationService);

  readonly selectedFile = signal<File | null>(null);
  /** UX-only inline feedback for the file selector — see `ALLOWED_PHOTO_TYPES` above. */
  readonly fileError = signal<string | null>(null);

  readonly title = signal<string | null>(null);
  readonly latitude = signal<number | null>(null);
  readonly longitude = signal<number | null>(null);
  /** True once geolocation is known to be unavailable/denied — reveals the manual inputs without
   * interrupting the rest of the form (FR-06/AC-04). */
  readonly manualLocationRequired = signal(false);

  readonly submitting = signal(false);
  readonly successMessage = signal<string | null>(null);
  /** On a failed submit this is the ONLY thing that resets — the file and coordinates stay put so
   * "Reintentar" can resubmit them without asking again (FR-13/AC-11). */
  readonly errorMessage = signal<string | null>(null);

  readonly canSubmit = computed(() => {
    const file = this.selectedFile();
    const title = this.title();
    const latitude = this.latitude();
    const longitude = this.longitude();
    return (
      file !== null &&
      this.fileError() === null &&
      title !== null &&
      title.trim().length > 0 &&
      latitude !== null &&
      latitude >= MIN_LATITUDE &&
      latitude <= MAX_LATITUDE &&
      longitude !== null &&
      longitude >= MIN_LONGITUDE &&
      longitude <= MAX_LONGITUDE &&
      !this.submitting()
    );
  });

  ngOnInit(): void {
    this.requestGeolocation();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    if (!file) {
      this.selectedFile.set(null);
      this.fileError.set(null);
      return;
    }

    if (!ALLOWED_PHOTO_TYPES.includes(file.type)) {
      this.selectedFile.set(null);
      this.fileError.set('El archivo debe ser una imagen JPEG, PNG o WebP.');
      return;
    }

    if (file.size > MAX_PHOTO_SIZE_BYTES) {
      this.selectedFile.set(null);
      this.fileError.set('El archivo no puede superar los 10 MB.');
      return;
    }

    this.fileError.set(null);
    this.selectedFile.set(file);
  }

  onTitleChange(event: Event): void {
    this.title.set((event.target as HTMLInputElement).value);
  }

  onLatitudeChange(event: Event): void {
    this.latitude.set(this.parseNumberInput(event));
  }

  onLongitudeChange(event: Event): void {
    this.longitude.set(this.parseNumberInput(event));
  }

  submit(): void {
    if (!this.canSubmit()) {
      return;
    }

    const request: CreateMuralRequest = {
      title: this.title() as string,
      photo: this.selectedFile() as File,
      latitude: this.latitude() as number,
      longitude: this.longitude() as number,
    };

    this.errorMessage.set(null);
    this.submitting.set(true);

    this.muralService.create(request).subscribe({
      next: () => {
        this.submitting.set(false);
        this.successMessage.set('Tu mural quedó pendiente de revisión.');
      },
      error: (error: ApiError) => {
        this.submitting.set(false);
        this.errorMessage.set(error.message);
      },
    });
  }

  /** Resubmits the SAME file/coordinates already held in signals — never asks the user again
   * (FR-13/AC-11). */
  retry(): void {
    this.submit();
  }

  /** Delegates to `GeolocationService` (Block 6). On any of its 3 typed error cases, falls back
   * to the same manual lat/lng input already offered before this extraction (FR-06/AC-04). */
  private requestGeolocation(): void {
    this.geolocationService.getCurrentPosition().then(
      (coordinates) => {
        this.latitude.set(coordinates.latitude);
        this.longitude.set(coordinates.longitude);
      },
      () => {
        this.manualLocationRequired.set(true);
      },
    );
  }

  private parseNumberInput(event: Event): number | null {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    return Number.isNaN(value) ? null : value;
  }
}
