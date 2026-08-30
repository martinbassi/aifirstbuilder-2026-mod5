import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  TemplateRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzAutocompleteModule } from 'ng-zorro-antd/auto-complete';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzUploadChangeParam, NzUploadFile, NzUploadModule } from 'ng-zorro-antd/upload';
import { ApiError } from '../../../core/http/api-error';
import { GeolocationCoordinates, GeolocationService } from '../../../shared/geolocation.service';
import { AddressService, AddressSuggestion } from '../data/address.service';
import { CreateMuralRequest, MuralService } from '../data/mural.service';
import { NzNotificationComponent, NzNotificationService } from 'ng-zorro-antd/notification';
import { Router } from '@angular/router';
import * as L from 'leaflet';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import {
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  of,
  switchMap,
  tap,
} from 'rxjs';

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
/** NFR-04 (spec-FEAT-011 Block 3): debounce del campo de dirección antes de llamar a
 * `address.service.ts#search`. */
const ADDRESS_SEARCH_DEBOUNCE_MS = 300;
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
    NzAutocompleteModule,
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
  private readonly addressService = inject(AddressService);
  private readonly notification = inject(NzNotificationService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /** Template usado por `NzNotificationService.template()` (AC-11) — contiene el botón
   * `data-testid="retry-button"` que dispara `retry()`. Se resuelve una única vez, tras el primer
   * render de la vista; `submit()` solo lo usa desde el handler de error, que jamás corre antes de
   * eso. */
  private readonly retryNotificationTemplate = viewChild.required<
    TemplateRef<{ $implicit: NzNotificationComponent; data: unknown }>
  >('retryNotificationTemplate');

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

  /** Texto libre del campo de dirección (spec-FEAT-011 Block 3). Se precompleta por reverse
   * geocoding tras un GPS exitoso, o al seleccionar una sugerencia del autocomplete. */
  readonly addressQuery = signal<string>('');
  /** Sugerencias vigentes del autocomplete (AC-17/AC-18). */
  readonly addressSuggestions = signal<AddressSuggestion[]>([]);
  /** True cuando `address.service.ts#search` devolvió un error de proveedor caído (503, AC-19) —
   * señal DELIBERADAMENTE separada de `manualLocationRequired`: una es "GPS denegado", la otra "el
   * proveedor de direcciones externo no responde"; el template necesita distinguirlas para mostrar
   * el mensaje correcto (hallazgo del arch-auditor documentado en el spec). */
  readonly addressProviderUnavailable = signal(false);

  /** Alimenta el pipeline de autocomplete (debounce 300ms, NFR-04) desde `onAddressQueryChange`. */
  private readonly addressQuery$ = new Subject<string>();

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

    // AC-17/AC-18/AC-19/NFR-04: debounce 300ms + distinctUntilChanged antes de golpear
    // `address.service.ts#search`. Una consulta vacía no llega al servicio (corta con `of([])`,
    // input validation del spec Block 3). Un 503 del proveedor no propaga como excepción no
    // manejada: revela el fallback manual en vez de romper el resto del formulario (AC-19).
    this.addressQuery$
      .pipe(
        debounceTime(ADDRESS_SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        switchMap((query) => {
          if (query.trim().length === 0) {
            return of<AddressSuggestion[]>([]);
          }
          return this.addressService.search(query).pipe(
            // Asunción (el spec no lo especifica): una búsqueda exitosa después de una previa
            // marcada `Unavailable` confirma que el proveedor volvió — se limpia la señal para no
            // dejar el fallback manual pegado indefinidamente tras una falla transitoria.
            tap(() => this.addressProviderUnavailable.set(false)),
            catchError(() => {
              this.addressProviderUnavailable.set(true);
              return of<AddressSuggestion[]>([]);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((suggestions) => {
        this.addressSuggestions.set(suggestions);
      });
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

  /** Alimenta el pipeline de autocomplete de dirección (spec-FEAT-011 Block 3, NFR-04). */
  onAddressQueryChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.addressQuery.set(value);
    this.addressQuery$.next(value);
  }

  /** Selección de una sugerencia del autocomplete (AC-05/AC-21): setea `latitude`/`longitude` (lo
   * que `submit()` envía no cambia) y reutiliza `setCoordinatesInMap()`, el mismo método privado ya
   * usado desde `requestGeolocation()`.
   *
   * Asunción (el spec no lo especifica): también limpia `manualLocationRequired`. El div
   * `#location-preview` que `setCoordinatesInMap()` necesita solo existe en el DOM cuando NINGÚN
   * fallback manual está activo (ver template) — si el usuario denegó el GPS y luego resuelve la
   * ubicación eligiendo una dirección, ya no tiene sentido seguir mostrando el fallback manual de
   * lat/lng: la dirección seleccionada es una ubicación válida y resuelta. `addressProviderUnavailable`
   * no se toca: seleccionar una sugerencia implica que el proveedor SÍ respondió. */
  onAddressSuggestionSelected(suggestion: AddressSuggestion): void {
    const latitude = suggestion.latitude ?? null;
    const longitude = suggestion.longitude ?? null;

    this.latitude.set(latitude);
    this.longitude.set(longitude);
    this.addressQuery.set(suggestion.address ?? '');

    if (latitude !== null && longitude !== null) {
      this.manualLocationRequired.set(false);
      this.setCoordinatesInMap({ latitude, longitude });
    }
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
        // AC-11: el guardado fallido se muestra vía notificación (consistente con el resto del
        // rediseño de la UI que ya usa `NzNotificationService` para éxito/rechazo) pero con un
        // botón de acción (`data-testid="retry-button"`) que dispara `retry()` sin volver a pedir
        // foto/ubicación — `nzDuration: 0` para que la notificación no se autocierre antes de que
        // el usuario pueda actuar sobre ella.
        this.notification.template(this.retryNotificationTemplate(), { nzDuration: 0 });
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

        // FR-04/AC-03: precompletar el campo de dirección por reverse geocoding. Un 503 del
        // proveedor (AC-19) o un `null` (sin match) NO bloquean el flujo GPS — el usuario ya tiene
        // lat/lng y el mapa ya muestra el pin, simplemente no hay texto legible que precompletar.
        // A diferencia del pipeline de `search()`, esto nunca setea `addressProviderUnavailable`:
        // ese signal es exclusivo del autocomplete (spec Block 3).
        this.addressService.reverseGeocode(coordinates.latitude, coordinates.longitude).subscribe({
          next: (suggestion) => {
            if (suggestion?.address) {
              this.addressQuery.set(suggestion.address);
            }
          },
          error: () => {
            // Proveedor caído durante el flujo GPS — no-op intencional, ver comentario arriba.
          },
        });
      },
      () => {
        this.manualLocationRequired.set(true);
      },
    );
  }

  private setCoordinatesInMap(coordinates: GeolocationCoordinates): void {
    // spec-FEAT-011 Block 3 reutiliza este método desde dos orígenes (GPS y selección de
    // sugerencia) — a diferencia de antes (una única llamada posible, desde `requestGeolocation`),
    // ahora puede invocarse más de una vez sobre el mismo `#location-preview`. Leaflet lanza si se
    // llama `L.map()` dos veces sobre el mismo contenedor sin liberar el anterior primero.
    this.map?.remove();

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
