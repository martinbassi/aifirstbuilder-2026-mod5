import { TestBed } from '@angular/core/testing';
import { provideNzIconsTesting } from 'ng-zorro-antd/icon/testing';
import { NzUploadFile } from 'ng-zorro-antd/upload';
import { Subject, of, throwError } from 'rxjs';
import { CreateMuralResponse } from '../../../core/api-client/api-client.generated';
import { GeolocationService } from '../../../shared/geolocation.service';
import { MuralService } from '../data/mural.service';
import { CreateMuralFormComponent } from './create-mural-form.component';

/**
 * `beforeUpload` es el único punto de alta de archivo desde Block 1 (`nz-upload` con
 * `nzBeforeUpload` devolviendo `false` de forma síncrona nunca dispara `nzChange` para el archivo
 * nuevo — ver Logic de spec-FEAT-008 Block 1). Los tests llaman a `beforeUpload` directamente en
 * vez de simular un file picker real (no simulable en jsdom), pasando el `File` tal cual: es
 * exactamente lo que `nz-upload` le pasa en producción (`ng-zorro-antd-upload.mjs` invoca
 * `beforeUpload` con el `File` crudo, no con un `NzUploadFile` envuelto — `originFileObj` sólo
 * existe si algo más arma la entrada a mano).
 */
function asUploadFile(file: File): NzUploadFile {
  return file as unknown as NzUploadFile;
}

function numberInputEvent(value: number): Event {
  const input = document.createElement('input');
  input.type = 'number';
  input.value = String(value);
  return { target: input } as unknown as Event;
}

// FIX-003: Title es obligatorio desde el commit 9cecf21 (`canSubmit` lo exige) — helper análogo a
// `numberInputEvent` para que los tests puedan simular su ingreso.
function titleInputEvent(value: string): Event {
  const input = document.createElement('input');
  input.type = 'text';
  input.value = value;
  return { target: input } as unknown as Event;
}

/**
 * Stubs the injected `GeolocationService` mock (Block 6) instead of `navigator.geolocation`
 * directly — the component no longer touches the browser API itself, it delegates to the
 * service. `getCurrentPosition()` now returns a `Promise`, so callers awaiting a resolved/rejected
 * test must flush a microtask (see `flushGeolocation` below) before asserting on the result.
 */
function stubGeolocation(
  geolocationService: { getCurrentPosition: ReturnType<typeof vi.fn> },
  behavior: 'success' | 'denied' | 'unsupported',
  position?: { latitude: number; longitude: number },
): void {
  if (behavior === 'success') {
    geolocationService.getCurrentPosition.mockReturnValue(
      Promise.resolve({
        latitude: position?.latitude ?? -34.6,
        longitude: position?.longitude ?? -58.4,
      }),
    );
    return;
  }

  // 'denied' and 'unsupported' both surface to the component as a rejection — which of the 3
  // typed `GeolocationError.kind` values it actually is does not matter to this component: it
  // reacts identically to any of them (falls back to manual input). `GeolocationService`'s own
  // spec is what verifies each `kind` is produced correctly.
  geolocationService.getCurrentPosition.mockReturnValue(
    Promise.reject({ kind: behavior === 'denied' ? 'denied' : 'unavailable' }),
  );
}

/** Flushes the microtask queue so the `GeolocationService.getCurrentPosition()` promise settles,
 * then re-runs change detection so the resulting signal updates reach the DOM. */
async function flushGeolocation(fixture: { detectChanges: () => void }): Promise<void> {
  await Promise.resolve();
  fixture.detectChanges();
}

describe('CreateMuralFormComponent', () => {
  let muralService: { create: ReturnType<typeof vi.fn> };
  let geolocationService: { getCurrentPosition: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    muralService = { create: vi.fn() };
    geolocationService = { getCurrentPosition: vi.fn() };

    TestBed.configureTestingModule({
      imports: [CreateMuralFormComponent],
      providers: [
        { provide: MuralService, useValue: muralService },
        { provide: GeolocationService, useValue: geolocationService },
        // nz-upload-list con nzListType="picture" renderiza incondicionalmente sus íconos (upload
        // button, delete, picture/file placeholder) — sin un registro de íconos, NzIconService
        // lanza IconNotFoundError apenas se hace detectChanges(). provideNzIconsTesting() registra
        // el set completo de @ant-design/icons-angular en vez de listar uno por uno (patrón usado
        // por login-form.component.spec.ts/register-form.component.spec.ts para íconos puntuales;
        // acá conviene el set completo porque nz-upload-list decide sus propios íconos
        // internamente, no los elige este componente).
        provideNzIconsTesting(),
      ],
    });
  });

  // `vi.spyOn(URL, 'revokeObjectURL')` (Block 2 tests) envuelve el método global sin restaurarlo
  // solo: sin este afterEach, los spies se anidan entre tests y sus contadores de llamadas se
  // arrastran de un test a otro.
  afterEach(() => {
    vi.restoreAllMocks();
  });

  // Required test (Block 1, AC-01): archivo válido (JPEG ≤10MB) → preview inmediato.
  it('selecciona un archivo válido y muestra el preview inmediatamente', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });

    const result = component.beforeUpload(asUploadFile(validFile));
    fixture.detectChanges();

    expect(result).toBe(false);
    expect(component.fileList()).toHaveLength(1);
    expect(component.fileList()[0].thumbUrl).toBeTruthy();
    expect(component.fileError()).toBeNull();
    const previewImg: HTMLImageElement = fixture.nativeElement.querySelector(
      '[data-testid="photo-upload"] img',
    );
    expect(previewImg).toBeTruthy();
  });

  // Required test (Block 1, AC-04): archivo >10MB → error inline, sin preview.
  it('rechaza un archivo oversized con error inline y no arma preview', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const oversizedFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    Object.defineProperty(oversizedFile, 'size', { value: 11 * 1024 * 1024 });

    const result = component.beforeUpload(asUploadFile(oversizedFile));
    fixture.detectChanges();

    expect(result).toBe(false);
    expect(component.fileError()).toBe('El archivo no puede superar los 10 MB.');
    expect(component.fileList()).toHaveLength(0);
    const previewImg = fixture.nativeElement.querySelector('[data-testid="photo-upload"] img');
    expect(previewImg).toBeFalsy();
    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    expect(submitButton.disabled).toBe(true);
  });

  // Required test (Block 1, AC-05): archivo de tipo inválido (application/pdf) → error inline, sin preview.
  it('rechaza un archivo de tipo inválido con error inline y no arma preview', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const invalidTypeFile = new File(['%PDF-1.4'], 'wall.pdf', { type: 'application/pdf' });

    const result = component.beforeUpload(asUploadFile(invalidTypeFile));
    fixture.detectChanges();

    expect(result).toBe(false);
    expect(component.fileError()).toBe('El archivo debe ser una imagen JPEG, PNG o WebP.');
    expect(component.fileList()).toHaveLength(0);
  });

  // Required test (Block 1, AC-08): sin archivo seleccionado → "Guardar" deshabilitado.
  it('deshabilita Guardar cuando no hay ningún archivo seleccionado', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    fixture.detectChanges();

    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    expect(submitButton.disabled).toBe(true);
  });

  // Required test 3: geolocalización exitosa → lat/lng se completan solos (AC-03).
  it('completa latitud y longitud automáticamente cuando la geolocalización tiene éxito', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.6037, longitude: -58.3816 });

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    expect(component.latitude()).toBe(-34.6037);
    expect(component.longitude()).toBe(-58.3816);
    expect(component.manualLocationRequired()).toBe(false);
    const manualInput = fixture.nativeElement.querySelector('[data-testid="latitude-input"]');
    expect(manualInput).toBeFalsy();
  });

  // Required test 4: geolocalización denegada → aparecen inputs manuales, formulario sigue usable (AC-04).
  it('revela inputs manuales cuando la geolocalización es denegada, sin bloquear el formulario', async () => {
    stubGeolocation(geolocationService, 'denied');

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    expect(component.manualLocationRequired()).toBe(true);
    const latitudeInput = fixture.nativeElement.querySelector('[data-testid="latitude-input"]');
    const longitudeInput = fixture.nativeElement.querySelector('[data-testid="longitude-input"]');
    expect(latitudeInput).toBeTruthy();
    expect(longitudeInput).toBeTruthy();

    // El resto del formulario (selector de foto) sigue disponible.
    const photoUpload = fixture.nativeElement.querySelector('[data-testid="photo-upload"]');
    expect(photoUpload).toBeTruthy();
  });

  // Required test 5: ingreso manual válido → "Guardar" habilitado (AC-05).
  it('habilita Guardar cuando el ingreso manual de coordenadas es válido junto con una foto válida', async () => {
    stubGeolocation(geolocationService, 'unsupported');

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    expect(component.canSubmit()).toBe(false);

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    component.onTitleChange(titleInputEvent('Mural de prueba'));
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
  it('muestra el mensaje de confirmación tras un envío exitoso', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.6, longitude: -58.4 });
    muralService.create.mockReturnValue(
      of(new CreateMuralResponse({ id: 'mural-1', status: 'Pending' })),
    );

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    component.onTitleChange(titleInputEvent('Mural de prueba'));
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
  it('conserva foto y ubicación tras un envío fallido y reintenta sin pedir datos de nuevo', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.6, longitude: -58.4 });
    muralService.create
      .mockReturnValueOnce(throwError(() => ({ status: 500, message: 'No se pudo guardar el mural. Intentá nuevamente.' })))
      .mockReturnValueOnce(of(new CreateMuralResponse({ id: 'mural-1', status: 'Pending' })));

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    component.onTitleChange(titleInputEvent('Mural de prueba'));
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
      title: 'Mural de prueba',
      latitude: -34.6,
      longitude: -58.4,
    });
    const successEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="success-message"]',
    );
    expect(successEl).toBeTruthy();
  });

  // Required test (Block 2, AC-02): reemplazar el archivo revoca el thumbUrl anterior antes de
  // asignar el nuevo.
  it('revoca el thumbUrl anterior al reemplazar el archivo seleccionado', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL');
    const firstFile = new File(['x'], 'wall-1.jpg', { type: 'image/jpeg' });
    const secondFile = new File(['y'], 'wall-2.jpg', { type: 'image/jpeg' });

    component.beforeUpload(asUploadFile(firstFile));
    fixture.detectChanges();
    const firstUrl = component.fileList()[0].thumbUrl;

    component.beforeUpload(asUploadFile(secondFile));
    fixture.detectChanges();

    expect(component.fileList()).toHaveLength(1);
    expect(component.fileList()[0].originFileObj).toBe(secondFile);
    expect(revokeSpy).toHaveBeenCalledWith(firstUrl);
  });

  // Required test (Block 2, AC-03): click en el ícono de eliminar de nz-upload-list limpia el
  // estado (fileList, selectedFile, fileError) y deshabilita "Guardar" de nuevo.
  it('limpia el estado cuando se elimina el archivo desde nz-upload-list', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    fixture.detectChanges();

    // `.ant-upload-list-item-card-actions-btn` matches BOTH the download and the delete buttons
    // (nz-upload-list renders both when `nzShowUploadList` is truthy) — narrow down to the one
    // whose icon is `nz-icon[nzType="delete"]` (rendered as class `anticon-delete`), never the
    // first match, or this would click "download" instead.
    const removeButton: HTMLButtonElement | null = fixture.nativeElement.querySelector(
      '.ant-upload-list-item-card-actions-btn:has(.anticon-delete)',
    );
    expect(removeButton).toBeTruthy();
    removeButton!.click();
    fixture.detectChanges();

    expect(component.fileList()).toHaveLength(0);
    expect(component.selectedFile()).toBeNull();
    expect(component.fileError()).toBeNull();
    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    expect(submitButton.disabled).toBe(true);
  });

  // Required test (Block 2, AC-06): el botón "Guardar" muestra nzLoading hasta que el submit
  // resuelve, y ninguna subida real se dispara antes del submit explícito.
  it('mantiene nzLoading durante el submit sin disparar ninguna subida antes del envío explícito', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.6, longitude: -58.4 });
    const createSubject = new Subject<CreateMuralResponse>();
    muralService.create.mockReturnValue(createSubject.asObservable());

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    component.onTitleChange(titleInputEvent('Mural de prueba'));
    fixture.detectChanges();

    expect(muralService.create).not.toHaveBeenCalled();

    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    submitButton.click();
    fixture.detectChanges();

    expect(muralService.create).toHaveBeenCalledTimes(1);
    expect(component.submitting()).toBe(true);
    expect(submitButton.classList.contains('ant-btn-loading')).toBe(true);

    createSubject.next(new CreateMuralResponse({ id: 'mural-1', status: 'Pending' }));
    createSubject.complete();
    fixture.detectChanges();

    expect(component.submitting()).toBe(false);
    expect(submitButton.classList.contains('ant-btn-loading')).toBe(false);
  });

  // Required test (Block 2, AC-07/NFR-01): reemplazar el archivo y destruir el componente revoca
  // el thumbUrl tanto en el reemplazo como en el destroy.
  it('revoca el thumbUrl en el reemplazo y en el destroy del componente', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL');
    const firstFile = new File(['x'], 'wall-1.jpg', { type: 'image/jpeg' });
    const secondFile = new File(['y'], 'wall-2.jpg', { type: 'image/jpeg' });

    component.beforeUpload(asUploadFile(firstFile));
    fixture.detectChanges();
    const firstUrl = component.fileList()[0].thumbUrl;

    component.beforeUpload(asUploadFile(secondFile));
    fixture.detectChanges();
    const secondUrl = component.fileList()[0].thumbUrl;

    expect(revokeSpy).toHaveBeenCalledWith(firstUrl);

    fixture.destroy();

    expect(revokeSpy).toHaveBeenCalledWith(secondUrl);
    expect(revokeSpy).toHaveBeenCalledTimes(2);
  });

  // Edge case (no-op seguro): destruir el componente sin haber seleccionado nunca un archivo no
  // debe lanzar excepción ni invocar URL.revokeObjectURL.
  it('no lanza excepción ni revoca nada al destruir sin haber seleccionado un archivo', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    fixture.detectChanges();

    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL');

    expect(() => fixture.destroy()).not.toThrow();
    expect(revokeSpy).not.toHaveBeenCalled();
  });
});
