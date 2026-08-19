import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CreateMuralResponse } from '../../../core/api-client/api-client.generated';
import { MuralService } from '../data/mural.service';
import { CreateMuralFormComponent } from './create-mural-form.component';

/**
 * Builds a synthetic file-input `change` Event carrying `file` in `target.files`, the same shape
 * `onFileSelected` reads from a real `<input type="file">`. Used instead of driving a real file
 * picker (not simulable in jsdom) — same "call the handler directly" style already used by
 * `login-form.component.spec.ts` (`component.form.setValue(...)` + `component.submit()`).
 */
function fileChangeEvent(file: File | null): Event {
  const input = document.createElement('input');
  Object.defineProperty(input, 'files', { value: file ? [file] : [] });
  return { target: input } as unknown as Event;
}

function numberInputEvent(value: number): Event {
  const input = document.createElement('input');
  input.type = 'number';
  input.value = String(value);
  return { target: input } as unknown as Event;
}

function stubGeolocation(
  behavior: 'success' | 'denied' | 'unsupported',
  position?: { latitude: number; longitude: number },
): void {
  if (behavior === 'unsupported') {
    Object.defineProperty(navigator, 'geolocation', { value: undefined, configurable: true });
    return;
  }

  Object.defineProperty(navigator, 'geolocation', {
    configurable: true,
    value: {
      getCurrentPosition: (success: PositionCallback, error?: PositionErrorCallback) => {
        if (behavior === 'success') {
          success({
            coords: {
              latitude: position?.latitude ?? -34.6,
              longitude: position?.longitude ?? -58.4,
            },
          } as GeolocationPosition);
        } else if (error) {
          error({ code: 1, message: 'User denied Geolocation' } as GeolocationPositionError);
        }
      },
    },
  });
}

describe('CreateMuralFormComponent', () => {
  let muralService: { create: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    muralService = { create: vi.fn() };

    TestBed.configureTestingModule({
      imports: [CreateMuralFormComponent],
      providers: [{ provide: MuralService, useValue: muralService }],
    });
  });

  afterEach(() => {
    Object.defineProperty(navigator, 'geolocation', { value: undefined, configurable: true });
  });

  // Required test 1: archivo oversized → error inline, "Guardar" deshabilitado (AC-02).
  it('rechaza un archivo oversized con error inline y deshabilita Guardar', () => {
    stubGeolocation('success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const oversizedFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    Object.defineProperty(oversizedFile, 'size', { value: 11 * 1024 * 1024 });

    component.onFileSelected(fileChangeEvent(oversizedFile));
    fixture.detectChanges();

    expect(component.fileError()).toBeTruthy();
    expect(component.canSubmit()).toBe(false);
    const errorEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="file-error-message"]',
    );
    expect(errorEl).toBeTruthy();
    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    expect(submitButton.disabled).toBe(true);
  });

  // Required test 2: archivo no-imagen → error inline, "Guardar" deshabilitado (AC-01, camino inverso).
  it('rechaza un archivo no-imagen con error inline y deshabilita Guardar', () => {
    stubGeolocation('success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const notAnImage = new File(['not an image'], 'wall.txt', { type: 'text/plain' });

    component.onFileSelected(fileChangeEvent(notAnImage));
    fixture.detectChanges();

    expect(component.fileError()).toBeTruthy();
    expect(component.canSubmit()).toBe(false);
    const errorEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="file-error-message"]',
    );
    expect(errorEl).toBeTruthy();
  });

  // Required test 3: geolocalización exitosa → lat/lng se completan solos (AC-03).
  it('completa latitud y longitud automáticamente cuando la geolocalización tiene éxito', () => {
    stubGeolocation('success', { latitude: -34.6037, longitude: -58.3816 });

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.latitude()).toBe(-34.6037);
    expect(component.longitude()).toBe(-58.3816);
    expect(component.manualLocationRequired()).toBe(false);
    const manualInput = fixture.nativeElement.querySelector('[data-testid="latitude-input"]');
    expect(manualInput).toBeFalsy();
  });

  // Required test 4: geolocalización denegada → aparecen inputs manuales, formulario sigue usable (AC-04).
  it('revela inputs manuales cuando la geolocalización es denegada, sin bloquear el formulario', () => {
    stubGeolocation('denied');

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.manualLocationRequired()).toBe(true);
    const latitudeInput = fixture.nativeElement.querySelector('[data-testid="latitude-input"]');
    const longitudeInput = fixture.nativeElement.querySelector('[data-testid="longitude-input"]');
    expect(latitudeInput).toBeTruthy();
    expect(longitudeInput).toBeTruthy();

    // El resto del formulario (selector de foto) sigue disponible.
    const photoInput = fixture.nativeElement.querySelector('[data-testid="photo-input"]');
    expect(photoInput).toBeTruthy();
  });

  // Required test 5: ingreso manual válido → "Guardar" habilitado (AC-05).
  it('habilita Guardar cuando el ingreso manual de coordenadas es válido junto con una foto válida', () => {
    stubGeolocation('unsupported');

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.canSubmit()).toBe(false);

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.onFileSelected(fileChangeEvent(validFile));
    component.onLatitudeChange(numberInputEvent(-34.6));
    component.onLongitudeChange(numberInputEvent(-58.4));
    fixture.detectChanges();

    expect(component.canSubmit()).toBe(true);
    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    expect(submitButton.disabled).toBe(false);
  });

  // Required test 6: envío exitoso → mensaje de confirmación visible (AC-12).
  it('muestra el mensaje de confirmación tras un envío exitoso', () => {
    stubGeolocation('success', { latitude: -34.6, longitude: -58.4 });
    muralService.create.mockReturnValue(
      of(new CreateMuralResponse({ id: 'mural-1', status: 'Pending' })),
    );

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.onFileSelected(fileChangeEvent(validFile));
    fixture.detectChanges();

    component.submit();
    fixture.detectChanges();

    const successEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="success-message"]',
    );
    expect(successEl).toBeTruthy();
    expect(successEl.textContent).toContain('pendiente de revisión');
  });

  // Required test 7: envío fallido → foto y ubicación se conservan, "Reintentar" vuelve a enviar
  // sin pedir datos de nuevo (AC-11).
  it('conserva foto y ubicación tras un envío fallido y reintenta sin pedir datos de nuevo', () => {
    stubGeolocation('success', { latitude: -34.6, longitude: -58.4 });
    muralService.create
      .mockReturnValueOnce(throwError(() => ({ status: 500, message: 'No se pudo guardar el mural. Intentá nuevamente.' })))
      .mockReturnValueOnce(of(new CreateMuralResponse({ id: 'mural-1', status: 'Pending' })));

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.onFileSelected(fileChangeEvent(validFile));
    fixture.detectChanges();

    component.submit();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('No se pudo guardar el mural. Intentá nuevamente.');
    expect(component.selectedFile()).toBe(validFile);
    expect(component.latitude()).toBe(-34.6);
    expect(component.longitude()).toBe(-58.4);
    const retryButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="retry-button"]',
    );
    expect(retryButton).toBeTruthy();

    retryButton.click();
    fixture.detectChanges();

    expect(muralService.create).toHaveBeenCalledTimes(2);
    expect(muralService.create).toHaveBeenNthCalledWith(2, {
      photo: validFile,
      latitude: -34.6,
      longitude: -58.4,
    });
    const successEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="success-message"]',
    );
    expect(successEl).toBeTruthy();
  });
});
