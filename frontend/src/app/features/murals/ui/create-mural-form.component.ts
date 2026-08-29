import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzUploadChangeParam, NzUploadFile, NzUploadModule } from 'ng-zorro-antd/upload';
import { ApiError } from '../../../core/http/api-error';
import { GeolocationCoordinates, GeolocationService } from '../../../shared/geolocation.service';
import { CreateMuralRequest, MuralService } from '../data/mural.service';
import { NzNotificationService } from 'ng-zorro-antd/notification';
import { Router } from '@angular/router';
import * as L from 'leaflet';
import { NzAlertModule } from 'ng-zorro-antd/alert';

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
const TILE_LAYER_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const TILE_LAYER_ATTRIBUTION =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

// `_getIconUrl` no está en las definiciones de tipos públicas de Leaflet — es el workaround
// documentado de la propia librería para bundlers ESM (esbuild/Angular 21), que sin esto resuelven
// mal la URL de los íconos por defecto y dejan los marcadores del mapa invisibles (FIX-002).
// eslint-disable-next-line @typescript-eslint/no-explicit-any
delete (L.Icon.Default.prototype as any)._getIconUrl;

L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'images/leaflet/marker-icon-2x.png',
  iconUrl: 'images/leaflet/marker-icon.png',
  shadowUrl: 'images/leaflet/marker-shadow.png',
});

/**
 * Standalone form to create a mural (photo + location). Consumes `MuralService` only — never the
 * NSwag-generated client directly (AGENTS.md). Does not check the session itself; the protected
 * route added by Block 8 handles that structurally (FR-07).
 */
@Component({
  selector: 'app-create-mural-form',
  standalone: true,
  imports: [
    FormsModule,
    NzButtonModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzUploadModule,
    NzAlertModule,
  ],
  templateUrl: './create-mural-form.component.html',
  styleUrls: ['./create-mural-form.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateMuralFormComponent implements OnInit, OnDestroy {
  private readonly muralService = inject(MuralService);
  private readonly geolocationService = inject(GeolocationService);
  private readonly notification = inject(NzNotificationService);
  private readonly router = inject(Router);

  readonly selectedFile = signal<File | null>(null);
  /** UX-only inline feedback for the file selector — see `ALLOWED_PHOTO_TYPES` above. */
  readonly fileError = signal<boolean>(false);
  /** Backs `[nzFileList]` on `<nz-upload>` (spec-FEAT-008 Block 1) — at most 1 entry (`nzMaxCount`).
   * Populated entirely inside `beforeUpload`, never by `nz-upload` itself: returning `false`
   * synchronously from `nzBeforeUpload` stops the upload flow before `onStart`, the only point
   * that would otherwise populate it. */
  readonly fileList = signal<NzUploadFile[]>([]);

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

  private map: L.Map | null = null;

  readonly canSubmit = computed(() => {
    const file = this.selectedFile();
    const title = this.title();
    const latitude = this.latitude();
    const longitude = this.longitude();
    return (
      file !== null &&
      this.fileError() === false &&
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

  /**
   * Única fuente de alta de archivo desde `nz-upload` (spec-FEAT-008 Block 1). Class field de
   * flecha (no un método): `nz-upload` lo invoca con un `this` distinto al de la instancia del
   * componente (verificado contra `ng-zorro-antd-upload.mjs`), así que un método común perdería el
   * `this` y rompería el acceso a los signals. SIEMPRE retorna `false` de forma síncrona: nunca deja
   * pasar la subida real de `nz-upload` (FR-07), la propia función valida y arma `fileList`
   * manualmente en el mismo paso.
   */
  readonly beforeUpload = (file: NzUploadFile): boolean => {
    const rawFile = ((file as { originFileObj?: File }).originFileObj ?? file) as unknown as File;

    if (!ALLOWED_PHOTO_TYPES.includes(rawFile.type)) {
      this.fileError.set(true);
      this.notification.create(
        'error',
        'Notificación',
        'El archivo debe ser una imagen JPEG, PNG o WebP.',
      );
      return false;
    }

    if (rawFile.size > MAX_PHOTO_SIZE_BYTES) {
      this.fileError.set(true);
      this.notification.create('error', 'Notificación', 'El archivo no puede superar los 10 MB.');
      return false;
    }

    // Replacement (spec-FEAT-008 Block 2, AC-02/NFR-01): revoke the previous entry's `thumbUrl`
    // before overwriting `fileList` with the new one, otherwise the old Blob URL leaks for the
    // lifetime of the page.
    this.revokeCurrentThumbUrl();

    this.fileError.set(false);
    this.selectedFile.set(rawFile);
    this.fileList.set([
      {
        // `nz-upload` ya adjunta un `uid` al `File` crudo antes de invocar `nzBeforeUpload`
        // (`attachUid` en `ng-zorro-antd-upload.mjs`); el fallback sólo cubre la invocación directa
        // desde tests, que no pasan por ese paso interno.
        uid: (file as { uid?: string }).uid ?? Math.random().toString(36).substring(2),
        name: file.name ?? rawFile.name,
        status: 'done',
        thumbUrl: URL.createObjectURL(rawFile),
        originFileObj: rawFile,
      },
    ]);

    return false;
  };

  /**
   * Único punto de baja de archivo (spec-FEAT-008 Block 2, AC-03/NFR-01). A diferencia de
   * `beforeUpload`, este SÍ llega vía `(nzChange)`: el ícono de eliminar de `nz-upload-list`
   * dispara el flujo interno de remoción de `nz-upload` independientemente de que
   * `nzBeforeUpload` haya devuelto `false` en el alta. Class field de flecha por el mismo motivo
   * que `beforeUpload`: `nz-upload` lo invoca con un `this` distinto al de la instancia del
   * componente.
   */
  readonly onUploadChange = (event: NzUploadChangeParam): void => {
    if (event.type !== 'removed') {
      return;
    }

    this.revokeCurrentThumbUrl();

    this.fileList.set([]);
    this.selectedFile.set(null);
    this.fileError.set(false);
  };

  /** Revoca el `thumbUrl` de la entrada actual, si existe, para no dejar un Blob URL vivo cuando
   * el usuario navega fuera del formulario con un archivo todavía seleccionado (AC-07/NFR-01). */
  ngOnDestroy(): void {
    this.revokeCurrentThumbUrl();
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
      next: (data) => {
        this.submitting.set(false);
        if (data.status === 'Pending') {
          this.notification.create(
            'success',
            'Notificación',
            'Tu mural quedó pendiente de revisión.',
          );
          this.router.navigate(['/discover']);
        } else {
          this.notification.create(
            'error',
            'Notificación',
            'Tu mural fue rechazado ya que no cumple con los criterios de moderación.',
          );
        }
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

        this.setCoordinatesInMap(coordinates);
      },
      () => {
        this.manualLocationRequired.set(true);
      },
    );
  }

  private setCoordinatesInMap(coordinates: GeolocationCoordinates): void {
    this.map = L.map('location-preview', {
      center: [coordinates.latitude, coordinates.longitude],
      zoom: 16,

      // El usuario no puede interactuar
      dragging: false,
      scrollWheelZoom: false,
      doubleClickZoom: false,
      boxZoom: false,
      keyboard: false,
      touchZoom: false,
      zoomControl: false,
    });

    L.tileLayer(TILE_LAYER_URL, { attribution: TILE_LAYER_ATTRIBUTION }).addTo(this.map);
    L.marker([coordinates.latitude, coordinates.longitude]).addTo(this.map);
  }

  private parseNumberInput(event: Event): number | null {
    const value = (event.target as HTMLInputElement).valueAsNumber;
    return Number.isNaN(value) ? null : value;
  }

  /** Revoca el `thumbUrl` de la entrada actual de `fileList`, si existe. Extraído de
   * `beforeUpload`, `onUploadChange` y `ngOnDestroy`, que repetían el mismo chequeo antes de
   * pisar/vaciar la lista o al destruir el componente (AC-02/AC-03/AC-07/NFR-01). */
  private revokeCurrentThumbUrl(): void {
    if (this.fileList().length > 0) {
      URL.revokeObjectURL(this.fileList()[0].thumbUrl as string);
    }
  }
}
